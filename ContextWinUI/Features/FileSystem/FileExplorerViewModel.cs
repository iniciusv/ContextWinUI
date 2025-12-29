using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.ContextBuilder;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using ContextWinUI.Services;
using ContextWinUI.ViewModels.Helpers; // Namespace dos novos helpers
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class FileExplorerViewModel : ObservableObject, IDisposable
{
	// Services
	public readonly IProjectSessionManager _sessionManager;
	private readonly IFileSystemService _fileSystemService;
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

	// Helpers (Composição)
	private readonly FileExplorerTreeManager _treeManager;
	private readonly FileExplorerOperations _operationsManager;

	// State
	private CancellationTokenSource? _searchCts;
	private FileSystemItem? _selectedItem;

	// Public Properties
	public ITagManagementUiService TagService { get; }
	public ContextSelectionViewModel SelectionViewModel { get; }

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> rootItems = new();

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private string currentPath = "Nenhum projeto carregado";

	[ObservableProperty]
	private ObservableCollection<string> allProjectTags = new();

	// Events
	public event EventHandler<string>? StatusChanged;
	public event EventHandler<FileSystemItem>? FileSelected;

	public FileExplorerViewModel(
		IProjectSessionManager sessionManager,
		ITagManagementUiService tagService,
		IFileSystemService fileSystemService,
		ContextSelectionViewModel sharedSelectionViewModel,
		IFileSystemItemFactory itemFactory)
	{
		_sessionManager = sessionManager;
		TagService = tagService;
		_fileSystemService = fileSystemService;
		SelectionViewModel = sharedSelectionViewModel;
		_itemFactory = itemFactory;

		// Inicializa Helpers
		_treeManager = new FileExplorerTreeManager();
		_operationsManager = new FileExplorerOperations(
			fileSystemService,
			itemFactory,
			msg => OnStatusChanged(msg),
			item => RegisterItemEvents(item) // Callback para registrar eventos em novos itens
		);

		_sessionManager.ProjectLoaded += OnProjectLoaded;
		_sessionManager.StatusChanged += (s, msg) => OnStatusChanged(msg);
	}

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		// Limpeza de eventos anteriores para evitar memory leaks
		UnregisterEventsRecursively(RootItems);

		RootItems = e.RootItems;
		CurrentPath = e.RootPath;
		IsLoading = false;

		SelectionViewModel.Clear();

		// Otimização: Fazer registro em background se a árvore for muito grande, 
		// mas eventos de UI geralmente precisam ser na thread principal.
		foreach (var item in RootItems)
		{
			RegisterItemEvents(item);
			if (item.IsChecked) SelectionViewModel.AddItem(item);
		}

		RefreshAllTags();
		OnStatusChanged("Projeto carregado com sucesso.");
	}

	// --- Gerenciamento de Eventos (Ponto Crítico de Performance) ---
	private void RegisterItemEvents(FileSystemItem item)
	{
		// Remove antes de adicionar para garantir que não haja duplicatas
		item.PropertyChanged -= OnItemPropertyChanged;
		item.PropertyChanged += OnItemPropertyChanged;

		if (item.SharedState != null)
		{
			item.SharedState.Tags.CollectionChanged -= OnItemTagsChanged;
			item.SharedState.Tags.CollectionChanged += OnItemTagsChanged;
		}

		if (item.Children != null)
		{
			foreach (var child in item.Children)
			{
				RegisterItemEvents(child);
			}
		}
	}

	private void UnregisterEventsRecursively(IEnumerable<FileSystemItem> items)
	{
		if (items == null) return;
		foreach (var item in items)
		{
			item.PropertyChanged -= OnItemPropertyChanged;
			if (item.SharedState != null)
				item.SharedState.Tags.CollectionChanged -= OnItemTagsChanged;

			if (item.Children != null)
				UnregisterEventsRecursively(item.Children);
		}
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// Performance: Filtro rápido
		if (e.PropertyName != nameof(FileSystemItem.IsChecked)) return;

		if (sender is FileSystemItem item)
		{
			if (item.IsChecked) SelectionViewModel.AddItem(item);
			else SelectionViewModel.RemoveItem(item);
		}
	}

	private void OnItemTagsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.NewItems == null) return;

		// Uso do Dispatcher apenas se necessário
		_dispatcherQueue.TryEnqueue(() =>
		{
			foreach (string newTag in e.NewItems)
			{
				if (!AllProjectTags.Contains(newTag)) AllProjectTags.Add(newTag);
			}
		});
	}

	// --- Comandos Delegados ---

	[RelayCommand]
	private void ExpandAll() => _treeManager.ExpandAll(RootItems);

	[RelayCommand]
	private void CollapseAll() => _treeManager.CollapseAll(RootItems);

	[RelayCommand]
	private void SyncFocus() => _treeManager.SyncFocus(RootItems, _selectedItem);

	[RelayCommand]
	private void ExpandItem(FileSystemItem item)
	{
		if (item != null) item.IsExpanded = true;
	}

	[RelayCommand]
	private async Task CreateNewItemAsync(object[] args)
	{
		if (args.Length < 3 || args[0] is not FileSystemItem target || args[2] is not XamlRoot root) return;
		await _operationsManager.CreateNewItemAsync(target, (bool)args[1], root);
	}

	[RelayCommand]
	private async Task DeleteItemAsync(object[] args)
	{
		if (args.Length < 2 || args[0] is not FileSystemItem item || args[1] is not XamlRoot root) return;

		// Passamos RootItems para que o Manager possa encontrar o pai e remover visualmente
		await _operationsManager.DeleteItemAsync(item, RootItems, root);
	}

	// --- Search Logic (Mantida aqui pois orquestra UI + TreeSearchHelper) ---

	[RelayCommand]
	private async Task SearchAsync(string query)
	{
		if (_searchCts != null)
		{
			_searchCts.Cancel();
			_searchCts.Dispose();
		}

		_searchCts = new CancellationTokenSource();
		var token = _searchCts.Token;

		try
		{
			await Task.Delay(300, token); // Debounce reduzido para 300ms (500ms é muito lento para UX)
			if (!token.IsCancellationRequested && RootItems != null)
			{
				// Chamada ao Helper Otimizado que você já criou
				await TreeSearchHelper.SearchAsync(RootItems, query, token, _dispatcherQueue);
			}
		}
		catch (TaskCanceledException) { }
		catch (Exception ex)
		{
			OnStatusChanged($"Erro busca: {ex.Message}");
		}
	}

	[RelayCommand]
	private async Task SubmitSearch(string query)
	{
		if (RootItems == null) return;
		IsLoading = true;
		try
		{
			var (success, itemsToChange, tag, isSelection) = await TreeSearchHelper.TryExecuteCommandAsync(RootItems, query);
			if (success)
			{
				// Batch update para evitar travamentos em seleções massivas
				foreach (var item in itemsToChange)
				{
					item.IsChecked = isSelection;
				}

				string action = isSelection ? "selecionados" : "desselecionados";
				OnStatusChanged(itemsToChange.Count > 0
					? $"{itemsToChange.Count} itens com a tag '{tag}' foram {action}."
					: $"Nenhum item encontrado com a tag '{tag}' precisou ser alterado.");
			}
		}
		finally
		{
			IsLoading = false;
		}
	}

	// --- Seleção em Lote ---

	[RelayCommand]
	private void SelectAll() => SetCheckedRecursive(RootItems, true);

	[RelayCommand]
	private void UnselectAll() => SetCheckedRecursive(RootItems, false);

	private void SetCheckedRecursive(IEnumerable<FileSystemItem> items, bool isChecked)
	{
		// Otimização: Iteração direta
		if (items == null) return;

		foreach (var item in items)
		{
			if (item.IsCodeFile) item.IsChecked = isChecked;

			// Só desce se tiver filhos carregados para economizar processamento
			if (item.Children != null && item.Children.Count > 0)
			{
				SetCheckedRecursive(item.Children, isChecked);
			}
		}
	}

	// --- Outros ---

	public void SelectFile(FileSystemItem item)
	{
		_selectedItem = item;
		FileSelected?.Invoke(this, item);
	}

	[RelayCommand]
	private async Task BrowseFolderAsync()
	{
		if (IsLoading) return;
		try
		{
			IsLoading = true;
			await _sessionManager.LoadProjectAsync();
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro: {ex.Message}");
		}
		finally
		{
			if (string.IsNullOrEmpty(CurrentPath)) IsLoading = false;
		}
	}

	public void RefreshAllTags()
	{
		if (_itemFactory == null) return;

		// Otimização: HashSet para lookup instantâneo ao invés de Distinct().OrderBy() toda vez se não precisar
		// Mas para UI, manter ordenado é bom.
		var uniqueTags = _itemFactory.GetAllStates()
			.SelectMany(s => s.Tags)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(t => t);

		AllProjectTags.Clear();
		foreach (var tag in uniqueTags)
		{
			AllProjectTags.Add(tag);
		}
	}

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);

	public void Dispose()
	{
		UnregisterEventsRecursively(RootItems);
		_sessionManager.ProjectLoaded -= OnProjectLoaded;
		if (_searchCts != null) _searchCts.Dispose();

		// Limpar referências grandes
		RootItems.Clear();
	}
}