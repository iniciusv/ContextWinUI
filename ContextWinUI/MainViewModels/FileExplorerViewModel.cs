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
	private readonly FileSystemItemFactory _itemFactory;

	// Campo para armazenar o item atualmente clicado/selecionado (foco visual)
	private FileSystemItem? _selectedItem;

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> rootItems = new();

	[ObservableProperty]
	private string currentPath = string.Empty;

	[ObservableProperty]
	private bool isLoading;

	public event EventHandler<FileSystemItem>? FileSelected;
	public event EventHandler<string>? StatusChanged;

	public FileExplorerViewModel(FileSystemService fileSystemService, FileSystemItemFactory itemFactory)
	{
		_fileSystemService = fileSystemService;
		_itemFactory = itemFactory;
	}

	// --- COMANDOS DE BUSCA ---
	[RelayCommand]
	private void Search(string query)
	{
		TreeSearchHelper.Search(RootItems, query);
	}

	// --- COMANDOS DE VISUALIZAÇÃO ---

	[RelayCommand]
	private void ExpandAll()
	{
		if (RootItems == null) return;
		foreach (var item in RootItems)
		{
			item.SetExpansionRecursively(true);
		}
	}

	[RelayCommand]
	private void CollapseAll()
	{
		if (RootItems == null) return;
		foreach (var item in RootItems)
		{
			item.SetExpansionRecursively(false);
		}
	}

	[RelayCommand]
	private void SyncFocus()
	{
		if (_selectedItem == null || RootItems == null) return;

		foreach (var item in RootItems)
		{
			SyncFocusRecursive(item, _selectedItem);
		}
	}

	private bool SyncFocusRecursive(FileSystemItem currentItem, FileSystemItem targetItem)
	{
		// Comparação segura por Path via Flyweight
		if (currentItem.FullPath == targetItem.FullPath)
		{
			if (currentItem.IsDirectory) currentItem.IsExpanded = true;
			return true;
		}

		bool keepExpanded = false;
		foreach (var child in currentItem.Children)
		{
			if (SyncFocusRecursive(child, targetItem))
			{
				keepExpanded = true;
			}
		}

		currentItem.IsExpanded = keepExpanded;
		return keepExpanded;
	}

	// --- CARREGAMENTO DE ARQUIVOS ---

	[RelayCommand]
	private async Task BrowseFolderAsync()
	{
		try
		{
			var folderPicker = new Windows.Storage.Pickers.FolderPicker();

			// Gambiarra necessária para WinUI 3 Window Handle
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

		// Opcional: Limpar cache da factory ao carregar novo projeto?
		// _itemFactory.ClearCache(); 

		try
		{
			CurrentPath = path;

			// O serviço agora usa a Factory internamente, retornando Wrappers
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