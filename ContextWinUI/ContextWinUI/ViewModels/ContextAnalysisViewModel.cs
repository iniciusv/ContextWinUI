using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.ViewModels;

public partial class ContextAnalysisViewModel : ObservableObject
{
	private readonly IRoslynAnalyzerService _roslynAnalyzer;
	private readonly IFileSystemService _fileSystemService;
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly IGitService _gitService; // <--- Git Service

	public ITagManagementUiService TagService { get; }

	private readonly Stack<List<FileSystemItem>> _historyStack = new();

	// Coleções
	[ObservableProperty]
	private ObservableCollection<FileSystemItem> contextTreeItems = new(); // Árvore

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> selectedItemsList = new(); // Lista Seleção

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> gitModifiedItems = new(); // Lista Git

	[ObservableProperty]
	private FileSystemItem? selectedItem;

	[ObservableProperty]
	private bool isVisible;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private int selectedCount;

	[ObservableProperty]
	private bool canGoBack;

	public event EventHandler<FileSystemItem>? FileSelectedForPreview;
	public event EventHandler<string>? StatusChanged;

	public ContextAnalysisViewModel(
			IRoslynAnalyzerService roslynAnalyzer,
			IFileSystemService fileSystemService,
			IFileSystemItemFactory itemFactory,
			ITagManagementUiService tagService,
			IGitService gitService)
	{
		_roslynAnalyzer = roslynAnalyzer;
		_fileSystemService = fileSystemService;
		_itemFactory = itemFactory;
		TagService = tagService;
		_gitService = gitService;
	}

	// --- PREVIEW RÁPIDO (Chamado pelo FileSelectionViewModel) ---
	public void UpdateSelectionPreview(IEnumerable<FileSystemItem> items)
	{
		if (IsLoading) return;

		ContextTreeItems.Clear();
		SelectedItemsList.Clear();

		foreach (var item in items)
		{
			// Cria nó visual simples
			var fileNode = _itemFactory.CreateWrapper(item.FullPath, FileSystemItemType.File, "\uE943");
			RegisterItemEventsRecursively(fileNode);

			ContextTreeItems.Add(fileNode);

			// Adiciona na lista plana se não existir
			if (!SelectedItemsList.Any(x => x.FullPath == item.FullPath))
			{
				SelectedItemsList.Add(fileNode);
			}
		}
		UpdateSelectedCount();
	}

	// --- ANÁLISE COMPLETA (Roslyn) ---
	public async Task AnalyzeContextAsync(List<FileSystemItem> selectedItems, string rootPath)
	{
		if (!selectedItems.Any()) return;

		IsLoading = true;
		IsVisible = true;

		if (ContextTreeItems.Any())
		{
			_historyStack.Push(ContextTreeItems.ToList());
			UpdateCanGoBack();
		}

		ContextTreeItems.Clear();
		SelectedItemsList.Clear();

		OnStatusChanged("Analisando estrutura do código...");

		try
		{
			await _roslynAnalyzer.IndexProjectAsync(rootPath);

			foreach (var item in selectedItems)
			{
				var fileNode = _itemFactory.CreateWrapper(item.FullPath, FileSystemItemType.File, "\uE943");
				await PopulateNodeAsync(fileNode, item.FullPath);
				RegisterItemEventsRecursively(fileNode);
				ContextTreeItems.Add(fileNode);
			}

			UpdateSelectedCount();
			OnStatusChanged($"Análise concluída.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro na análise: {ex.Message}");
		}
		finally { IsLoading = false; }
	}

	// --- GIT INTEGRATION ---
	[RelayCommand]
	public async Task RefreshGitChangesAsync(string rootPath)
	{
		if (string.IsNullOrEmpty(rootPath) || !_gitService.IsGitRepository(rootPath))
		{
			GitModifiedItems.Clear();
			return;
		}

		IsLoading = true;
		// Não muda mensagem global para não poluir, ou usa evento separado

		try
		{
			var changedFiles = await _gitService.GetModifiedFilesAsync(rootPath);
			GitModifiedItems.Clear();

			foreach (var path in changedFiles)
			{
				// Usa Factory para manter estado das tags
				var item = _itemFactory.CreateWrapper(path, FileSystemItemType.File, "\uE70F"); // Ícone Edit
				RegisterItemEventsRecursively(item);
				GitModifiedItems.Add(item);
			}
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro Git: {ex.Message}");
		}
		finally { IsLoading = false; }
	}

	// --- LÓGICA DE POPULAR NÓS ---
	private async Task PopulateNodeAsync(FileSystemItem node, string filePath)
	{
		var analysis = await _roslynAnalyzer.AnalyzeFileStructureAsync(filePath);

		if (analysis.Methods.Any())
		{
			var methodsGroup = _itemFactory.CreateWrapper($"{filePath}::methods", FileSystemItemType.LogicalGroup, "\uEA86");
			methodsGroup.SharedState.Name = "Métodos";

			foreach (var method in analysis.Methods)
			{
				var methodItem = _itemFactory.CreateWrapper($"{filePath}::{method}", FileSystemItemType.Method, "\uF158");
				methodItem.SharedState.Name = method;
				methodItem.MethodSignature = method;
				methodsGroup.Children.Add(methodItem);
			}
			node.Children.Add(methodsGroup);
		}

		if (analysis.Dependencies.Any())
		{
			var contextGroup = _itemFactory.CreateWrapper($"{filePath}::deps", FileSystemItemType.LogicalGroup, "\uE71D");
			contextGroup.SharedState.Name = "Contexto";

			foreach (var depPath in analysis.Dependencies)
			{
				var depItem = _itemFactory.CreateWrapper(depPath, FileSystemItemType.Dependency, "\uE943");
				depItem.IsChecked = true;
				contextGroup.Children.Add(depItem);
			}
			node.Children.Add(contextGroup);
		}
		node.IsExpanded = true;
	}

	// --- EVENTOS E HELPERS ---
	private void RegisterItemEventsRecursively(FileSystemItem item)
	{
		item.PropertyChanged -= OnItemPropertyChanged;
		item.PropertyChanged += OnItemPropertyChanged;
		if (item.IsChecked) AddToSelectionListIfNew(item);
		foreach (var child in item.Children) RegisterItemEventsRecursively(child);
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is FileSystemItem item && e.PropertyName == nameof(FileSystemItem.IsChecked))
		{
			if (item.IsChecked) AddToSelectionListIfNew(item);
			else RemoveFromSelectionList(item);
			UpdateSelectedCount();
		}
	}

	private void AddToSelectionListIfNew(FileSystemItem item)
	{
		if (!SelectedItemsList.Any(x => x.FullPath == item.FullPath)) SelectedItemsList.Add(item);
	}

	private void RemoveFromSelectionList(FileSystemItem item)
	{
		var toRemove = SelectedItemsList.FirstOrDefault(x => x.FullPath == item.FullPath);
		if (toRemove != null) SelectedItemsList.Remove(toRemove);
	}

	private void UpdateSelectedCount() => SelectedCount = SelectedItemsList.Count;

	// --- COMANDOS VISUAIS ---
	[RelayCommand]
	private async Task AnalyzeItemDepthAsync(FileSystemItem item)
	{
		if (item == null || string.IsNullOrEmpty(item.FullPath)) return;
		IsLoading = true;
		try
		{
			_historyStack.Push(ContextTreeItems.ToList());
			UpdateCanGoBack();
			item.Children.Clear();
			await PopulateNodeAsync(item, item.FullPath);
			RegisterItemEventsRecursively(item);
			UpdateSelectedCount();
		}
		finally { IsLoading = false; }
	}

	[RelayCommand]
	private async Task AnalyzeMethodFlowAsync(FileSystemItem item)
	{
		if (item == null) return;
		IsLoading = true;
		try
		{
			_historyStack.Push(ContextTreeItems.ToList());
			UpdateCanGoBack();
			ContextTreeItems.Clear();
			var flowRoot = _itemFactory.CreateWrapper($"{item.FullPath}::flow", FileSystemItemType.LogicalGroup, "\uE768");
			flowRoot.SharedState.Name = $"Fluxo: {item.Name}";
			await PopulateNodeAsync(flowRoot, item.FullPath);
			RegisterItemEventsRecursively(flowRoot);
			ContextTreeItems.Add(flowRoot);
			UpdateSelectedCount();
		}
		finally { IsLoading = false; }
	}

	[RelayCommand] private void ExpandAll() { foreach (var item in ContextTreeItems) item.SetExpansionRecursively(true); }
	[RelayCommand] private void CollapseAll() { foreach (var item in ContextTreeItems) item.SetExpansionRecursively(false); }

	[RelayCommand]
	private void SyncFocus()
	{
		if (SelectedItem == null) return;
		foreach (var item in ContextTreeItems) SyncFocusRecursive(item, SelectedItem);
	}

	private bool SyncFocusRecursive(FileSystemItem current, FileSystemItem target)
	{
		if (current.FullPath == target.FullPath) { if (current.Children.Any()) current.IsExpanded = true; return true; }
		bool keep = false;
		foreach (var child in current.Children) if (SyncFocusRecursive(child, target)) keep = true;
		current.IsExpanded = keep;
		return keep;
	}

	[RelayCommand] private void Search(string query) => TreeSearchHelper.Search(ContextTreeItems, query);

	[RelayCommand]
	private void GoBack()
	{
		if (_historyStack.Count > 0)
		{
			var prev = _historyStack.Pop();
			ContextTreeItems.Clear();
			foreach (var item in prev) { ContextTreeItems.Add(item); RegisterItemEventsRecursively(item); }
			UpdateCanGoBack();
			UpdateSelectedCount();
		}
	}
	private void UpdateCanGoBack() => CanGoBack = _historyStack.Count > 0;

	public void SelectFileForPreview(FileSystemItem item)
	{
		SelectedItem = item;
		if (!string.IsNullOrEmpty(item.FullPath) && File.Exists(item.FullPath))
			FileSelectedForPreview?.Invoke(this, item);
	}

	[RelayCommand]
	private void Close()
	{
		IsVisible = false;
		ContextTreeItems.Clear();
		SelectedItemsList.Clear();
		GitModifiedItems.Clear();
		_historyStack.Clear();
		UpdateCanGoBack();
		SelectedItem = null;
	}

	[RelayCommand]
	private async Task CopyContextToClipboardAsync()
	{
		IsLoading = true;
		try
		{
			if (!SelectedItemsList.Any()) return;
			var sb = new StringBuilder();
			sb.AppendLine("/* CONTEXTO SELECIONADO */");
			sb.AppendLine();

			// Aqui você deve usar o CodeCleanupHelper se desejar aplicar filtros também
			// ou deixar cru. Vou deixar a lógica básica aqui.
			foreach (var item in SelectedItemsList.OrderBy(x => x.FullPath))
			{
				if (string.IsNullOrEmpty(item.FullPath)) continue;
				try
				{
					var content = await _fileSystemService.ReadFileContentAsync(item.FullPath);
					sb.AppendLine($"// Arquivo: {item.Name}");
					sb.AppendLine(content);
					sb.AppendLine();
				}
				catch { }
			}
			var dp = new DataPackage();
			dp.SetText(sb.ToString());
			Clipboard.SetContent(dp);
			OnStatusChanged("Copiado com sucesso!");
		}
		finally { IsLoading = false; }
	}

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
}