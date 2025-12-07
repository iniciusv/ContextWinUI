using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> contextTreeItems = new();

	[ObservableProperty]
	private bool isVisible;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private int selectedCount;

	// Evento para notificar o MainViewModel que um arquivo deve ser aberto no editor
	public event EventHandler<FileSystemItem>? FileSelectedForPreview;
	public event EventHandler<string>? StatusChanged;

	public ContextAnalysisViewModel(RoslynAnalyzerService roslynAnalyzer, FileSystemService fileSystemService)
	{
		_roslynAnalyzer = roslynAnalyzer;
		_fileSystemService = fileSystemService;
	}

	public async Task AnalyzeContextAsync(List<FileSystemItem> selectedItems, string rootPath)
	{
		if (!selectedItems.Any()) return;

		IsLoading = true;
		IsVisible = true;
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

	// Aprofundar análise em um item específico (Botão +)
	[RelayCommand]
	private async Task AnalyzeItemDepthAsync(FileSystemItem item)
	{
		if (string.IsNullOrEmpty(item.FullPath)) return;

		IsLoading = true;
		OnStatusChanged($"Analisando profundamente {item.Name}...");

		try
		{
			// Limpa filhos atuais para evitar duplicação
			item.Children.Clear();

			// Re-analisa usando o Roslyn Analyzer para pegar dependências deste arquivo específico
			await PopulateNodeAsync(item, item.FullPath);

			UpdateSelectedCount();
			OnStatusChanged($"Dependências de {item.Name} carregadas.");
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

	private async Task PopulateNodeAsync(FileSystemItem node, string filePath)
	{
		var analysis = await _roslynAnalyzer.AnalyzeFileStructureAsync(filePath);

		// 1. Métodos (apenas informativo, não é "profundo" no sentido de arquivo)
		if (analysis.Methods.Any())
		{
			var methodsGroup = CreateTreeItem("Métodos", "", "\uEA86", false);
			foreach (var method in analysis.Methods)
			{
				methodsGroup.Children.Add(CreateTreeItem(method, "", "\uF158", false));
			}
			node.Children.Add(methodsGroup);
		}

		// 2. Dependências
		if (analysis.Dependencies.Any())
		{
			var contextGroup = CreateTreeItem("Contexto / Dependências", "", "\uE71D", false);
			foreach (var depPath in analysis.Dependencies)
			{
				var depName = Path.GetFileName(depPath);

				// CRUCIAL: Ao criar o item de dependência, passamos o FullPath.
				// Isso ativa a propriedade CanDeepAnalyze = true, habilitando o botão (+)
				var depNode = CreateTreeItem(depName, depPath, "\uE943", true);

				contextGroup.Children.Add(depNode);
			}
			node.Children.Add(contextGroup);
		}

		node.IsExpanded = true;
	}

	// Chamado pelo CodeBehind quando clica no item
	public void SelectFileForPreview(FileSystemItem item)
	{
		// Só dispara se tiver caminho válido e arquivo existir
		if (!string.IsNullOrEmpty(item.FullPath) && File.Exists(item.FullPath))
		{
			FileSelectedForPreview?.Invoke(this, item);
		}
	}

	[RelayCommand]
	private void ItemChecked() => UpdateSelectedCount();

	[RelayCommand]
	private async Task CopyContextToClipboardAsync()
	{
		IsLoading = true;
		try
		{
			var filesToCopy = new HashSet<string>();
			CollectFilesToCopy(ContextTreeItems, filesToCopy);

			if (!filesToCopy.Any())
			{
				OnStatusChanged("Nenhum arquivo selecionado para cópia.");
				return;
			}

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

			var dataPackage = new DataPackage();
			dataPackage.SetText(sb.ToString());
			Clipboard.SetContent(dataPackage);
			OnStatusChanged("Copiado com sucesso!");
		}
		finally
		{
			IsLoading = false;
		}
	}

	[RelayCommand]
	private void Close()
	{
		IsVisible = false;
		ContextTreeItems.Clear();
	}

	private FileSystemItem CreateTreeItem(string name, string path, string customIcon, bool isChecked)
	{
		return new FileSystemItem
		{
			Name = name,
			FullPath = path,
			CustomIcon = customIcon,
			IsChecked = isChecked,
			IsExpanded = true
		};
	}

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