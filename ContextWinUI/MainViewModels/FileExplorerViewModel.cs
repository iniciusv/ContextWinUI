using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class FileExplorerViewModel : ObservableObject
{
	private readonly FileSystemService _fileSystemService;

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

	[RelayCommand]
	private async Task BrowseFolderAsync()
	{
		try
		{
			var folderPicker = new Windows.Storage.Pickers.FolderPicker();

			if (App.MainWindow == null)
			{
				OnStatusChanged("Erro: Janela principal não encontrada");
				return;
			}

			var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
			WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

			folderPicker.FileTypeFilter.Add("*");

			var folder = await folderPicker.PickSingleFolderAsync();

			if (folder != null)
			{
				await LoadDirectoryAsync(folder.Path);
			}
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao selecionar pasta: {ex.Message}");
		}
	}

	public async Task LoadDirectoryAsync(string path)
	{
		IsLoading = true;
		OnStatusChanged("Carregando...");

		try
		{
			CurrentPath = path;
			RootItems = await _fileSystemService.LoadDirectoryAsync(path);
			OnStatusChanged($"Carregado: {RootItems.Count} itens");
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

	[RelayCommand]
	private async Task ExpandItemAsync(FileSystemItem item)
	{
		if (!item.IsDirectory)
			return;

		if (item.Children.Any())
		{
			item.IsExpanded = true;
			return;
		}

		try
		{
			OnStatusChanged($"Carregando: {item.Name}...");
			var children = await _fileSystemService.LoadChildrenAsync(item);

			item.Children.Clear();
			foreach (var child in children)
			{
				item.Children.Add(child);
			}

			item.IsExpanded = true;
			OnStatusChanged($"Pasta {item.Name}: {children.Count} itens");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao expandir pasta: {ex.Message}");
		}
	}

	public void SelectFile(FileSystemItem item)
	{
		if (item.IsCodeFile)
		{
			FileSelected?.Invoke(this, item);
		}
	}

	private void OnStatusChanged(string message)
	{
		StatusChanged?.Invoke(this, message);
	}
}