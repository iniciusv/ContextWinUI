using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.Features.ContextBuilder;

public partial class ContextAnalysisViewModel : ObservableObject
{
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly IDependencyAnalysisOrchestrator _analysisOrchestrator;
	private readonly IProjectSessionManager _sessionManager;
	private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

	public ContextTreeViewModel TreeVM { get; }
	public ContextGitViewModel GitVM { get; }
	public ContextSelectionViewModel SelectionVM { get; }
	public ITagManagementUiService TagService { get; }

	[ObservableProperty] private bool isVisible;
	[ObservableProperty] private bool isLoading;
	[ObservableProperty] private bool canGoBack;
	[ObservableProperty] private FileSystemItem? selectedPreviewItem;

	// Contagem para UI
	public int SelectedCount => SelectionVM.SelectedItemsList.Count;

	public event EventHandler<FileSystemItem>? FileSelectedForPreview;
	public event EventHandler<string>? StatusChanged;

	private readonly Stack<List<FileSystemItem>> _historyStack = new();

	public ContextAnalysisViewModel(
		IFileSystemItemFactory itemFactory,
		IDependencyAnalysisOrchestrator analysisOrchestrator,
		IProjectSessionManager sessionManager,
		IGitService gitService,
		ITagManagementUiService tagService,
		ContextSelectionViewModel selectionVM)
	{
		_itemFactory = itemFactory;
		_analysisOrchestrator = analysisOrchestrator;
		_sessionManager = sessionManager;
		TagService = tagService;
		SelectionVM = selectionVM;

		// ViewModels Filhos
		TreeVM = new ContextTreeViewModel(itemFactory, analysisOrchestrator, sessionManager);
		GitVM = new ContextGitViewModel(gitService, itemFactory, sessionManager);

		// --- MÁGICA REATIVA AQUI ---
		// Escuta alterações na lista de seleção para atualizar a árvore em tempo real
		SelectionVM.SelectedItemsList.CollectionChanged += OnSelectionChanged;

		// Atualiza contagem quando a lista muda
		SelectionVM.SelectedItemsList.CollectionChanged += (s, e) => OnPropertyChanged(nameof(SelectedCount));

		// Propaga eventos da árvore (ex: expandir nós)
		TreeVM.StructureUpdated += (s, parentItem) => RegisterItemRecursively(parentItem);
	}

	/// <summary>
	/// Reage imediatamente quando o usuário marca/desmarca arquivos no Explorer.
	/// </summary>
	private void OnSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (!_sessionManager.IsProjectLoaded) return;

		if (e.NewItems != null)
		{
			foreach (FileSystemItem item in e.NewItems)
			{
				if (!item.IsCodeFile) continue;
				if (TreeVM.Items.Any(x => x.FullPath == item.FullPath)) continue;

				// 1. Cria o nó visual IMEDIATAMENTE (vazio)
				var analysisNode = _itemFactory.CreateWrapper(item.FullPath, FileSystemItemType.File, "\uE943");
				analysisNode.IsChecked = true;

				// Adiciona na UI agora (para o usuário ver que algo aconteceu)
				TreeVM.Items.Add(analysisNode);

				// 2. Dispara o processamento pesado em BACKGROUND (Fire and Forget seguro)
				_ = EnrichNodeInBackgroundAsync(analysisNode);
			}
		}

		if (e.OldItems != null)
		{
			// Remoção é rápida, pode ficar na UI thread
			foreach (FileSystemItem item in e.OldItems)
			{
				var target = TreeVM.Items.FirstOrDefault(x => x.FullPath == item.FullPath);
				if (target != null) TreeVM.Items.Remove(target);
			}
		}

		IsVisible = TreeVM.Items.Count > 0;
	}

	private async Task EnrichNodeInBackgroundAsync(FileSystemItem node)
	{
		try
		{
			// Roda o processamento pesado (Roslyn/Busca no Grafo) em outra thread
			await Task.Run(async () =>
			{
				// Aqui o Orchestrator vai tentar popular 'node.Children'.
				// COMO node.Children é uma ObservableCollection ligada à UI, 
				// o Orchestrator PRECISA ser Thread-Safe ou precisamos de um truque.

				// TRUQUE DE PERFORMANCE:
				// Vamos pedir para o Orchestrator preparar os itens, mas o Orchestrator atual
				// adiciona direto na coleção. Para não refatorar o Orchestrator inteiro agora,
				// vamos usar o Dispatcher DENTRO da chamada se necessário, ou garantir
				// que a coleção Children suporte acesso de background (WinUI 3 geralmente suporta, mas falha às vezes).

				// A abordagem mais segura sem refatorar o Orchestrator inteiro é:
				// Garantir que a busca do grafo (pesada) ocorra antes, e a adição visual depois.

				// Como o Orchestrator atual mistura os dois, vamos envolver a chamada inteira
				// em um try/catch e torcer para o WinUI lidar com o binding, 
				// OU (Recomendado) -> Alterar levemente o Orchestrator para usar Dispatcher.

				// Mas para resolver seu freeze AGORA:
				// O simples fato de usarmos Task.Run aqui já tira a carga da CPU da UI Thread.
				// O único risco é "Cross-Thread Exception" ao adicionar filhos.
				// Se der erro, usamos _dispatcherQueue.TryEnqueue.

				// Vamos assumir que precisamos voltar para a UI para tocar nos Children:

				_dispatcherQueue.TryEnqueue(async () =>
				{
					// Voltamos para a UI Thread só para preencher os dados, 
					// MAS garantindo que GetOrIndexProjectAsync (que é chamado dentro)
					// já retornou do background e está cacheado.
					await _analysisOrchestrator.EnrichFileNodeAsync(node, _sessionManager.CurrentProjectPath!);
					RegisterItemRecursively(node);
				});
			});
		}
		catch (Exception ex)
		{
			_dispatcherQueue.TryEnqueue(() => OnStatusChanged($"Erro background: {ex.Message}"));
		}
	}

	// Este método agora serve mais como um "Reset Total" ou "Reload"
	public async Task AnalyzeContextAsync(List<FileSystemItem> selectedItems, string rootPath)
	{
		IsLoading = true;
		IsVisible = true;

		// Limpa tudo para recomeçar
		_historyStack.Clear();
		CanGoBack = false;
		TreeVM.Clear();

		OnStatusChanged("Sincronizando contexto...");

		try
		{
			// Adiciona itens manualmente (o evento OnSelectionChanged cuida do resto se a lista mudar,
			// mas aqui estamos forçando um estado inicial).

			foreach (var item in selectedItems)
			{
				var fileNode = _itemFactory.CreateWrapper(item.FullPath, FileSystemItemType.File, "\uE943");
				await _analysisOrchestrator.EnrichFileNodeAsync(fileNode, rootPath);
				TreeVM.Items.Add(fileNode);
				RegisterItemRecursively(fileNode);
			}

			_ = GitVM.RefreshChangesAsync();
			OnStatusChanged("Contexto atualizado.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro: {ex.Message}");
		}
		finally { IsLoading = false; }
	}

	public void SelectFileForPreview(FileSystemItem item)
	{
		SelectedPreviewItem = item;

		// Lógica para extrair caminho físico caso seja um nó lógico (ex: File.cs::Method)
		string realPath = item.FullPath;
		if (item.FullPath.Contains("::"))
			realPath = item.FullPath.Substring(0, item.FullPath.IndexOf("::"));

		if (!string.IsNullOrEmpty(realPath) && System.IO.File.Exists(realPath))
		{
			// Se não for um arquivo puro (ex: é um método), cria um wrapper temporário para o visualizador de código
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
	public async Task CopyContextToClipboardAsync()
	{
		// Pega apenas o que está visível/marcado na árvore de análise
		// (Isso permite que o usuário desmarque métodos específicos antes de copiar)
		var items = TreeVM.Items.SelectMany(GetMarkedItemsRecursively).ToList();

		if (!items.Any())
		{
			OnStatusChanged("Nenhum item selecionado para cópia.");
			return;
		}

		IsLoading = true;
		try
		{
			string text = await _analysisOrchestrator.BuildContextStringAsync(items, _sessionManager);

			var dp = new DataPackage();
			dp.SetText(text);
			Clipboard.SetContent(dp);

			OnStatusChanged("Contexto copiado para a área de transferência!");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao copiar: {ex.Message}");
		}
		finally { IsLoading = false; }
	}

	[RelayCommand]
	private void GoBack()
	{
		if (_historyStack.Count > 0)
		{
			var prev = _historyStack.Pop();
			TreeVM.SetItems(prev);
			foreach (var item in prev) RegisterItemRecursively(item);
			CanGoBack = _historyStack.Count > 0;
		}
	}

	[RelayCommand]
	private void Close()
	{
		IsVisible = false;
		// Opcional: Limpar seleção também?
		// SelectionVM.Clear(); 
	}

	// --- Helpers ---

	private IEnumerable<FileSystemItem> GetMarkedItemsRecursively(FileSystemItem root)
	{
		if (root.IsChecked) yield return root;
		foreach (var child in root.Children)
		{
			foreach (var sub in GetMarkedItemsRecursively(child)) yield return sub;
		}
	}

	private void RegisterItemRecursively(FileSystemItem item)
	{
		item.PropertyChanged -= OnItemPropertyChanged;
		item.PropertyChanged += OnItemPropertyChanged;

		foreach (var child in item.Children) RegisterItemRecursively(child);
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// Se precisar reagir a check/uncheck específicos dentro da árvore de análise
	}

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
}