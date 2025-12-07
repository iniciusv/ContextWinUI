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
	private readonly FileSystemService _fileSystemService;
	private ObservableCollection<FileSystemItem> _rootItems = new();

	[ObservableProperty]
	private int selectedFilesCount;

	[ObservableProperty]
	private bool isLoading;

	public event EventHandler<string>? StatusChanged;

	public FileSelectionViewModel(FileSystemService fileSystemService)
	{
		_fileSystemService = fileSystemService;
	}

	public void SetRootItems(ObservableCollection<FileSystemItem> items)
	{
		_rootItems = items;
		UpdateSelectedCount();
	}

	[RelayCommand]
	private void ItemChecked(FileSystemItem item)
	{
		UpdateSelectedCount();
		CopySelectedFilesCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand]
	private void SelectAll()
	{
		SetAllItemsChecked(_rootItems, true);
		UpdateSelectedCount();
		CopySelectedFilesCommand.NotifyCanExecuteChanged();
	}

	[RelayCommand]
	private void UnselectAll()
	{
		SetAllItemsChecked(_rootItems, false);
		UpdateSelectedCount();
		CopySelectedFilesCommand.NotifyCanExecuteChanged();
	}

	private void SetAllItemsChecked(ObservableCollection<FileSystemItem> items, bool isChecked)
	{
		foreach (var item in items)
		{
			if (item.IsCodeFile)
			{
				item.IsChecked = isChecked;
			}

			if (item.IsDirectory && item.Children.Any())
			{
				SetAllItemsChecked(item.Children, isChecked);
			}
		}
	}

	[RelayCommand(CanExecute = nameof(CanCopySelectedFiles))]
	private async Task CopySelectedFilesAsync()
	{
		var selectedFiles = GetAllCheckedFiles(_rootItems).ToList();

		if (!selectedFiles.Any())
			return;

		IsLoading = true;
		OnStatusChanged($"Copiando {selectedFiles.Count} arquivo(s)...");

		try
		{
			var sb = new StringBuilder();

			foreach (var file in selectedFiles)
			{
				sb.AppendLine($"// ==================== {file.FullPath} ====================");
				sb.AppendLine();

				var content = await _fileSystemService.ReadFileContentAsync(file.FullPath);
				sb.AppendLine(content);
				sb.AppendLine();
				sb.AppendLine();
			}

			var dataPackage = new DataPackage();
			dataPackage.SetText(sb.ToString());
			Clipboard.SetContent(dataPackage);

			OnStatusChanged($"{selectedFiles.Count} arquivo(s) copiado(s) para a área de transferência!");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao copiar arquivos: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

	private bool CanCopySelectedFiles() => SelectedFilesCount > 0;

	private IEnumerable<FileSystemItem> GetAllCheckedFiles(ObservableCollection<FileSystemItem> items)
	{
		foreach (var item in items)
		{
			if (item.IsChecked && item.IsCodeFile)
			{
				yield return item;
			}

			if (item.IsDirectory && item.Children.Any())
			{
				foreach (var child in GetAllCheckedFiles(item.Children))
				{
					yield return child;
				}
			}
		}
	}

	private void UpdateSelectedCount()
	{
		SelectedFilesCount = GetAllCheckedFiles(_rootItems).Count();
	}

	private void OnStatusChanged(string message)
	{
		StatusChanged?.Invoke(this, message);
	}
}