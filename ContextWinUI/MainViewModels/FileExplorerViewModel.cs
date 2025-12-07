using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Helpers; // Usa o seu Helper de busca visual
using ContextWinUI.Models;
using ContextWinUI.Services;
using System;
using System.Collections.ObjectModel;
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

	// ---------------------------------------------------------
	// BUSCA SIMPLES EM MEMÓRIA (IGUAL À TELA DE CONTEXTO)
	// ---------------------------------------------------------
	[RelayCommand]
	private void Search(string query)
	{
		// Usa o TreeSearchHelper que já implementamos
		// Como a árvore já está toda carregada, ele vai filtrar e expandir automaticamente
		TreeSearchHelper.Search(RootItems, query);
	}

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
		OnStatusChanged("Indexando projeto completo..."); // Feedback importante

		try
		{
			CurrentPath = path;

			// Chama o novo método recursivo
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

	// O comando de expandir agora é puramente visual (o TreeView faz sozinho),
	// mas mantemos caso você queira logar algo ou forçar comportamento futuro.
	// Se quiser, pode até remover este comando do XAML, pois o TreeView expande se tiver Children.
	[RelayCommand]
	private void ExpandItem(FileSystemItem item)
	{
		// Não faz nada de IO, apenas UI binding
		item.IsExpanded = true;
	}

	public void SelectFile(FileSystemItem item)
	{
		if (item.IsCodeFile)
		{
			FileSelected?.Invoke(this, item);
		}
	}

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);

	// Helper para contar total (recursivo)
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