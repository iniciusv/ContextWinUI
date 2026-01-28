// ARQUIVO: FileExplorerViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.ContextBuilder;
using ContextWinUI.Features.FileSystem;
using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class FileExplorerViewModel : ObservableObject, IDisposable
{
	// Serviços Injetados
	public IProjectSessionManager SessionManager;
	private readonly IFileExplorerDataService _dataService;
	private readonly IFileExplorerSearchService _searchService;
	private readonly IFileExplorerTreeService _treeService;
	private readonly IFileExplorerOperationsService _operationsService;
	private readonly IFileSelectionService _selectionService;

	private FileSystemItem? _selectedItem;

	[ObservableProperty]
	private bool isLoading;

	// Propriedades Expostas (Proxy para o DataService)
	public ObservableCollection<FileSystemItem> RootItems => _dataService.RootItems;
	public string CurrentPath => _dataService.CurrentPath;
	public ObservableCollection<string> AllProjectTags => _dataService.AllProjectTags;

	// ViewModels e Serviços auxiliares de UI
	public ITagManagementUiService TagService { get; }
	public ContextSelectionViewModel SelectionViewModel { get; }

	// Eventos
	public event EventHandler<string>? StatusChanged;
	public event EventHandler<FileSystemItem>? FileSelected;

	public FileExplorerViewModel(
		IProjectSessionManager sessionManager,
		ITagManagementUiService tagService,
		IFileExplorerDataService dataService,
		IFileExplorerSearchService searchService,
		IFileExplorerTreeService treeService,
		IFileExplorerOperationsService operationsService,
		IFileSelectionService selectionService,
		ContextSelectionViewModel sharedSelectionViewModel)
	{
		SessionManager = sessionManager;
		TagService = tagService;
		_dataService = dataService;
		_searchService = searchService;
		_treeService = treeService;
		_operationsService = operationsService;
		_selectionService = selectionService;
		SelectionViewModel = sharedSelectionViewModel;

		// Configuração de Eventos
		SessionManager.ProjectLoaded += OnProjectLoaded;
		SessionManager.StatusChanged += (s, msg) => OnStatusChanged(msg);
		_dataService.StatusChanged += (s, msg) => OnStatusChanged(msg);
	}

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		IsLoading = false;
		_dataService.Initialize(e.RootItems, e.RootPath);

		// Notificar UI que as propriedades mudaram (já que RootItems mudou dentro do serviço)
		OnPropertyChanged(nameof(RootItems));
		OnPropertyChanged(nameof(CurrentPath));

		OnStatusChanged("Projeto carregado com sucesso.");
	}

	// --- Comandos de Árvore ---

	[RelayCommand]
	private void ExpandAll() => _treeService.ExpandAll(RootItems);

	[RelayCommand]
	private void CollapseAll() => _treeService.CollapseAll(RootItems);

	[RelayCommand]
	private void SyncFocus() => _treeService.SyncFocus(RootItems, _selectedItem);

	[RelayCommand]
	private void ExpandItem(FileSystemItem item)
	{
		if (item != null) item.IsExpanded = true;
	}

	// --- Comandos de Operações ---

	[RelayCommand]
	private async Task CreateNewItemAsync(object[] args)
	{
		if (args.Length < 3 || args[0] is not FileSystemItem target || args[2] is not XamlRoot root) return;
		await _operationsService.CreateNewItemAsync(target, (bool)args[1], root);
	}

	[RelayCommand]
	private async Task DeleteItemAsync(object[] args)
	{
		if (args.Length < 2 || args[0] is not FileSystemItem item || args[1] is not XamlRoot root) return;
		await _operationsService.DeleteItemAsync(item, RootItems, root);
	}

	// --- Comandos de Busca ---

	[RelayCommand]
	private async Task SearchAsync(string query)
	{
		await _searchService.PerformSearchAsync(RootItems, query, OnStatusChanged);
	}

	[RelayCommand]
	private async Task SubmitSearch(string query)
	{
		if (RootItems == null) return;
		IsLoading = true;
		try
		{
			var message = await _searchService.SubmitSearchAsync(RootItems, query);
			if (!string.IsNullOrEmpty(message))
			{
				OnStatusChanged(message);
			}
		}
		finally
		{
			IsLoading = false;
		}
	}

	// --- Comandos de Seleção e Carregamento ---

	[RelayCommand]
	private void SelectAll() => _dataService.SetSelectionState(RootItems, true);

	[RelayCommand]
	private void UnselectAll() => _dataService.SetSelectionState(RootItems, false);

	[RelayCommand]
	private async Task BrowseFolderAsync()
	{
		if (IsLoading) return;
		try
		{
			IsLoading = true;
			await _dataService.LoadProjectAsync();
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

	public void SelectFile(FileSystemItem item)
	{
		_selectedItem = item;

		// 1. Avisa o serviço global (Isso fará o FileContentViewModel carregar o arquivo)
		_selectionService.SetSelection(item);

		// 2. Mantém o evento local caso a MainWindow use
		FileSelected?.Invoke(this, item);
	}

    public void SelectFileByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        IsLoading = true;
        try
        {
            var item = _treeService.FindItemByPath(RootItems, path);
            if (item != null)
            {
                SelectFile(item);
                _treeService.SyncFocus(RootItems, item);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);

	public async Task RefreshTreeAsync()
	{
		if (string.IsNullOrEmpty(CurrentPath) || SessionManager == null) return;

		try
		{
			IsLoading = true;
			OnStatusChanged("Sincronizando arquivos (mantendo estado)...");

            // 1. CAPTURA O ESTADO ATUAL
            // Captura seleção (incluindo blocos)
            var selectionSnapshot = SelectionViewModel.GetCurrentSelectionSnapshot();

            // Captura expansão de pastas
            var expandedPaths = new System.Collections.Generic.HashSet<string>();
            CollectExpandedPaths(RootItems, expandedPaths);

			// 2. REFRESH SEM LIMPAR O CACHE
			// Solicita ao SessionManager que recarregue os arquivos do disco, 
            // mas NÃO limpe as tags/cores da memória.
			await SessionManager.RefreshProjectAsync(CurrentPath);
            
            // 3. RESTAURA O ESTADO
            // Restaura expansão
            if (RootItems != null)
            {
                RestoreExpandedPaths(RootItems, expandedPaths);
            }

            // Restaura seleção
            if (selectionSnapshot != null && selectionSnapshot.Any())
            {
                SelectionViewModel.ProcessPaths(selectionSnapshot);
            }

            OnStatusChanged("Sincronização concluída.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao atualizar árvore: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

    private void CollectExpandedPaths(System.Collections.Generic.IEnumerable<FileSystemItem> items, System.Collections.Generic.HashSet<string> expanded)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            if (item.IsExpanded) expanded.Add(item.FullPath);
            // Recursão
            CollectExpandedPaths(item.Children, expanded);
        }
    }

    private void RestoreExpandedPaths(System.Collections.Generic.IEnumerable<FileSystemItem> items, System.Collections.Generic.HashSet<string> expanded)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            // Se estava expandido antes, re-expande
            if (expanded.Contains(item.FullPath))
            {
                item.IsExpanded = true;
            }
            // Recursão
            RestoreExpandedPaths(item.Children, expanded);
        }
    }
	public void Dispose()
	{
		SessionManager.ProjectLoaded -= OnProjectLoaded;
		_dataService.Dispose();
		_searchService.Dispose();
	}
}