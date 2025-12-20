using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.Core.Shared; // Ou ViewModels, dependendo de onde salvou

public partial class FileSelectionViewModel : ObservableObject
{
	private readonly IContentGenerationService _contentGenService;
	private readonly IProjectSessionManager _sessionManager;
	private ObservableCollection<FileSystemItem> _rootItems = new();

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(CopySelectedFilesCommand))]
	private int selectedFilesCount;

	[ObservableProperty] private bool isLoading;
	public event EventHandler<string>? StatusChanged;

	public FileSelectionViewModel(IContentGenerationService contentGenService, IProjectSessionManager sessionManager)
	{
		_contentGenService = contentGenService;
		_sessionManager = sessionManager;

		// --- CORREÇÃO PRINCIPAL ---
		// Assina o evento para receber os arquivos quando um projeto é aberto
		_sessionManager.ProjectLoaded += OnProjectLoaded;
	}

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		SetRootItems(e.RootItems);
	}

	public void SetRootItems(ObservableCollection<FileSystemItem> items)
	{
		_rootItems = items;
		// Recalcula caso já venha algo marcado (ex: do cache)
		RecalculateSelection();
	}

	// Método necessário para o MainViewModel pegar os arquivos
	public IEnumerable<FileSystemItem> GetCheckedFiles()
	{
		return TreeTraversalHelper.GetCheckedItems(_rootItems);
	}

	public void RecalculateSelection() => SelectedFilesCount = TreeTraversalHelper.CountCheckedFiles(_rootItems);

	[RelayCommand]
	private void SelectAll()
	{
		TreeTraversalHelper.SetAllChecked(_rootItems, true);
		RecalculateSelection();
	}

	[RelayCommand]
	private void UnselectAll()
	{
		TreeTraversalHelper.SetAllChecked(_rootItems, false);
		RecalculateSelection();
	}

	[RelayCommand(CanExecute = nameof(CanCopySelectedFiles))]
	private async Task CopySelectedFilesAsync()
	{
		var selectedFiles = TreeTraversalHelper.GetCheckedItems(_rootItems).ToList();
		if (!selectedFiles.Any()) return;

		IsLoading = true;
		OnStatusChanged($"Processando {selectedFiles.Count} arquivos...");

		try
		{
			string finalContent = await _contentGenService.GenerateContentAsync(selectedFiles, _sessionManager);

			var dataPackage = new DataPackage();
			dataPackage.SetText(finalContent);
			Clipboard.SetContent(dataPackage);

			OnStatusChanged("Copiado com sucesso!");
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

	private bool CanCopySelectedFiles() => SelectedFilesCount > 0;
	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
}