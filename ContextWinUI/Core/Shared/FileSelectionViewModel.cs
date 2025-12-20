using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
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
	private readonly IProjectSessionManager _sessionManager;
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

		// [CORREÇÃO] Lê todas as configs, incluindo as novas
		bool omitUsings = _sessionManager.OmitUsings;
		bool omitNamespaces = _sessionManager.OmitNamespaces; // Novo
		bool omitComments = _sessionManager.OmitComments;
		bool omitEmptyLines = _sessionManager.OmitEmptyLines; // Novo
		bool includeStructure = _sessionManager.IncludeStructure;
		bool structureOnlyFolders = _sessionManager.StructureOnlyFolders;
		string prePrompt = _sessionManager.PrePrompt;

		OnStatusChanged($"Processando {selectedFiles.Count} arquivos...");

		try
		{
			var sb = new StringBuilder();

			// 1. Pré-Prompt
			if (!string.IsNullOrWhiteSpace(prePrompt))
			{
				sb.AppendLine("/* --- INSTRUÇÕES GLOBAIS (CONTEXTO) --- */");
				sb.AppendLine(prePrompt);
				sb.AppendLine();
			}

			// 2. Estrutura de Pastas
			if (includeStructure)
			{
				sb.AppendLine("/* --- ESTRUTURA DO PROJETO --- */");
				var treeStr = StructureGeneratorHelper.GenerateTree(_rootItems, structureOnlyFolders);
				sb.AppendLine(treeStr);
				sb.AppendLine();
			}

			sb.AppendLine("/* --- CONTEÚDO DOS ARQUIVOS --- */");
			sb.AppendLine();

			// 3. Conteúdo dos Arquivos
			foreach (var file in selectedFiles)
			{
				sb.AppendLine($"// ==================== {file.FullPath} ====================");
				sb.AppendLine();

				var rawContent = await _fileSystemService.ReadFileContentAsync(file.FullPath);
				var extension = System.IO.Path.GetExtension(file.FullPath);

				// [CORREÇÃO] Passa os 6 argumentos corretos
				var cleanContent = CodeCleanupHelper.ProcessCode(
					rawContent,
					extension,
					omitUsings,
					omitNamespaces,
					omitComments,
					omitEmptyLines);

				sb.AppendLine(cleanContent);
				sb.AppendLine();
				sb.AppendLine();
			}

			var dataPackage = new DataPackage();
			dataPackage.SetText(sb.ToString());
			Clipboard.SetContent(dataPackage);

			OnStatusChanged("Copiado com sucesso!");
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