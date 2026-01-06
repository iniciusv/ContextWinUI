using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace ContextWinUI.Features.GraphAnalysis;

public sealed partial class SemanticGraphView : UserControl
{
	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(
			nameof(ViewModel),
			typeof(SemanticGraphViewModel),
			typeof(SemanticGraphView),
			new PropertyMetadata(null));

	public SemanticGraphViewModel ViewModel
	{
		get => (SemanticGraphViewModel)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	public SemanticGraphView()
	{
		this.InitializeComponent();
	}

	private void OnItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		if (args.InvokedItem is GraphNodeViewModel nodeVm)
		{
			ViewModel.NavigateToNodeCommand.Execute(nodeVm);
		}
	}

	private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
	{
		if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
		{
			ViewModel?.UpdateSearchSuggestions(sender.Text);
		}
	}

	private void OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
	{
		if (args.SelectedItem is GraphNodeViewModel selectedNode)
		{
			sender.Text = selectedNode.Name;
		}
	}

	private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
	{
		if (args.ChosenSuggestion is GraphNodeViewModel selectedNode)
		{
			ViewModel?.NavigateToNodeCommand.Execute(selectedNode);
		}
		else if (!string.IsNullOrEmpty(args.QueryText))
		{
			var firstMatch = ViewModel?.SearchSuggestions.FirstOrDefault();
			if (firstMatch != null)
			{
				ViewModel.NavigateToNodeCommand.Execute(firstMatch);
			}
		}
	}
}