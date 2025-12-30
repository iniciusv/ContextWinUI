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
	private readonly IFileSelectionService _fileSelectionService; // Nova dependência

	private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

	public ContextTreeViewModel TreeVM { get; }
	public ContextGitViewModel GitVM { get; }
	public ContextSelectionViewModel SelectionVM { get; }
	public ITagManagementUiService TagService { get; }

	[ObservableProperty] private bool isVisible;
	[ObservableProperty] private bool isLoading;
	[ObservableProperty] private bool canGoBack;
	[ObservableProperty] private FileSystemItem? selectedPreviewItem;

	public int SelectedCount => SelectionVM.SelectedItemsList.Count;

	public event EventHandler<string>? StatusChanged;

	private readonly Stack<List<FileSystemItem>> _historyStack = new();

	public ContextAnalysisViewModel(
		IFileSystemItemFactory itemFactory,
		IDependencyAnalysisOrchestrator analysisOrchestrator,
		IProjectSessionManager sessionManager,
		IGitService gitService,
		ITagManagementUiService tagService,
		ContextSelectionViewModel selectionVM,
		IFileSelectionService fileSelectionService)
	{
		_itemFactory = itemFactory;
		_analysisOrchestrator = analysisOrchestrator;
		_sessionManager = sessionManager;
		_fileSelectionService = fileSelectionService;
		TagService = tagService;
		SelectionVM = selectionVM;

		TreeVM = new ContextTreeViewModel(itemFactory, analysisOrchestrator, sessionManager);
		GitVM = new ContextGitViewModel(gitService, itemFactory, sessionManager);

		SelectionVM.SelectedItemsList.CollectionChanged += OnSelectionChanged;
		SelectionVM.SelectedItemsList.CollectionChanged += (s, e) => OnPropertyChanged(nameof(SelectedCount));

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
				//if (!item.IsCodeFile) continue;
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

	[RelayCommand]
	private async Task AnalyzeAsync()
	{
		// Coloque aqui sua lógica de análise
		await Task.Delay(100); // Exemplo simulado
	}

	private async Task EnrichNodeInBackgroundAsync(FileSystemItem node)
	{
		try
		{
			// 1. O trabalho pesado acontece no background
			await Task.Run(async () =>
			{
				await _analysisOrchestrator.EnrichFileNodeAsync(node, _sessionManager.CurrentProjectPath!);
			});

			// 2. CORREÇÃO CRÍTICA:
			// Assim que a tarefa termina, voltamos para a Thread Principal para "amarrar" os eventos nos filhos que acabaram de nascer.
			_dispatcherQueue.TryEnqueue(() =>
			{
				// Isso vai varrer todos os filhos novos e conectar o PropertyChanged neles
				RegisterItemRecursively(node);

				// Log de debug opcional para você ver se funcionou
				System.Diagnostics.Debug.WriteLine($"Node {node.Name} enriquecido. Filhos registrados: {node.Children.Count}");
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
		string realPath = item.FullPath;

		// Lógica para tratar métodos (ex: "Arquivo.cs::MetodoA")
		if (item.FullPath.Contains("::"))
			realPath = item.FullPath.Substring(0, item.FullPath.IndexOf("::"));

		if (!string.IsNullOrEmpty(realPath) && System.IO.File.Exists(realPath))
		{
			FileSystemItem itemToSend;

			if (item.Type != FileSystemItemType.File)
			{
				itemToSend = _itemFactory.CreateWrapper(realPath, FileSystemItemType.File);
			}
			else
			{
				itemToSend = item;
			}

			// AQUI ESTÁ A MUDANÇA: Usamos o serviço global
			_fileSelectionService.SetSelection(itemToSend);
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


	private IEnumerable<FileSystemItem> GetMarkedItemsRecursively(FileSystemItem root)
	{
		if (root.IsChecked) yield return root;
		foreach (var child in root.Children)
		{
			foreach (var sub in GetMarkedItemsRecursively(child)) yield return sub;
		}
	}

	// --- INÍCIO DA SUBSTITUIÇÃO EM ContextAnalysisViewModel.cs ---

	// 1. Método recursivo aprimorado para ouvir quando filhos são adicionados
	private void RegisterItemRecursively(FileSystemItem item)
	{
		// Remove listeners antigos para segurança (evita duplicação)
		item.PropertyChanged -= OnItemPropertyChanged;
		item.Children.CollectionChanged -= OnChildrenCollectionChanged;

		// Adiciona o listener para capturar o CheckBox (IsChecked)
		item.PropertyChanged += OnItemPropertyChanged;

		// Adiciona o listener para saber se NOVOS filhos nasceram (IMPORTANTE!)
		item.Children.CollectionChanged += OnChildrenCollectionChanged;

		// Aplica a mesma lógica para os filhos que já existem agora
		foreach (var child in item.Children)
		{
			RegisterItemRecursively(child);
		}
	}

	// 2. Novo método para lidar com filhos adicionados dinamicamente (pela análise)
	private void OnChildrenCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		// Se novos itens foram adicionados à lista Children...
		if (e.NewItems != null)
		{
			foreach (FileSystemItem newItem in e.NewItems)
			{
				// ...registramos eles imediatamente para ouvir seus clicks!
				RegisterItemRecursively(newItem);
			}
		}

		// Boa prática: limpar listeners de itens removidos
		if (e.OldItems != null)
		{
			foreach (FileSystemItem oldItem in e.OldItems)
			{
				oldItem.PropertyChanged -= OnItemPropertyChanged;
				oldItem.Children.CollectionChanged -= OnChildrenCollectionChanged;
			}
		}
	}

	// --- FIM DA SUBSTITUIÇÃO ---

	// NOVO MÉTODO: Limpeza de eventos para evitar memory leaks
	private void UnregisterItemRecursively(FileSystemItem item)
	{
		item.PropertyChanged -= OnItemPropertyChanged;
		item.Children.CollectionChanged -= OnChildrenCollectionChanged;

		foreach (var child in item.Children)
		{
			UnregisterItemRecursively(child);
		}
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// Quando qualquer item (pai ou filho) for marcado/desmarcado, atualiza a lista de seleção
		if (e.PropertyName == nameof(FileSystemItem.IsChecked))
		{
			SelectionVM.RefreshSelectedItems(TreeVM.Items);
		}
	}

	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
}