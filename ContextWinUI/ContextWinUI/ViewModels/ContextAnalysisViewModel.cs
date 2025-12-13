using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
	private readonly FileSystemItemFactory _itemFactory;

	// Histórico de navegação para o botão "Voltar"
	private readonly Stack<List<FileSystemItem>> _historyStack = new();

	// Árvore Hierárquica (Visual)
	[ObservableProperty]
	private ObservableCollection<FileSystemItem> contextTreeItems = new();

	// Lista Plana de Selecionados (Sincronizada via Eventos)
	[ObservableProperty]
	private ObservableCollection<FileSystemItem> selectedItemsList = new();

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

	public ContextAnalysisViewModel(RoslynAnalyzerService roslynAnalyzer, FileSystemService fileSystemService, FileSystemItemFactory itemFactory)
	{
		_roslynAnalyzer = roslynAnalyzer;
		_fileSystemService = fileSystemService;
		_itemFactory = itemFactory;
	}

	// --- SINCRONIZAÇÃO REATIVA (O Coração do Flyweight) ---

	/// <summary>
	/// Registra ouvintes nos wrappers da árvore.
	/// Quando o SharedState de um item muda (ex: IsChecked), o Wrapper dispara PropertyChanged.
	/// </summary>
	private void RegisterItemEventsRecursively(FileSystemItem item)
	{
		// Remove antes de adicionar para evitar vazamento de memória ou eventos duplicados
		item.PropertyChanged -= OnItemPropertyChanged;
		item.PropertyChanged += OnItemPropertyChanged;

		// Se o item já nasceu marcado (ex: dependência vinda do Explorer), garante que esteja na lista
		if (item.IsChecked)
		{
			AddToSelectionListIfNew(item);
		}

		foreach (var child in item.Children)
		{
			RegisterItemEventsRecursively(child);
		}
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// Se a propriedade IsChecked mudou no Wrapper (refletindo o SharedState)
		if (sender is FileSystemItem item && e.PropertyName == nameof(FileSystemItem.IsChecked))
		{
			if (item.IsChecked)
			{
				AddToSelectionListIfNew(item);
			}
			else
			{
				RemoveFromSelectionList(item);
			}
			UpdateSelectedCount();
		}
	}

	private void AddToSelectionListIfNew(FileSystemItem item)
	{
		// Evita duplicatas visuais na lista plana baseada no caminho do arquivo
		if (!SelectedItemsList.Any(x => x.FullPath == item.FullPath))
		{
			SelectedItemsList.Add(item);
		}
	}

	private void RemoveFromSelectionList(FileSystemItem item)
	{
		// Remove o item da lista plana baseada no caminho
		var toRemove = SelectedItemsList.FirstOrDefault(x => x.FullPath == item.FullPath);
		if (toRemove != null)
		{
			SelectedItemsList.Remove(toRemove);
		}
	}

	private void UpdateSelectedCount() => SelectedCount = SelectedItemsList.Count;


	// --- COMANDOS DE ANÁLISE ---

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
		// Nota: Não limpamos SelectedItemsList aqui para manter a persistência da seleção
		// entre diferentes visualizações, a menos que seja um comportamento desejado limpar tudo.
		// Para este exemplo, vou limpar para começar uma nova análise "limpa".
		SelectedItemsList.Clear();

		OnStatusChanged("Indexando projeto e analisando referências...");

		try
		{
			// Indexa todo o projeto para resolver referências cruzadas
			await _roslynAnalyzer.IndexProjectAsync(rootPath);

			foreach (var item in selectedItems)
			{
				// USA A FACTORY: Cria um wrapper raiz para a árvore de análise
				var fileNode = _itemFactory.CreateWrapper(item.FullPath, FileSystemItemType.File, "\uE943");

				// Popula filhos (Métodos e Dependências)
				await PopulateNodeAsync(fileNode, item.FullPath);

				// Registra eventos para sincronização
				RegisterItemEventsRecursively(fileNode);

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

	private async Task PopulateNodeAsync(FileSystemItem node, string filePath)
	{
		var analysis = await _roslynAnalyzer.AnalyzeFileStructureAsync(filePath);

		// 1. Grupo de Métodos
		if (analysis.Methods.Any())
		{
			// Cria um nó lógico (não existe arquivo real, usamos um path fictício para chave do cache)
			var methodsGroup = _itemFactory.CreateWrapper($"{filePath}::methods", FileSystemItemType.LogicalGroup, "\uEA86");
			methodsGroup.SharedState.Name = "Métodos"; // Sobrescreve nome visual

			foreach (var method in analysis.Methods)
			{
				var methodItem = _itemFactory.CreateWrapper($"{filePath}::{method}", FileSystemItemType.Method, "\uF158");
				methodItem.SharedState.Name = method;
				methodItem.MethodSignature = method;
				methodsGroup.Children.Add(methodItem);
			}
			node.Children.Add(methodsGroup);
		}

		// 2. Grupo de Dependências
		if (analysis.Dependencies.Any())
		{
			var contextGroup = _itemFactory.CreateWrapper($"{filePath}::deps", FileSystemItemType.LogicalGroup, "\uE71D");
			contextGroup.SharedState.Name = "Contexto / Dependências";

			foreach (var depPath in analysis.Dependencies)
			{
				// AQUI OCORRE A MÁGICA:
				// Se 'depPath' já foi carregado no Explorer ou em outra parte da árvore,
				// CreateWrapper retorna um novo nó visual, mas apontando para o MESMO estado (IsChecked).
				var depItem = _itemFactory.CreateWrapper(depPath, FileSystemItemType.Dependency, "\uE943");

				// Opcional: Se quiser que dependências já venham marcadas por padrão:
				depItem.IsChecked = true;

				contextGroup.Children.Add(depItem);
			}
			node.Children.Add(contextGroup);
		}
		node.IsExpanded = true;
	}

	[RelayCommand]
	private async Task AnalyzeItemDepthAsync(FileSystemItem item)
	{
		if (item == null || string.IsNullOrEmpty(item.FullPath)) return;

		IsLoading = true;
		OnStatusChanged($"Aprofundando análise de {item.Name}...");

		try
		{
			_historyStack.Push(ContextTreeItems.ToList());
			UpdateCanGoBack();

			item.Children.Clear();
			await PopulateNodeAsync(item, item.FullPath);

			// Importante: Re-registrar eventos nos novos filhos
			RegisterItemEventsRecursively(item);

			UpdateSelectedCount();
			OnStatusChanged($"Conteúdo de {item.Name} carregado.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao aprofundar: {ex.Message}");
			if (_historyStack.Count > 0) _historyStack.Pop();
			UpdateCanGoBack();
		}
		finally
		{
			IsLoading = false;
		}
	}

	[RelayCommand]
	private async Task AnalyzeMethodFlowAsync(FileSystemItem item)
	{
		if (item == null) return;
		IsLoading = true;
		OnStatusChanged($"Analisando fluxo de {item.Name}...");

		try
		{
			_historyStack.Push(ContextTreeItems.ToList());
			UpdateCanGoBack();

			ContextTreeItems.Clear();

			// Criação simulada do fluxo (substituir por lógica real do Roslyn)
			var flowRoot = _itemFactory.CreateWrapper($"{item.FullPath}::flow", FileSystemItemType.LogicalGroup, "\uE768");
			flowRoot.SharedState.Name = $"Fluxo: {item.Name}";

			// Aqui você chamaria PopulateNodeAsync ou similar para preencher o fluxo
			await PopulateNodeAsync(flowRoot, item.FullPath);

			RegisterItemEventsRecursively(flowRoot);
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

	// --- COMANDOS VISUAIS E UTILITÁRIOS ---

	[RelayCommand]
	private void ExpandAll()
	{
		foreach (var item in ContextTreeItems) item.SetExpansionRecursively(true);
	}

	[RelayCommand]
	private void CollapseAll()
	{
		foreach (var item in ContextTreeItems) item.SetExpansionRecursively(false);
	}

	[RelayCommand]
	private void SyncFocus()
	{
		if (SelectedItem == null || !ContextTreeItems.Any()) return;
		foreach (var item in ContextTreeItems) SyncFocusRecursive(item, SelectedItem);
	}

	private bool SyncFocusRecursive(FileSystemItem currentItem, FileSystemItem targetItem)
	{
		// Comparação por Path é segura devido ao Flyweight
		if (currentItem.FullPath == targetItem.FullPath)
		{
			if (currentItem.Children.Any()) currentItem.IsExpanded = true;
			return true;
		}

		bool keepExpanded = false;
		foreach (var child in currentItem.Children)
		{
			if (SyncFocusRecursive(child, targetItem)) keepExpanded = true;
		}

		currentItem.IsExpanded = keepExpanded;
		return keepExpanded;
	}

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
			foreach (var item in previousState)
			{
				ContextTreeItems.Add(item);
				// Segurança: garante que eventos estejam ativos
				RegisterItemEventsRecursively(item);
			}
			UpdateCanGoBack();
			UpdateSelectedCount();
			OnStatusChanged("Histórico restaurado.");
		}
	}

	private void UpdateCanGoBack() => CanGoBack = _historyStack.Count > 0;

	public void SelectFileForPreview(FileSystemItem item)
	{
		SelectedItem = item;
		if (!string.IsNullOrEmpty(item.FullPath) && File.Exists(item.FullPath))
			FileSelectedForPreview?.Invoke(this, item);
	}

	[RelayCommand]
	private void Close()
	{
		IsVisible = false;
		ContextTreeItems.Clear();
		SelectedItemsList.Clear();
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
			if (!SelectedItemsList.Any()) return;

			var sb = new StringBuilder();
			sb.AppendLine("/* CONTEXTO SELECIONADO */");
			sb.AppendLine();

			// Ordena para garantir consistência
			foreach (var item in SelectedItemsList.OrderBy(x => x.FullPath))
			{
				if (string.IsNullOrEmpty(item.FullPath)) continue;

				try
				{
					// Se for um método específico, tenta extrair só ele (se a lógica existir)
					if (item.Type == FileSystemItemType.Method && !string.IsNullOrEmpty(item.MethodSignature))
					{
						sb.AppendLine($"// Método: {item.Name}");
						sb.AppendLine($"// Origem: {item.FullPath}");
						// TODO: Implementar extração de texto do método no FileService
						sb.AppendLine("// (Conteúdo do método)");
					}
					else
					{
						var content = await _fileSystemService.ReadFileContentAsync(item.FullPath);
						sb.AppendLine($"// Arquivo: {Path.GetFileName(item.FullPath)}");
						sb.AppendLine($"// Caminho: {item.FullPath}");
						sb.AppendLine(content);
					}
					sb.AppendLine();
				}
				catch { }
			}
			var dp = new DataPackage();
			dp.SetText(sb.ToString());
			Clipboard.SetContent(dp);
			OnStatusChanged("Conteúdo copiado com sucesso!");
		}
		finally { IsLoading = false; }
	}

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
}