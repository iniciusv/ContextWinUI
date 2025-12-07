using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
	private readonly FileSystemService _fileSystemService;

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> rootItems = new();

	[ObservableProperty]
	private FileSystemItem? selectedItem;

	[ObservableProperty]
	private string fileContent = string.Empty;

	[ObservableProperty]
	private string currentPath = string.Empty;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private string statusMessage = "Selecione uma pasta para começar";

	public MainViewModel()
	{
		_fileSystemService = new FileSystemService();
	}

	[RelayCommand]
	private async Task BrowseFolderAsync()
	{
		try
		{
			var folderPicker = new Windows.Storage.Pickers.FolderPicker();

			// Verificar se MainWindow não é null
			if (App.MainWindow == null)
			{
				StatusMessage = "Erro: Janela principal não encontrada";
				return;
			}

			// Obter o handle da janela para WinUI 3
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
			StatusMessage = $"Erro ao selecionar pasta: {ex.Message}";
		}
	}

	private async Task LoadDirectoryAsync(string path)
	{
		IsLoading = true;
		StatusMessage = "Carregando...";

		try
		{
			CurrentPath = path;
			RootItems = await _fileSystemService.LoadDirectoryAsync(path);
			StatusMessage = $"Carregado: {RootItems.Count} itens";
		}
		catch (Exception ex)
		{
			StatusMessage = $"Erro: {ex.Message}";
		}
		finally
		{
			IsLoading = false;
		}
	}

	[RelayCommand]
	private async Task LoadFileContentAsync(FileSystemItem? item)
	{
		if (item == null || !item.IsCodeFile)
		{
			FileContent = string.Empty;
			return;
		}

		SelectedItem = item;
		IsLoading = true;

		try
		{
			FileContent = await _fileSystemService.ReadFileContentAsync(item.FullPath);
			StatusMessage = $"Arquivo: {item.Name} ({item.FileSizeFormatted})";
		}
		catch (Exception ex)
		{
			FileContent = $"Erro ao carregar arquivo: {ex.Message}";
			StatusMessage = "Erro ao carregar arquivo";
		}
		finally
		{
			IsLoading = false;
		}
	}

	[RelayCommand(CanExecute = nameof(CanCopyToClipboard))]
	private void CopyToClipboard()
	{
		if (string.IsNullOrEmpty(FileContent))
			return;

		var dataPackage = new DataPackage();
		dataPackage.SetText(FileContent);
		Clipboard.SetContent(dataPackage);

		StatusMessage = "Conteúdo copiado!";
	}

	private bool CanCopyToClipboard() => !string.IsNullOrEmpty(FileContent);

	partial void OnFileContentChanged(string value)
	{
		CopyToClipboardCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand]
	private async Task ExpandItemAsync(FileSystemItem item)
	{
		if (!item.IsDirectory || item.Children.Any())
			return;

		try
		{
			item.Children = await _fileSystemService.LoadChildrenAsync(item);
		}
		catch (Exception ex)
		{
			StatusMessage = $"Erro ao expandir pasta: {ex.Message}";
		}
	}
}