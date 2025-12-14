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
	private readonly IFileSystemService _fileSystemService; // Usado para leitura de conteúdo na cópia
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly IGitService _gitService;

	// --- DEPENDÊNCIAS PÚBLICAS ---
	public ITagManagementUiService TagService { get; }

	// O ViewModel Filho que gerencia a Lista Plana e IO
	public ContextSelectionViewModel SelectionVM { get; }

	// --- ESTADO INTERNO ---
	private readonly Stack<List<FileSystemItem>> _historyStack = new();

	// Coleções que este VM ainda gerencia (Árvore e Git)
	[ObservableProperty]
	private ObservableCollection<FileSystemItem> contextTreeItems = new();

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> gitModifiedItems = new();

	[ObservableProperty]
	private FileSystemItem? selectedItem;

	[ObservableProperty]
	private bool isVisible;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private bool canGoBack;

	// Atalho para View exibir contagem
	public int SelectedCount => SelectionVM.SelectedItemsList.Count;

	public event EventHandler<FileSystemItem>? FileSelectedForPreview;
	public event EventHandler<string>? StatusChanged;

	public ContextAnalysisViewModel(
			IRoslynAnalyzerService roslynAnalyzer,
			IFileSystemService fileSystemService,
			IFileSystemItemFactory itemFactory,
			ITagManagementUiService tagService,
			IGitService gitService,
			ContextSelectionViewModel selectionVM) // <--- Recebe o filho injetado
	{
		_roslynAnalyzer = roslynAnalyzer;
		_fileSystemService = fileSystemService;
		_itemFactory = itemFactory;
		TagService = tagService;
		_gitService = gitService;
		SelectionVM = selectionVM;

		// Ouve mudanças no filho para notificar a View sobre o contador
		SelectionVM.SelectedItemsList.CollectionChanged += (s, e) =>
		{
			OnPropertyChanged(nameof(SelectedCount));
		};
	}

	// --- MÉTODOS DE ORQUESTRAÇÃO ---

	// Chamado em tempo real quando checkboxes mudam no Explorer
	public void UpdateSelectionPreview(IEnumerable<FileSystemItem> items)
	{
		if (IsLoading) return;

		ContextTreeItems.Clear();
		SelectionVM.Clear(); // Delega limpeza

		foreach (var item in items)
		{
			// Cria wrapper visual
			var fileNode = _itemFactory.CreateWrapper(item.FullPath, FileSystemItemType.File, "\uE943");
			RegisterItemEventsRecursively(fileNode);

			ContextTreeItems.Add(fileNode);
			SelectionVM.AddItem(fileNode); // Delega adição
		}
	}

	// Chamado pelo botão "Analisar Contexto" (Roslyn pesado)
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
		// Não limpamos o SelectionVM aqui obrigatoriamente, 
		// mas geralmente queremos sincronizar com o que foi pedido.
		SelectionVM.Clear();

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
				SelectionVM.AddItem(fileNode);
			}

			OnStatusChanged($"Análise concluída.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro na análise: {ex.Message}");
		}
		finally { IsLoading = false; }
	}

	// --- LÓGICA DE POPULAÇÃO (ROSLYN) ---
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
			contextGroup.SharedState.Name = "Dependências";

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

	// --- GIT ---
	[RelayCommand]
	public async Task RefreshGitChangesAsync(string rootPath)
	{
		if (string.IsNullOrEmpty(rootPath) || !_gitService.IsGitRepository(rootPath))
		{
			GitModifiedItems.Clear();
			return;
		}

		IsLoading = true;
		try
		{
			var changedFiles = await _gitService.GetModifiedFilesAsync(rootPath);
			GitModifiedItems.Clear();

			foreach (var path in changedFiles)
			{
				var item = _itemFactory.CreateWrapper(path, FileSystemItemType.File, "\uE70F");
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

	// --- GERENCIAMENTO DE EVENTOS ---
	private void RegisterItemEventsRecursively(FileSystemItem item)
	{
		item.PropertyChanged -= OnItemPropertyChanged;
		item.PropertyChanged += OnItemPropertyChanged;

		// Se já vier marcado, adiciona na lista de seleção
		if (item.IsChecked) SelectionVM.AddItem(item);

		foreach (var child in item.Children) RegisterItemEventsRecursively(child);
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is FileSystemItem item && e.PropertyName == nameof(FileSystemItem.IsChecked))
		{
			// Sincroniza check visual da árvore com a lista de seleção
			if (item.IsChecked) SelectionVM.AddItem(item);
			else SelectionVM.RemoveItem(item);
		}
	}

	// --- NAVEGAÇÃO E UTILS ---

	[RelayCommand]
	private async Task AnalyzeItemDepthAsync(FileSystemItem item)
	{
		// Lógica de aprofundamento (Drill-down)
		if (item == null || string.IsNullOrEmpty(item.FullPath)) return;
		IsLoading = true;
		try
		{
			_historyStack.Push(ContextTreeItems.ToList());
			UpdateCanGoBack();
			item.Children.Clear();
			await PopulateNodeAsync(item, item.FullPath);
			RegisterItemEventsRecursively(item);
		}
		finally { IsLoading = false; }
	}

	[RelayCommand] private void ExpandAll() { foreach (var item in ContextTreeItems) item.SetExpansionRecursively(true); }
	[RelayCommand] private void CollapseAll() { foreach (var item in ContextTreeItems) item.SetExpansionRecursively(false); }
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
		SelectionVM.Clear();
		GitModifiedItems.Clear();
		_historyStack.Clear();
		UpdateCanGoBack();
		SelectedItem = null;
	}

	// --- COPIAR (Lê do SelectionVM) ---
	[RelayCommand]
	private async Task CopyContextToClipboardAsync()
	{
		IsLoading = true;
		try
		{
			// Agora lê da lista do sub-viewModel
			var itemsToCopy = SelectionVM.SelectedItemsList;

			if (!itemsToCopy.Any()) return;

			var sb = new StringBuilder();
			sb.AppendLine("/* CONTEXTO SELECIONADO */");
			sb.AppendLine();

			foreach (var item in itemsToCopy.OrderBy(x => x.FullPath))
			{
				if (string.IsNullOrEmpty(item.FullPath)) continue;
				try
				{
					// Lógica básica de cópia (ou use CodeCleanupHelper se desejar)
					var content = await _fileSystemService.ReadFileContentAsync(item.FullPath);
					sb.AppendLine($"// Arquivo: {Path.GetFileName(item.FullPath)}");
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

	[RelayCommand] // Comando vazio para binding do botão de fluxo (métodos)
	private void AnalyzeMethodFlow(FileSystemItem item) { /* TODO: Implementar */ }

	[RelayCommand]
	private void SyncFocus() { /* TODO: Implementar sync visual */ }

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
}