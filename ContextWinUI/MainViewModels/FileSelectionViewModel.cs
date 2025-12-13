using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.ViewModels;

public partial class FileSelectionViewModel : ObservableObject
{
	private readonly IFileSystemService _fileSystemService;
	private ObservableCollection<FileSystemItem> _rootItems = new();

	// Propriedade contador. O atributo avisa o botão para verificar se pode ser clicado.
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(CopySelectedFilesCommand))]
	private int selectedFilesCount;

	[ObservableProperty]
	private bool isLoading;

	public event EventHandler<string>? StatusChanged;

	public FileSelectionViewModel(IFileSystemService fileSystemService)
	{
		_fileSystemService = fileSystemService;
	}

	public void SetRootItems(ObservableCollection<FileSystemItem> items)
	{
		_rootItems = items;
		RecalculateSelection();
	}

    public IEnumerable<FileSystemItem> GetCheckedFiles() => GetAllCheckedFiles(_rootItems);

    public void RecalculateSelection() => SelectedFilesCount = GetAllCheckedFiles(_rootItems).Count();

	[RelayCommand]
	private void SelectAll()
	{
		SetAllItemsChecked(_rootItems, true);
		RecalculateSelection();
	}

	[RelayCommand]
	private void UnselectAll()
	{
		SetAllItemsChecked(_rootItems, false);
		RecalculateSelection();
	}

	[RelayCommand(CanExecute = nameof(CanCopySelectedFiles))]
	private async Task CopySelectedFilesAsync()
	{
		var selectedFiles = GetAllCheckedFiles(_rootItems).ToList();

		if (!selectedFiles.Any()) return;

		IsLoading = true;
		OnStatusChanged($"Lendo {selectedFiles.Count} arquivo(s) do disco...");

		try
		{
			var sb = new StringBuilder();

			foreach (var file in selectedFiles)
			{
				sb.AppendLine($"// ==================== {file.FullPath} ====================");
				sb.AppendLine();

				// LÊ DO DISCO AGORA (Garante última versão salva)
				var content = await _fileSystemService.ReadFileContentAsync(file.FullPath);

				sb.AppendLine(content);
				sb.AppendLine();
				sb.AppendLine();
			}

			var dataPackage = new DataPackage();
			dataPackage.SetText(sb.ToString());
			Clipboard.SetContent(dataPackage);

			OnStatusChanged($"{selectedFiles.Count} arquivo(s) copiados!");
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

	private bool CanCopySelectedFiles() => SelectedFilesCount > 0;

	// --- Helpers ---

	private void SetAllItemsChecked(ObservableCollection<FileSystemItem> items, bool isChecked)
	{
		foreach (var item in items)
		{
			if (item.IsCodeFile) item.IsChecked = isChecked;
			if (item.Children.Any()) SetAllItemsChecked(item.Children, isChecked);
		}
	}

	private IEnumerable<FileSystemItem> GetAllCheckedFiles(ObservableCollection<FileSystemItem> items)
	{
		foreach (var item in items)
		{
			if (item.IsChecked && item.IsCodeFile) yield return item;

			if (item.Children.Any())
			{
				foreach (var child in GetAllCheckedFiles(item.Children)) yield return child;
			}
		}
	}

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
}