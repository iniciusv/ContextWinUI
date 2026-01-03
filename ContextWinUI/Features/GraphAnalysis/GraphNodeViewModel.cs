using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace ContextWinUI.Features.GraphAnalysis;

public partial class GraphNodeViewModel : ObservableObject
{
	private readonly SymbolNode _node;
	private readonly IDependencyGraph _graph;
	private bool _childrenLoaded = false;

	// --- NOVA PROPRIEDADE ---
	// Precisamos expor o nó interno para acessar o FileId na navegação
	public SymbolNode InternalNode => _node;
	// ------------------------

	public string Name => _node.Name;
	public string Details => $"{_node.Type} (ID: {_node.Id})";

	public string IconGlyph => _node.Type switch
	{
		SymbolType.Class => "\uEA86",
		SymbolType.Interface => "\uE9CE",
		SymbolType.Method => "\uEA2C",
		SymbolType.Property => "\uEA19",
		SymbolType.Field => "\uEA1C",
		SymbolType.Constructor => "\uEA2C", // Usando ícone de método para construtor
		_ => "\uE82D"
	};

	[ObservableProperty]
	private bool isExpanded;

	[ObservableProperty]
	private string extraInfo;

	public ObservableCollection<GraphNodeViewModel> Children { get; } = new();

	public GraphNodeViewModel(SymbolNode node, IDependencyGraph graph)
	{
		_node = node;
		_graph = graph;

		// Adiciona placeholder se tiver filhos ou links para permitir expansão
		if ((_node.Children != null && _node.Children.Any()) ||
			(_node.OutgoingLinks != null && _node.OutgoingLinks.Any()))
		{
			Children.Add(null);
		}
	}

	partial void OnIsExpandedChanged(bool value)
	{
		if (value && !_childrenLoaded)
		{
			if (App.MainWindow != null)
			{
				App.MainWindow.DispatcherQueue.TryEnqueue(() =>
				{
					LoadChildren();
				});
			}
		}
	}

	private void LoadChildren()
	{
		Children.Clear();

		// 1. Estrutura Interna (Métodos, Propriedades)
		if (_node.Children != null && _node.Children.Count > 0)
		{
			var sortedChildren = _node.Children.OrderBy(c => c.StartPosition);
			foreach (var childNode in sortedChildren)
			{
				Children.Add(new GraphNodeViewModel(childNode, _graph));
			}
		}

		// 2. Dependências Externas (Links)
		if (_node.OutgoingLinks != null && _node.OutgoingLinks.Count > 0)
		{
			var uniqueLinks = _node.OutgoingLinks
				.GroupBy(l => l.TargetId)
				.Select(g => g.First());

			foreach (var link in uniqueLinks)
			{
				if (_graph.Nodes.TryGetValue(link.TargetId, out var targetNode))
				{
					// Evita referência circular visual direta (filho apontando pra pai)
					if (_node.Children != null && _node.Children.Any(c => c.Id == targetNode.Id)) continue;

					var vm = new GraphNodeViewModel(targetNode, _graph);
					vm.ExtraInfo = $"-> {link.Type}";
					Children.Add(vm);
				}
			}
		}

		_childrenLoaded = true;
	}
}