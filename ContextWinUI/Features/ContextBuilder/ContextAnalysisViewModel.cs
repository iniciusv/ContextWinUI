using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using Microsoft.UI.Dispatching;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.Features.ContextBuilder;

public partial class ContextAnalysisViewModel : ObservableObject
{
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly IDependencyAnalysisOrchestrator _analysisOrchestrator;
	private readonly IProjectSessionManager _sessionManager;
	private readonly IFileSelectionService _fileSelectionService;
	private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

	// Removida a referência ao TreeVM
	public ContextGitViewModel GitVM { get; }
	public ContextSelectionViewModel SelectionVM { get; }
	public ITagManagementUiService TagService { get; }

	[ObservableProperty] private bool isVisible;
	[ObservableProperty] private bool isLoading;
	[ObservableProperty] private FileSystemItem? selectedPreviewItem;

	public int SelectedCount => SelectionVM.SelectedItemsList.Count;

	public event EventHandler<string>? StatusChanged;

	public ContextAnalysisViewModel(
		IFileSystemItemFactory itemFactory,
		IDependencyAnalysisOrchestrator analysisOrchestrator,
		IProjectSessionManager sessionManager,
		IGitService gitService,
		ITagManagementUiService tagService,
		ContextSelectionViewModel selectionVM,
		IFileSelectionService fileSelectionService,
        ContextWinUI.Features.GraphParser.ViewModels.IGraphParserContract graphParser)
	{
		_itemFactory = itemFactory;
		_analysisOrchestrator = analysisOrchestrator;
		_sessionManager = sessionManager;
		_fileSelectionService = fileSelectionService;
		TagService = tagService;
		SelectionVM = selectionVM;

		GitVM = new ContextGitViewModel(gitService, itemFactory, sessionManager, graphParser);
        GitVM.AddFilesToContextRequested += GitVM_AddFilesToContextRequested;

		SelectionVM.SelectedItemsList.CollectionChanged += (s, e) =>
			OnPropertyChanged(nameof(SelectedCount));
	}

    private void GitVM_AddFilesToContextRequested(object? sender, System.Collections.Generic.List<string> paths)
    {
        // Add files to selection
        SelectionVM.ProcessPaths(paths);
        OnStatusChanged($"{paths.Count} arquivos adicionados à seleção.");
    }

	[RelayCommand]
	public async Task AnalyzeContextAsync()
	{
		if (!_sessionManager.IsProjectLoaded ||
			SelectionVM.SelectedItemsList.Count == 0)
		{
			OnStatusChanged("Nenhum item selecionado para análise.");
			return;
		}

		IsLoading = true;
		IsVisible = true;

		try
		{
			OnStatusChanged("Sincronizando contexto...");

			// Atualiza arquivos modificados no Git
			await GitVM.RefreshChangesAsync();

			OnStatusChanged("Contexto atualizado.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

	public void SelectFileForPreview(FileSystemItem item)
	{
		SelectedPreviewItem = item;
		string realPath = item.FullPath;

		if (item.FullPath.Contains("::"))
			realPath = item.FullPath.Substring(0, item.FullPath.IndexOf("::"));

		if (!string.IsNullOrEmpty(realPath) && System.IO.File.Exists(realPath))
		{
			FileSystemItem itemToSend = (item.Type != FileSystemItemType.File)
				? _itemFactory.CreateWrapper(realPath, FileSystemItemType.File)
				: item;

			_fileSelectionService.SetSelection(itemToSend);
		}
	}

	[RelayCommand]
	public async Task CopyContextToClipboardAsync()
	{
		var items = SelectionVM.GetCheckedFiles().ToList();

		if (!items.Any())
		{
			OnStatusChanged("Nenhum item selecionado para cópia.");
			return;
		}

		IsLoading = true;

		try
		{
			string text = await _analysisOrchestrator.BuildContextStringAsync(
				items, _sessionManager);

			var dp = new DataPackage();
			dp.SetText(text);
			Clipboard.SetContent(dp);

			OnStatusChanged("Contexto copiado para a área de transferência!");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao copiar: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

	[RelayCommand]
	private void Close()
	{
		IsVisible = false;
	}

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
}