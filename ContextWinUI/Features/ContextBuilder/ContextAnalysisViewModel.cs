using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Services;
using ContextWinUI.Features.CodeAnalyses; // Namespace da nova feature
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.ViewModels;

public partial class ContextAnalysisViewModel : ObservableObject
{
	// =========================================================================
	// DEPENDÊNCIAS
	// =========================================================================
	private readonly IRoslynAnalyzerService _roslynAnalyzer;
	private readonly IFileSystemService _fileSystemService;
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly IGitService _gitService;
	private readonly IProjectSessionManager _sessionManager;

	// Nova dependência que contém a lógica de negócio extraída
	private readonly IDependencyAnalysisOrchestrator _analysisOrchestrator;

	public ITagManagementUiService TagService { get; }
	public ContextSelectionViewModel SelectionVM { get; }

	// =========================================================================
	// ESTADO DA UI
	// =========================================================================

	// Pilha para o botão "Voltar" (Histórico de navegação na árvore)
	private readonly Stack<List<FileSystemItem>> _historyStack = new();

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> contextTreeItems = new();

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> gitModifiedItems = new();

	[ObservableProperty]
	private FileSystemItem? selectedItem;

	[ObservableProperty]
	private bool isVisible;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private bool canGoBack;

	// Proxy para notificar a View sobre contagem de itens
	public int SelectedCount => SelectionVM.SelectedItemsList.Count;

	// Eventos para comunicação com a View (Code-Behind)
	public event EventHandler<FileSystemItem>? FileSelectedForPreview;
	public event EventHandler<string>? StatusChanged;

	// =========================================================================
	// CONSTRUTOR
	// =========================================================================
	public ContextAnalysisViewModel(
			IRoslynAnalyzerService roslynAnalyzer,
			IFileSystemService fileSystemService,
			IFileSystemItemFactory itemFactory,
			ITagManagementUiService tagService,
			IGitService gitService,
			IProjectSessionManager sessionManager,
			ContextSelectionViewModel selectionVM,
			IDependencyAnalysisOrchestrator analysisOrchestrator)
	{
		_roslynAnalyzer = roslynAnalyzer;
		_fileSystemService = fileSystemService;
		_itemFactory = itemFactory;
		TagService = tagService;
		_gitService = gitService;
		_sessionManager = sessionManager;
		SelectionVM = selectionVM;
		_analysisOrchestrator = analysisOrchestrator;

		// Atualiza a contagem na UI quando a seleção muda
		SelectionVM.SelectedItemsList.CollectionChanged += (s, e) =>
		{
			OnPropertyChanged(nameof(SelectedCount));
		};
	}

	// =========================================================================
	// COMANDOS PRINCIPAIS DE ANÁLISE
	// =========================================================================

	/// <summary>
	/// Entrada Principal: Chamado pela MainViewModel quando clica em "Analisar Contexto"
	/// </summary>
	public async Task AnalyzeContextAsync(List<FileSystemItem> selectedItems, string rootPath)
	{
		if (!selectedItems.Any()) return;

		IsLoading = true;
		IsVisible = true;

		// Salva histórico se já houver algo
		if (ContextTreeItems.Any())
		{
			_historyStack.Push(ContextTreeItems.ToList());
			UpdateCanGoBack();
		}

		ContextTreeItems.Clear();
		SelectionVM.Clear();
		OnStatusChanged("Inicializando análise...");

		try
		{
			// Indexação inicial global
			await _roslynAnalyzer.IndexProjectAsync(rootPath);

			foreach (var item in selectedItems)
			{
				// Cria o nó raiz visual
				var fileNode = _itemFactory.CreateWrapper(item.FullPath, FileSystemItemType.File, "\uE943");

				// Usa o Orchestrator para preencher (Métodos, Dependências iniciais)
				await _analysisOrchestrator.EnrichFileNodeAsync(fileNode, rootPath);

				// Configura eventos de UI (Checkbox, Seleção)
				RegisterItemEventsRecursively(fileNode);

				ContextTreeItems.Add(fileNode);
				SelectionVM.AddItem(fileNode); // Auto-seleciona a raiz
			}
			OnStatusChanged("Análise concluída.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro na análise: {ex.Message}");
		}
		finally { IsLoading = false; }
	}

	/// <summary>
	/// Botão "+" : Aprofundar análise na classe (Drill Down)
	/// </summary>
	[RelayCommand]
	private async Task AnalyzeItemDepthAsync(FileSystemItem item)
	{
		// Validação
		if (item == null || string.IsNullOrEmpty(item.FullPath)) return;
		if (!item.IsCodeFile) return;

		IsLoading = true;
		try
		{
			OnStatusChanged($"Analisando estrutura de {item.Name}...");

			// Delega para o Orchestrator a lógica de limpar filhos e buscar novos dados
			await _analysisOrchestrator.EnrichFileNodeAsync(item, _sessionManager.CurrentProjectPath);

			// Re-conecta os eventos nos novos filhos criados
			RegisterItemEventsRecursively(item);

			// Expande visualmente
			item.IsExpanded = true;

			OnStatusChanged("Estrutura atualizada.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro na análise detalhada: {ex.Message}");
		}
		finally { IsLoading = false; }
	}

	/// <summary>
	/// Botão "Play" (Quadrado): Analisar fluxo do método
	/// </summary>
	[RelayCommand]
	private async Task AnalyzeMethodFlow(FileSystemItem item)
	{
		// Validação
		if (item == null || item.Type != FileSystemItemType.Method) return;

		IsLoading = true;
		try
		{
			OnStatusChanged($"Analisando fluxo: {item.Name}...");

			// Delega para o Orchestrator a lógica de parsear o corpo do método
			await _analysisOrchestrator.EnrichMethodFlowAsync(item, _sessionManager.CurrentProjectPath);

			// Re-conecta eventos (Importante: O Orchestrator já marca IsChecked=true, 
			// mas precisamos registrar o evento para o SelectionVM saber disso)
			RegisterItemEventsRecursively(item);

			item.IsExpanded = true;
			OnStatusChanged("Fluxo analisado.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro no fluxo: {ex.Message}");
		}
		finally { IsLoading = false; }
	}

	/// <summary>
	/// Botão Copiar: Gera o texto final para o Clipboard
	/// </summary>
	[RelayCommand]
	public async Task CopyContextToClipboardAsync()
	{
		var selectedItems = SelectionVM.SelectedItemsList.ToList();
		if (!selectedItems.Any())
		{
			OnStatusChanged("Nenhum item selecionado.");
			return;
		}

		IsLoading = true;
		try
		{
			// O Orchestrator cuida de toda a lógica de:
			// 1. Agrupar por arquivo (evitar duplicatas)
			// 2. Aplicar filtros (Métodos vs Arquivo Completo)
			// 3. Limpar código (Regex)
			string finalText = await _analysisOrchestrator.BuildContextStringAsync(selectedItems, _sessionManager);

			var dp = new DataPackage();
			dp.SetText(finalText);
			Clipboard.SetContent(dp);

			OnStatusChanged("Conteúdo copiado com sucesso!");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao copiar: {ex.Message}");
		}
		finally { IsLoading = false; }
	}

	// =========================================================================
	// GIT
	// =========================================================================

	[RelayCommand]
	public async Task RefreshGitChangesAsync()
	{
		var rootPath = _sessionManager.CurrentProjectPath;

		if (string.IsNullOrEmpty(rootPath) || !_gitService.IsGitRepository(rootPath))
		{
			GitModifiedItems.Clear();
			OnStatusChanged("Git não disponível.");
			return;
		}

		IsLoading = true;
		try
		{
			var changedFiles = await _gitService.GetModifiedFilesAsync(rootPath);
			GitModifiedItems.Clear();

			foreach (var path in changedFiles)
			{
				var item = _itemFactory.CreateWrapper(path, FileSystemItemType.File, "\uE70F");
				RegisterItemEventsRecursively(item);
				GitModifiedItems.Add(item);
			}
			OnStatusChanged("Git atualizado.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro Git: {ex.Message}");
		}
		finally { IsLoading = false; }
	}

	// =========================================================================
	// GESTÃO DE UI E EVENTOS (A "Cola" entre View e ViewModel)
	// =========================================================================

	public void UpdateSelectionPreview(IEnumerable<FileSystemItem> items)
	{
		if (IsLoading) return;
		ContextTreeItems.Clear();
		SelectionVM.Clear();
		foreach (var item in items)
		{
			var fileNode = _itemFactory.CreateWrapper(item.FullPath, FileSystemItemType.File, "\uE943");
			RegisterItemEventsRecursively(fileNode);
			ContextTreeItems.Add(fileNode);
			SelectionVM.AddItem(fileNode);
		}
	}

	/// <summary>
	/// Método CRUCIAL. Conecta o evento PropertyChanged de cada item (CheckBox)
	/// ao SelectionVM. Sem isso, marcar um checkbox na árvore não atualiza a lista de cópia.
	/// </summary>
	private void RegisterItemEventsRecursively(FileSystemItem item)
	{
		// Remove antes de adicionar para evitar duplicidade de inscrição
		item.PropertyChanged -= OnItemPropertyChanged;
		item.PropertyChanged += OnItemPropertyChanged;

		// Se o item nasceu marcado (ex: via Orchestrator), avisa o SelectionVM agora
		if (item.IsChecked)
		{
			SelectionVM.AddItem(item);
		}

		foreach (var child in item.Children)
		{
			RegisterItemEventsRecursively(child);
		}
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is FileSystemItem item && e.PropertyName == nameof(FileSystemItem.IsChecked))
		{
			if (item.IsChecked) SelectionVM.AddItem(item);
			else SelectionVM.RemoveItem(item);
		}
	}

	// =========================================================================
	// NAVEGAÇÃO E UTILITÁRIOS
	// =========================================================================

	public void SelectFileForPreview(FileSystemItem item)
	{
		SelectedItem = item;
		// Lógica para obter caminho físico real caso seja um método ou dependência
		string realPath = item.FullPath;
		if (item.FullPath.Contains("::"))
			realPath = item.FullPath.Substring(0, item.FullPath.IndexOf("::"));

		if (!string.IsNullOrEmpty(realPath) && File.Exists(realPath))
		{
			// Cria um wrapper temporário para visualização se necessário
			if (item.Type != FileSystemItemType.File)
			{
				var tempItem = _itemFactory.CreateWrapper(realPath, FileSystemItemType.File);
				FileSelectedForPreview?.Invoke(this, tempItem);
			}
			else
			{
				FileSelectedForPreview?.Invoke(this, item);
			}
		}
	}

	[RelayCommand]
	private void GoBack()
	{
		if (_historyStack.Count > 0)
		{
			var prev = _historyStack.Pop();
			ContextTreeItems.Clear();
			foreach (var item in prev)
			{
				ContextTreeItems.Add(item);
				// Importante: Re-registrar eventos ao restaurar do histórico
				RegisterItemEventsRecursively(item);
			}
			UpdateCanGoBack();
		}
	}

	private void UpdateCanGoBack() => CanGoBack = _historyStack.Count > 0;

	[RelayCommand]
	private void Close()
	{
		IsVisible = false;
		ContextTreeItems.Clear();
		SelectionVM.Clear();
		GitModifiedItems.Clear();
		_historyStack.Clear();
		UpdateCanGoBack();
		SelectedItem = null;
	}

	[RelayCommand] private void ExpandAll() { foreach (var item in ContextTreeItems) item.SetExpansionRecursively(true); }
	[RelayCommand] private void CollapseAll() { foreach (var item in ContextTreeItems) item.SetExpansionRecursively(false); }
	[RelayCommand] private void Search(string query) => TreeSearchHelper.Search(ContextTreeItems, query, CancellationToken.None);
	[RelayCommand] private void SyncFocus() { /* Lógica de sync focus se necessária nesta view */ }

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
}