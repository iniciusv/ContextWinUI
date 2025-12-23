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
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class FileExplorerViewModel : ObservableObject
{
	private readonly IProjectSessionManager _sessionManager;
	private readonly IFileSystemService _fileSystemService;
	private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
	private CancellationTokenSource? _searchCts;
	private FileSystemItem? _selectedItem;

	public ITagManagementUiService TagService { get; }

	public ContextSelectionViewModel SelectionViewModel { get; }

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> rootItems = new();

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private string currentPath = "Nenhum projeto carregado";

	public event EventHandler<string>? StatusChanged;
	public event EventHandler<FileSystemItem>? FileSelected;

	public FileExplorerViewModel(
		IProjectSessionManager sessionManager,
		ITagManagementUiService tagService,
		IFileSystemService fileSystemService,
		ContextSelectionViewModel sharedSelectionViewModel) 
	{
		_sessionManager = sessionManager;
		TagService = tagService;
		_fileSystemService = fileSystemService;

		SelectionViewModel = sharedSelectionViewModel;

		_sessionManager.ProjectLoaded += OnProjectLoaded;
		_sessionManager.StatusChanged += (s, msg) => OnStatusChanged(msg);
	}

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		RootItems = e.RootItems;
		CurrentPath = e.RootPath;
		IsLoading = false;

		// Limpa a seleção anterior ao carregar novo projeto
		SelectionViewModel.Clear();

		// Registra eventos de clique para cada item da árvore
		foreach (var item in RootItems)
		{
			RegisterItemEvents(item);

			// Se o item já vier marcado (do cache), avisa o SelectionViewModel
			if (item.IsChecked) SelectionViewModel.AddItem(item);
		}

		OnStatusChanged("Projeto carregado com sucesso.");
	}
	private void RegisterItemEvents(FileSystemItem item)
	{
		item.PropertyChanged -= OnItemPropertyChanged;
		item.PropertyChanged += OnItemPropertyChanged;

		if (item.Children != null)
		{
			foreach (var child in item.Children)
			{
				RegisterItemEvents(child);
			}
		}
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(FileSystemItem.IsChecked) && sender is FileSystemItem item)
		{
			if (item.IsChecked)
				SelectionViewModel.AddItem(item);
			else
				SelectionViewModel.RemoveItem(item);
		}
	}

	public void SelectFile(FileSystemItem item)
	{
		_selectedItem = item;
		FileSelected?.Invoke(this, item);
	}

	[RelayCommand]
	private void SyncFocus()
	{
		if (RootItems == null || !RootItems.Any()) return;

		if (_selectedItem == null)
		{
			CollapseAll();
			return;
		}

		foreach (var item in RootItems)
		{
			DetermineExpansionState(item, _selectedItem);
		}
	}

	private bool DetermineExpansionState(FileSystemItem current, FileSystemItem target)
	{
		if (current == target)
		{
			return true;
		}

		bool containsTarget = false;
		if (current.Children != null && current.Children.Any())
		{
			foreach (var child in current.Children)
			{
				if (DetermineExpansionState(child, target))
				{
					containsTarget = true;
				}
			}
		}

		if (current.IsDirectory)
		{
			current.IsExpanded = containsTarget;
		}

		return containsTarget;
	}

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
		// Nota: O IsLoading = false é feito no evento OnProjectLoaded ou em caso de erro deve ser tratado aqui se o evento não disparar.
		// Como LoadProjectAsync do SessionManager dispara eventos, garantimos o finally no SessionManager ou aqui se der erro síncrono.
		if (!IsLoading && string.IsNullOrEmpty(CurrentPath)) IsLoading = false;
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



	// [CORREÇÃO] Comandos de Seleção em Massa
	[RelayCommand]
	private void SelectAll()
	{
		if (RootItems == null) return;
		SetCheckedRecursive(RootItems, true);
	}

	[RelayCommand]
	private void UnselectAll()
	{
		if (RootItems == null) return;
		SetCheckedRecursive(RootItems, false);
	}

	// Helper recursivo para marcar/desmarcar
	private void SetCheckedRecursive(IEnumerable<FileSystemItem> items, bool isChecked)
	{
		foreach (var item in items)
		{
			// Só marcamos se for arquivo de código (pastas não entram na seleção final)
			if (item.IsCodeFile)
			{
				item.IsChecked = isChecked;
				// A mágica reativa que criamos antes (OnItemPropertyChanged) 
				// vai rodar automaticamente aqui e atualizar a SelectionViewModel
			}

			if (item.Children != null && item.Children.Any())
			{
				SetCheckedRecursive(item.Children, isChecked);
			}
		}
	}
}
