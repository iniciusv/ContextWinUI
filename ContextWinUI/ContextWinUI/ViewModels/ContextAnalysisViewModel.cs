using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Helpers; // Importante para o Search
using ContextWinUI.Models;
using ContextWinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.ViewModels;

public partial class ContextAnalysisViewModel : ObservableObject
{
	private readonly RoslynAnalyzerService _roslynAnalyzer;
	private readonly FileSystemService _fileSystemService;

	// Histórico para o botão Voltar
	private readonly Stack<List<FileSystemItem>> _historyStack = new();

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> contextTreeItems = new();

	[ObservableProperty]
	private bool isVisible;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private int selectedCount;

	[ObservableProperty]
	private bool canGoBack;

	public event EventHandler<FileSystemItem>? FileSelectedForPreview;
	public event EventHandler<string>? StatusChanged;

	public ContextAnalysisViewModel(RoslynAnalyzerService roslynAnalyzer, FileSystemService fileSystemService)
	{
		_roslynAnalyzer = roslynAnalyzer;
		_fileSystemService = fileSystemService;
	}

	// 1. ANÁLISE INICIAL
	public async Task AnalyzeContextAsync(List<FileSystemItem> selectedItems, string rootPath)
	{
		if (!selectedItems.Any()) return;

		IsLoading = true;
		IsVisible = true;

		if (ContextTreeItems.Any())
		{
			_historyStack.Push(ContextTreeItems.ToList());
			UpdateCanGoBack();
		}

		ContextTreeItems.Clear();
		OnStatusChanged("Indexando projeto e analisando referências...");

		try
		{
			await _roslynAnalyzer.IndexProjectAsync(rootPath);

			foreach (var item in selectedItems)
			{
				var fileNode = CreateTreeItem(item.Name, item.FullPath, "\uE943", true);
				await PopulateNodeAsync(fileNode, item.FullPath);
				ContextTreeItems.Add(fileNode);
			}

			UpdateSelectedCount();
			OnStatusChanged($"Análise concluída. {ContextTreeItems.Count} arquivos base.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro na análise: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

	// 2. ANÁLISE PROFUNDA (+)
	[RelayCommand]
	private async Task AnalyzeItemDepthAsync(FileSystemItem item)
	{
		if (item == null || string.IsNullOrEmpty(item.FullPath)) return;

		IsLoading = true;
		OnStatusChanged($"Aprofundando análise de {item.Name}...");

		try
		{
			item.Children.Clear();
			await PopulateNodeAsync(item, item.FullPath);
			UpdateSelectedCount();
			OnStatusChanged($"Conteúdo de {item.Name} carregado.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao aprofundar: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

	// 3. BUSCA
	[RelayCommand]
	private void Search(string query)
	{
		TreeSearchHelper.Search(ContextTreeItems, query);
	}

	// 4. VOLTAR
	[RelayCommand]
	private void GoBack()
	{
		if (_historyStack.Count > 0)
		{
			var previousState = _historyStack.Pop();
			ContextTreeItems.Clear();
			foreach (var item in previousState) ContextTreeItems.Add(item);
			UpdateCanGoBack();
			UpdateSelectedCount();
			OnStatusChanged("Histórico restaurado.");
		}
	}

	private void UpdateCanGoBack() => CanGoBack = _historyStack.Count > 0;

	// Helpers Lógicos
	private async Task PopulateNodeAsync(FileSystemItem node, string filePath)
	{
		var analysis = await _roslynAnalyzer.AnalyzeFileStructureAsync(filePath);

		if (analysis.Methods.Any())
		{
			var methodsGroup = CreateTreeItem("Métodos", "", "\uEA86", false);
			foreach (var method in analysis.Methods)
			{
				methodsGroup.Children.Add(CreateTreeItem(method, "", "\uF158", false));
			}
			node.Children.Add(methodsGroup);
		}

		if (analysis.Dependencies.Any())
		{
			var contextGroup = CreateTreeItem("Contexto / Dependências", "", "\uE71D", false);
			foreach (var depPath in analysis.Dependencies)
			{
				var depName = Path.GetFileName(depPath);
				contextGroup.Children.Add(CreateTreeItem(depName, depPath, "\uE943", true));
			}
			node.Children.Add(contextGroup);
		}
		node.IsExpanded = true;
	}

	// Interações UI
	public void SelectFileForPreview(FileSystemItem item)
	{
		if (!string.IsNullOrEmpty(item.FullPath) && File.Exists(item.FullPath))
			FileSelectedForPreview?.Invoke(this, item);
	}

	[RelayCommand]
	private void ItemChecked() => UpdateSelectedCount();

	[RelayCommand]
	private void Close()
	{
		IsVisible = false;
		ContextTreeItems.Clear();
		_historyStack.Clear();
		UpdateCanGoBack();
	}

	[RelayCommand]
	private async Task CopyContextToClipboardAsync()
	{
		IsLoading = true;
		try
		{
			var filesToCopy = new HashSet<string>();
			CollectFilesToCopy(ContextTreeItems, filesToCopy);
			if (!filesToCopy.Any()) return;

			var sb = new StringBuilder();
			sb.AppendLine("/* CONTEXTO SELECIONADO */");
			sb.AppendLine();

			foreach (var path in filesToCopy)
			{
				try
				{
					var content = await _fileSystemService.ReadFileContentAsync(path);
					sb.AppendLine($"// Arquivo: {Path.GetFileName(path)}");
					sb.AppendLine($"// Caminho: {path}");
					sb.AppendLine(content);
					sb.AppendLine();
				}
				catch { }
			}
			var dp = new DataPackage();
			dp.SetText(sb.ToString());
			Clipboard.SetContent(dp);
			OnStatusChanged("Copiado com sucesso!");
		}
		finally { IsLoading = false; }
	}

	private FileSystemItem CreateTreeItem(string name, string path, string customIcon, bool isChecked) =>
		new() { Name = name, FullPath = path, CustomIcon = customIcon, IsChecked = isChecked, IsExpanded = true };

	private void UpdateSelectedCount() => SelectedCount = CountCheckedFiles(ContextTreeItems);

	private int CountCheckedFiles(ObservableCollection<FileSystemItem> items)
	{
		int count = 0;
		foreach (var item in items)
		{
			if (item.IsChecked && !string.IsNullOrEmpty(item.FullPath)) count++;
			if (item.Children.Any()) count += CountCheckedFiles(item.Children);
		}
		return count;
	}

	private void CollectFilesToCopy(ObservableCollection<FileSystemItem> items, HashSet<string> paths)
	{
		foreach (var item in items)
		{
			if (item.IsChecked && !string.IsNullOrEmpty(item.FullPath)) paths.Add(item.FullPath);
			if (item.Children.Any()) CollectFilesToCopy(item.Children, paths);
		}
	}

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
}