using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

namespace ContextWinUI.Features.GraphAnalysis;

public partial class GraphNodeViewModel : ObservableObject
{
	private readonly SymbolNode _node;
	private readonly IDependencyGraph _graph;
	private bool _childrenLoaded = false;

	public SymbolNode InternalNode => _node;
	public string Name => _node.Name;
	public string Details => $"{_node.Type} (ID: {_node.Id})";

	public string IconGlyph => _node.Type switch
	{
		SymbolType.Class => "\uEA86",
		SymbolType.Interface => "\uE9CE",
		SymbolType.Method => "\uEA2C",
		SymbolType.Property => "\uEA19",
		SymbolType.Field => "\uEA1C",
		SymbolType.Constructor => "\uEA2C",
		_ => "\uE82D"
	};

	[ObservableProperty]
	private bool isExpanded;

	[ObservableProperty]
	private string extraInfo;

	[ObservableProperty]
	private bool isVisible = true;

	[ObservableProperty]
	private bool isSelected;

	public SolidColorBrush TitleBrush => IsSelected ? new SolidColorBrush(Colors.Orange) : new SolidColorBrush(Colors.White);

	public ObservableCollection<GraphNodeViewModel> Children { get; } = new();

	public GraphNodeViewModel(SymbolNode node, IDependencyGraph graph)
	{
		_node = node;
		_graph = graph;

		if ((_node.Children != null && _node.Children.Any()) ||
			(_node.OutgoingLinks != null && _node.OutgoingLinks.Any()))
		{
			Children.Add(null);
		}
	}

	partial void OnIsSelectedChanged(bool value)
	{
		OnPropertyChanged(nameof(TitleBrush));
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

	public void EnsureChildrenLoaded()
	{
		if (!_childrenLoaded) LoadChildren();
	}

	private void LoadChildren()
	{
		Children.Clear();

		if (_node.Children != null && _node.Children.Count > 0)
		{
			var sortedChildren = _node.Children.OrderBy(c => c.StartPosition);
			foreach (var childNode in sortedChildren)
			{
				Children.Add(new GraphNodeViewModel(childNode, _graph));
			}
		}

		if (_node.OutgoingLinks != null && _node.OutgoingLinks.Count > 0)
		{
			var uniqueLinks = _node.OutgoingLinks
				.GroupBy(l => l.TargetId)
				.Select(g => g.First());

			foreach (var link in uniqueLinks)
			{
				if (_graph.Nodes.TryGetValue(link.TargetId, out var targetNode))
				{
					if (_node.Children != null && _node.Children.Any(c => c.Id == targetNode.Id)) continue;

					var vm = new GraphNodeViewModel(targetNode, _graph);
					vm.ExtraInfo = $"-> {link.Type}";
					Children.Add(vm);
				}
			}
		}
		_childrenLoaded = true;
	}

	public void SetVisibilityRecursive(bool visible)
	{
		IsVisible = visible;

		// Se já carregou os filhos, propaga o estado
		if (_childrenLoaded)
		{
			foreach (var child in Children)
			{
				child?.SetVisibilityRecursive(visible);
			}
		}
	}
}