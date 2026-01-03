using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

	// Handler do evento de clique no TreeView
	private void OnItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		// Verifica se o item clicado é do tipo correto
		if (args.InvokedItem is GraphNodeViewModel nodeVm)
		{
			// Dispara o comando de navegação no ViewModel
			ViewModel.NavigateToNodeCommand.Execute(nodeVm);
		}
	}
}