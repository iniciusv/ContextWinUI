// ==================== C:\Users\vinic\source\repos\ContextWinUI\ContextWinUI\ViewModels\ContextAnalysisViewModel.cs ====================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Helpers;
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

	// Histórico de navegação
	private readonly Stack<List<FileSystemItem>> _historyStack = new();

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> contextTreeItems = new();

	// --- NOVO: Necessário para o botão de Foco (Sync) funcionar ---
	[ObservableProperty]
	private FileSystemItem? selectedItem;

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

	// --- COMANDOS DE VISUALIZAÇÃO ---

	[RelayCommand]
	private void ExpandAll()
	{
		foreach (var item in ContextTreeItems)
		{
			item.SetExpansionRecursively(true);
		}
	}

	[RelayCommand]
	private void CollapseAll()
	{
		foreach (var item in ContextTreeItems)
		{
			item.SetExpansionRecursively(false);
		}
	}

	// --- NOVO: Lógica do Botão de Alvo (Sync/Focus) ---
	[RelayCommand]
	private void SyncFocus()
	{
		if (SelectedItem == null || !ContextTreeItems.Any()) return;

		foreach (var item in ContextTreeItems)
		{
			SyncFocusRecursive(item, SelectedItem);
		}
	}

	private bool SyncFocusRecursive(FileSystemItem currentItem, FileSystemItem targetItem)
	{
		// Se encontrou o item, expande e retorna true
		if (currentItem == targetItem)
		{
			// Se for pasta ou tiver filhos, expande ele também para ver o conteúdo
			if (currentItem.Children.Any()) currentItem.IsExpanded = true;
			return true;
		}

		bool keepExpanded = false;

		foreach (var child in currentItem.Children)
		{
			if (SyncFocusRecursive(child, targetItem))
			{
				keepExpanded = true;
			}
		}

		// Se faz parte do caminho, mantém expandido. Se não, fecha.
		currentItem.IsExpanded = keepExpanded;
		return keepExpanded;
	}
	// --------------------------------------------------

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
			// Indexa o projeto (se necessário pelo seu RoslynService)
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

	[RelayCommand]
	private async Task AnalyzeItemDepthAsync(FileSystemItem item)
	{
		if (item == null || string.IsNullOrEmpty(item.FullPath)) return;

		IsLoading = true;
		OnStatusChanged($"Aprofundando análise de {item.Name}...");

		try
		{
			// Salva estado atual para permitir voltar
			_historyStack.Push(ContextTreeItems.ToList());
			UpdateCanGoBack();

			// Limpa filhos e repopula com nova análise
			// Nota: Dependendo da lógica, talvez você queira limpar a raiz e focar só nesse item
			// Mas aqui mantive sua lógica de repopular os filhos do nó
			item.Children.Clear();
			await PopulateNodeAsync(item, item.FullPath);

			UpdateSelectedCount();
			OnStatusChanged($"Conteúdo de {item.Name} carregado.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao aprofundar: {ex.Message}");
			// Se der erro, remove do histórico para não ficar inconsistente
			if (_historyStack.Count > 0) _historyStack.Pop();
			UpdateCanGoBack();
		}
		finally
		{
			IsLoading = false;
		}
	}

	// --- NOVO: Comando para o botão de Fluxo de Método (ícone de grafo) ---
	[RelayCommand]
	private async Task AnalyzeMethodFlowAsync(FileSystemItem item)
	{
		if (item == null) return;

		IsLoading = true;
		OnStatusChanged($"Analisando fluxo de chamadas para {item.Name}...");

		try
		{
			_historyStack.Push(ContextTreeItems.ToList());
			UpdateCanGoBack();

			// Aqui você deve chamar seu serviço Roslyn para pegar "Quem chama" e "Quem é chamado"
			// Como não tenho a assinatura exata do seu método novo no RoslynService, 
			// vou usar o PopulateNodeAsync como fallback, mas o ideal é algo como:
			// var flowNodes = await _roslynAnalyzer.GetMethodCallHierarchyAsync(item.FullPath, item.Name);

			// Fallback para não quebrar:
			ContextTreeItems.Clear();
			var flowRoot = CreateTreeItem($"Fluxo: {item.Name}", item.FullPath, "\uE768", true);
			await PopulateNodeAsync(flowRoot, item.FullPath); // Substitua pela lógica real de fluxo
			ContextTreeItems.Add(flowRoot);

			UpdateSelectedCount();
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro no fluxo: {ex.Message}");
			if (_historyStack.Count > 0) _historyStack.Pop();
			UpdateCanGoBack();
		}
		finally
		{
			IsLoading = false;
		}
	}
	// ---------------------------------------------------------------------

	[RelayCommand]
	private void Search(string query)
	{
		TreeSearchHelper.Search(ContextTreeItems, query);
	}

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

	private async Task PopulateNodeAsync(FileSystemItem node, string filePath)
	{
		// Chama seu serviço existente
		var analysis = await _roslynAnalyzer.AnalyzeFileStructureAsync(filePath);

		if (analysis.Methods.Any())
		{
			var methodsGroup = CreateTreeItem("Métodos", "", "\uEA86", false);
			// Define o tipo como LogicalGroup para ícone/comportamento
			methodsGroup.Type = FileSystemItemType.LogicalGroup;

			foreach (var method in analysis.Methods)
			{
				var methodItem = CreateTreeItem(method, filePath, "\uF158", false);
				methodItem.Type = FileSystemItemType.Method; // Define como Método
															 // Se possível, armazene a assinatura para busca de referências futura
				methodItem.MethodSignature = method;
				methodsGroup.Children.Add(methodItem);
			}
			node.Children.Add(methodsGroup);
		}

		if (analysis.Dependencies.Any())
		{
			var contextGroup = CreateTreeItem("Contexto / Dependências", "", "\uE71D", false);
			contextGroup.Type = FileSystemItemType.LogicalGroup;

			foreach (var depPath in analysis.Dependencies)
			{
				var depName = Path.GetFileName(depPath);
				var depItem = CreateTreeItem(depName, depPath, "\uE943", true);
				depItem.Type = FileSystemItemType.Dependency;
				contextGroup.Children.Add(depItem);
			}
			node.Children.Add(contextGroup);
		}
		node.IsExpanded = true;
	}

	public void SelectFileForPreview(FileSystemItem item)
	{
		// --- ATUALIZAÇÃO: Armazena o SelectedItem para o SyncFocus funcionar ---
		SelectedItem = item;

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
		SelectedItem = null;
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

	// Helper para criação rápida
	private FileSystemItem CreateTreeItem(string name, string path, string customIcon, bool isChecked) =>
		new()
		{
			Name = name,
			FullPath = path,
			CustomIcon = customIcon,
			IsChecked = isChecked,
			IsExpanded = true
		};

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