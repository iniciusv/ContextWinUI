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
	// Dependência do Manager (Dados)
	private readonly IProjectSessionManager _sessionManager;

	// Dependência do Serviço de UI (Tags) - Exposto para a View usar
	public ITagManagementUiService TagService { get; }

	// Item com foco visual (azul)
	private FileSystemItem? _selectedItem;

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> rootItems = new();

	[ObservableProperty]
	private string currentPath = string.Empty;

	[ObservableProperty]
	private bool isLoading;

	public event EventHandler<FileSystemItem>? FileSelected;
	public event EventHandler<string>? StatusChanged;

	// CONSTRUTOR ATUALIZADO
	public FileExplorerViewModel(
		IProjectSessionManager sessionManager,
		ITagManagementUiService tagService)
	{
		_sessionManager = sessionManager;
		TagService = tagService;

		// INSCRIÇÃO: Quando o Manager terminar de carregar tudo
		_sessionManager.ProjectLoaded += OnProjectLoaded;
	}

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		// Atualiza a UI com a árvore já montada e enriquecida
		RootItems = e.RootItems;
		CurrentPath = e.RootPath;
	}

	[RelayCommand]
	private async Task BrowseFolderAsync()
	{
		try
		{
			var folderPicker = new Windows.Storage.Pickers.FolderPicker();

			// Configuração para WinUI 3 (Handle da Janela)
			if (App.MainWindow != null)
			{
				var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
				WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
			}

			folderPicker.FileTypeFilter.Add("*");

			var folder = await folderPicker.PickSingleFolderAsync();

			if (folder != null)
			{
				await _sessionManager.OpenProjectAsync(folder.Path);
			}
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao selecionar pasta: {ex.Message}");
		}
	}

	// --- COMANDOS VISUAIS (Busca, Expansão, Foco) ---

	[RelayCommand]
	private void Search(string query)
	{
		TreeSearchHelper.Search(RootItems, query);
	}

	[RelayCommand]
	private void ExpandAll()
	{
		if (RootItems == null) return;
		foreach (var item in RootItems) item.SetExpansionRecursively(true);
	}

	[RelayCommand]
	private void CollapseAll()
	{
		if (RootItems == null) return;
		foreach (var item in RootItems) item.SetExpansionRecursively(false);
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
}