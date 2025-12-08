// ==================== C:\Users\vinic\source\repos\ContextWinUI\ContextWinUI\MainViewModels\FileExplorerViewModel.cs ====================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class FileExplorerViewModel : ObservableObject
{
	private readonly FileSystemService _fileSystemService;

	// Campo para armazenar o item atualmente clicado/selecionado
	private FileSystemItem? _selectedItem;

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> rootItems = new();

	[ObservableProperty]
	private string currentPath = string.Empty;

	[ObservableProperty]
	private bool isLoading;

	public event EventHandler<FileSystemItem>? FileSelected;
	public event EventHandler<string>? StatusChanged;

	public FileExplorerViewModel(FileSystemService fileSystemService)
	{
		_fileSystemService = fileSystemService;
	}

	// --- COMANDOS DE BUSCA ---
	[RelayCommand]
	private void Search(string query)
	{
		TreeSearchHelper.Search(RootItems, query);
	}

	// --- COMANDOS DE EXPANSÃO / VISUALIZAÇÃO ---

	// 1. Expandir Tudo
	[RelayCommand]
	private void ExpandAll()
	{
		if (RootItems == null) return;
		foreach (var item in RootItems)
		{
			item.SetExpansionRecursively(true);
		}
	}

	// 2. Recolher Tudo
	[RelayCommand]
	private void CollapseAll()
	{
		if (RootItems == null) return;
		foreach (var item in RootItems)
		{
			item.SetExpansionRecursively(false);
		}
	}

	// 3. Focar no Item (Sync) - Novo comando que faltava
	[RelayCommand]
	private void SyncFocus()
	{
		// Se não houver item selecionado ou árvore vazia, ignora
		if (_selectedItem == null || RootItems == null) return;

		foreach (var item in RootItems)
		{
			// A função retorna true se o _selectedItem estiver dentro deste ramo
			SyncFocusRecursive(item, _selectedItem);
		}
	}

	// Lógica recursiva para o SyncFocus
	private bool SyncFocusRecursive(FileSystemItem currentItem, FileSystemItem targetItem)
	{
		// Caso base: Encontramos o alvo
		if (currentItem == targetItem)
		{
			// Opcional: Se o alvo for uma pasta, expande ela também
			if (currentItem.IsDirectory) currentItem.IsExpanded = true;
			return true;
		}

		bool keepExpanded = false;

		// Verifica filhos
		foreach (var child in currentItem.Children)
		{
			if (SyncFocusRecursive(child, targetItem))
			{
				keepExpanded = true;
			}
		}

		// Se keepExpanded for true, expande este nó para mostrar o caminho.
		// Se for false, fecha para limpar a visão.
		currentItem.IsExpanded = keepExpanded;

		return keepExpanded;
	}

	// ---------------------------------------------------------------------

	[RelayCommand]
	private async Task BrowseFolderAsync()
	{
		try
		{
			var folderPicker = new Windows.Storage.Pickers.FolderPicker();
			if (App.MainWindow != null)
			{
				var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
				WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
			}
			folderPicker.FileTypeFilter.Add("*");

			var folder = await folderPicker.PickSingleFolderAsync();

			if (folder != null)
			{
				await LoadProjectAsync(folder.Path);
			}
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao selecionar pasta: {ex.Message}");
		}
	}

	public async Task LoadProjectAsync(string path)
	{
		IsLoading = true;
		OnStatusChanged("Indexando projeto completo...");

		try
		{
			CurrentPath = path;
			// Carrega a árvore recursivamente
			RootItems = await _fileSystemService.LoadProjectRecursivelyAsync(path);
			OnStatusChanged($"Projeto carregado. {CountTotalItems(RootItems)} itens indexados.");
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

	// Comando auxiliar usado pelo evento Expanding do TreeView
	[RelayCommand]
	private void ExpandItem(FileSystemItem item)
	{
		item.IsExpanded = true;
	}

	public void SelectFile(FileSystemItem item)
	{
		// Armazena o item para uso no SyncFocus
		_selectedItem = item;

		if (item.IsCodeFile)
		{
			FileSelected?.Invoke(this, item);
		}
	}

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);

	private int CountTotalItems(ObservableCollection<FileSystemItem> items)
	{
		int count = 0;
		foreach (var item in items)
		{
			count++;
			if (item.Children.Count > 0)
				count += CountTotalItems(item.Children);
		}
		return count;
	}
}