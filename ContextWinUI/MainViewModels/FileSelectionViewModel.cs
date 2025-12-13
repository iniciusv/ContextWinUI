using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Helpers; // Helper novo
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
	private readonly IProjectSessionManager _sessionManager; // Injeção necessária
	private ObservableCollection<FileSystemItem> _rootItems = new();

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(CopySelectedFilesCommand))]
	private int selectedFilesCount;

	[ObservableProperty]
	private bool isLoading;

	public event EventHandler<string>? StatusChanged;

	public FileSelectionViewModel(IFileSystemService fileSystemService, IProjectSessionManager sessionManager)
	{
		_fileSystemService = fileSystemService;
		_sessionManager = sessionManager;
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

		// Lê as configurações do SessionManager no momento exato da cópia
		bool omitUsings = _sessionManager.OmitUsings;
		bool omitComments = _sessionManager.OmitComments;
		string prePrompt = _sessionManager.PrePrompt;

		OnStatusChanged($"Processando {selectedFiles.Count} arquivos...");

		try
		{
			var sb = new StringBuilder();

			// 1. Adiciona o Pré-Prompt se houver
			if (!string.IsNullOrWhiteSpace(prePrompt))
			{
				sb.AppendLine("/* --- INSTRUÇÕES GLOBAIS (CONTEXTO) --- */");
				sb.AppendLine(prePrompt);
				sb.AppendLine();
				sb.AppendLine("/* --- INÍCIO DOS ARQUIVOS --- */");
				sb.AppendLine();
			}

			foreach (var file in selectedFiles)
			{
				sb.AppendLine($"// ==================== {file.FullPath} ====================");
				sb.AppendLine();

				var rawContent = await _fileSystemService.ReadFileContentAsync(file.FullPath);
				var extension = System.IO.Path.GetExtension(file.FullPath);

				// 2. Limpa o código com o Helper
				var cleanContent = CodeCleanupHelper.ProcessCode(rawContent, extension, omitUsings, omitComments);

				sb.AppendLine(cleanContent);
				sb.AppendLine();
				sb.AppendLine();
			}

			var dataPackage = new DataPackage();
			dataPackage.SetText(sb.ToString());
			Clipboard.SetContent(dataPackage);

			OnStatusChanged($"{selectedFiles.Count} arquivos copiados com sucesso!");
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