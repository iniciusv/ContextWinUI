using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.ViewModels;

public partial class FileContentViewModel : ObservableObject
{
	private readonly IFileSystemService _fileSystemService;

	[ObservableProperty]
	private FileSystemItem? selectedItem;

	[ObservableProperty]
	private string fileContent = string.Empty;

	[ObservableProperty]
	private bool isLoading;

	public event EventHandler<string>? StatusChanged;

	public FileContentViewModel(IFileSystemService fileSystemService)
	{
		_fileSystemService = fileSystemService;
	}

	public async Task LoadFileAsync(FileSystemItem item)
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
			OnStatusChanged($"Arquivo: {item.Name} ({item.FileSizeFormatted})");
		}
		catch (Exception ex)
		{
			FileContent = $"Erro ao carregar arquivo: {ex.Message}";
			OnStatusChanged("Erro ao carregar arquivo");
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

		OnStatusChanged("Conteúdo copiado!");
	}

	private bool CanCopyToClipboard() => !string.IsNullOrEmpty(FileContent);

	partial void OnFileContentChanged(string value)
	{
		CopyToClipboardCommand.NotifyCanExecuteChanged();
	}

	private void OnStatusChanged(string message)
	{
		StatusChanged?.Invoke(this, message);
	}
}