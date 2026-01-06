using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphAnalysis;

public partial class SemanticGraphViewModel : ObservableObject
{
	private readonly ISemanticIndexService _indexService;
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly IFileSelectionService _selectionService;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private string statusMessage = "Aguardando carregamento...";

	public ObservableCollection<GraphNodeViewModel> RootNodes { get; } = new();
	public ObservableCollection<GraphNodeViewModel> SearchSuggestions { get; } = new();

	public SemanticGraphViewModel(
		ISemanticIndexService indexService,
		IFileSystemItemFactory itemFactory,
		IFileSelectionService selectionService)
	{
		_indexService = indexService;
		_itemFactory = itemFactory;
		_selectionService = selectionService;
	}

	[RelayCommand]
	public async Task LoadGraphAsync()
	{
		IsLoading = true;
		StatusMessage = "Carregando grafo semântico...";
		RootNodes.Clear();
		SearchSuggestions.Clear();

		await Task.Run(() =>
		{
			var graph = _indexService.GetCurrentGraph();
			var rootSymbols = graph.Nodes.Values
				.Where(n => n.Type == SymbolType.Class || n.Type == SymbolType.Interface)
				.OrderBy(n => n.Name);

			foreach (var node in rootSymbols)
			{
				App.MainWindow.DispatcherQueue.TryEnqueue(() =>
				{
					RootNodes.Add(new GraphNodeViewModel(node, graph));
				});
			}
		});

		IsLoading = false;
		StatusMessage = $"{RootNodes.Count} tipos carregados.";
	}

	public void UpdateSearchSuggestions(string query)
	{
		SearchSuggestions.Clear();

		if (string.IsNullOrWhiteSpace(query))
			return;

		var graph = _indexService.GetCurrentGraph();
		if (graph?.Nodes == null) return;

		var matches = graph.Nodes.Values
			.Where(n => n.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
			.OrderBy(n => n.Name.Length)
			.ThenBy(n => n.Name)
			.Take(20);

		foreach (var match in matches)
		{
			SearchSuggestions.Add(new GraphNodeViewModel(match, graph));
		}
	}

	[RelayCommand]
	public async Task NavigateToNode(GraphNodeViewModel? nodeVm)
	{
		if (nodeVm == null) return;

		var targetNode = nodeVm.InternalNode;
		var graph = _indexService.GetCurrentGraph();

		string filePath = graph.GetFilePath(targetNode.FileId);
		if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
		{
			var fileItem = _itemFactory.CreateWrapper(filePath, FileSystemItemType.File);
			_selectionService.SetSelection(fileItem);
		}

		await FocusNodeInTreeAsync(targetNode);
	}
	private async Task ExpandPathRecursive(GraphNodeViewModel parentVm, Stack<SymbolNode> pathStack, int targetId)
	{
		await Task.Delay(50);

		if (pathStack.Count == 0)
		{
			var targetVm = parentVm.Children.FirstOrDefault(c => c != null && c.InternalNode.Id == targetId);
			if (targetVm != null)
			{
				targetVm.IsSelected = true;
			}
			return;
		}

		var nextSymbol = pathStack.Pop();
		var childVm = parentVm.Children.FirstOrDefault(c => c != null && c.InternalNode.Id == nextSymbol.Id);

		if (childVm != null)
		{
			childVm.IsExpanded = true;
			childVm.EnsureChildrenLoaded();
			await ExpandPathRecursive(childVm, pathStack, targetId);
		}
	}



	public void ResetVisibility()
	{
		foreach (var root in RootNodes)
		{
			root.SetVisibilityRecursive(true);
			root.IsExpanded = false; // Opcional: Colapsar tudo ao resetar
		}
	}

	private async Task FocusNodeInTreeAsync(SymbolNode target)
	{
		// 1. Identificar o "Caminho Dourado" (IDs dos ancestrais)
		var pathIds = new HashSet<int>();
		var current = target;
		while (current != null)
		{
			pathIds.Add(current.Id);
			current = current.Parent;
		}

		// 2. Iterar sobre as Raízes
		foreach (var rootVm in RootNodes)
		{
			if (pathIds.Contains(rootVm.InternalNode.Id))
			{
				// É parte do caminho: Mostra e processa filhos
				rootVm.IsVisible = true;
				rootVm.IsExpanded = true;
				rootVm.EnsureChildrenLoaded();

				await FilterChildrenRecursive(rootVm, pathIds, target.Id);
			}
			else
			{
				// Não é parente: ESCONDE
				rootVm.IsVisible = false;
			}
		}
	}

	private async Task FilterChildrenRecursive(GraphNodeViewModel parentVm, HashSet<int> pathIds, int targetId)
	{
		await Task.Delay(20); // Pequeno respiro para a UI

		foreach (var childVm in parentVm.Children)
		{
			if (childVm == null) continue;

			if (pathIds.Contains(childVm.InternalNode.Id))
			{
				// É um ancestral intermediário (ex: Pasta ou Classe pai)
				childVm.IsVisible = true;
				childVm.IsExpanded = true;
				childVm.EnsureChildrenLoaded();
				await FilterChildrenRecursive(childVm, pathIds, targetId);
			}
			else if (childVm.InternalNode.Id == targetId)
			{
				// É O ALVO
				childVm.IsVisible = true;
				childVm.IsSelected = true;
				childVm.IsExpanded = true; // Opcional: abrir o alvo para ver seus membros

				// Opcional: Se quiser ver os filhos do alvo (ex: métodos da classe encontrada)
				// descomente a linha abaixo:
				// childVm.SetVisibilityRecursive(true); 
			}
			else
			{
				// É um irmão irrelevante (ex: outra classe na mesma pasta)
				childVm.IsVisible = false;
			}
		}
	}
}