using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class FileExplorerViewModel : ObservableObject
{
	private readonly IProjectSessionManager _sessionManager;
	private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
	private CancellationTokenSource? _searchCts;

	// Armazena o item que está sendo exibido atualmente
	private FileSystemItem? _selectedItem;

	public ITagManagementUiService TagService { get; }

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> rootItems = new();

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private string currentPath = "Nenhum projeto carregado";

	public event EventHandler<string>? StatusChanged;
	public event EventHandler<FileSystemItem>? FileSelected;

	public FileExplorerViewModel(IProjectSessionManager sessionManager, ITagManagementUiService tagService)
	{
		_sessionManager = sessionManager;
		TagService = tagService;

		_sessionManager.ProjectLoaded += OnProjectLoaded;
		_sessionManager.StatusChanged += (s, msg) => OnStatusChanged(msg);
	}

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		RootItems = e.RootItems;
		CurrentPath = e.RootPath;
		IsLoading = false;
		OnStatusChanged("Projeto carregado com sucesso.");
	}

	// Chamado quando o usuário clica num arquivo na árvore (via MainWindow/View)
	public void SelectFile(FileSystemItem item)
	{
		_selectedItem = item; // Guarda a referência para o SyncFocus usar depois
		FileSelected?.Invoke(this, item);
	}

	// --- LÓGICA DE FOCAR NO ITEM ATUAL ---

	[RelayCommand]
	private void SyncFocus()
	{
		// Se não tem nada carregado ou nenhum arquivo selecionado, fecha tudo.
		if (RootItems == null || !RootItems.Any()) return;

		if (_selectedItem == null)
		{
			CollapseAll();
			return;
		}

		// Algoritmo: Percorre a árvore recursivamente.
		// Se o nó faz parte do caminho até _selectedItem, ele se expande.
		// Se não faz parte, ele se fecha.
		foreach (var item in RootItems)
		{
			DetermineExpansionState(item, _selectedItem);
		}
	}

	/// <summary>
	/// Retorna TRUE se este item (ou um descendente) for o alvo.
	/// Define IsExpanded baseado nisso.
	/// </summary>
	private bool DetermineExpansionState(FileSystemItem current, FileSystemItem target)
	{
		// 1. É o próprio arquivo alvo?
		if (current == target)
		{
			// Arquivos não expandem, mas retornamos true para avisar o pai para ficar aberto
			return true;
		}

		// 2. Verifica os filhos recursivamente
		bool containsTarget = false;
		if (current.Children != null && current.Children.Any())
		{
			foreach (var child in current.Children)
			{
				// Se ALGUM filho retornar true, este item (current) precisa ficar aberto
				if (DetermineExpansionState(child, target))
				{
					containsTarget = true;
				}
			}
		}

		// 3. Aplica o estado visual (apenas se for diretório)
		if (current.IsDirectory)
		{
			current.IsExpanded = containsTarget;
		}

		return containsTarget;
	}

	// --- OUTROS COMANDOS DE VISUALIZAÇÃO ---

	[RelayCommand]
	private void ExpandAll()
	{
		if (RootItems == null) return;
		SetExpansionRecursive(RootItems, true);
	}

	[RelayCommand]
	private void CollapseAll()
	{
		if (RootItems == null) return;
		SetExpansionRecursive(RootItems, false);
	}

	private void SetExpansionRecursive(IEnumerable<FileSystemItem> items, bool isExpanded)
	{
		foreach (var item in items)
		{
			if (item.IsDirectory)
			{
				item.IsExpanded = isExpanded;
				if (item.Children != null && item.Children.Any())
				{
					SetExpansionRecursive(item.Children, isExpanded);
				}
			}
		}
	}

	// --- NAVEGAÇÃO / BUSCA ---

	[RelayCommand]
	private async Task BrowseFolderAsync()
	{
		if (IsLoading) return;
		try
		{
			IsLoading = true;
			await _sessionManager.LoadProjectAsync();
		}
		catch (Exception ex) { OnStatusChanged($"Erro: {ex.Message}"); }
		finally { IsLoading = false; }
	}

	[RelayCommand]
	private async Task SearchAsync(string query)
	{
		if (_searchCts != null) { _searchCts.Cancel(); _searchCts.Dispose(); }
		_searchCts = new CancellationTokenSource();
		var token = _searchCts.Token;

		try
		{
			await Task.Delay(300, token); // Debounce
			if (!token.IsCancellationRequested && RootItems != null)
			{
				await TreeSearchHelper.SearchAsync(RootItems, query, token, _dispatcherQueue);
			}
		}
		catch (TaskCanceledException) { }
		catch (Exception ex) { OnStatusChanged($"Erro busca: {ex.Message}"); }
	}

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);

	[RelayCommand]
	private void ExpandItem(FileSystemItem item)
	{
		if (item != null)
		{
			item.IsExpanded = true;
		}
	}
}