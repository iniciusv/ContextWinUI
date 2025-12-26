using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ContextWinUI.ViewModels;

namespace ContextWinUI.Features.GraphView;

public sealed partial class GraphLegendControl : UserControl
{
	// Propriedade de Dependência para permitir Binding no XAML Pai
	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(
			nameof(ViewModel),
			typeof(GraphVisualizationViewModel),
			typeof(GraphLegendControl),
			new PropertyMetadata(null));

	public GraphVisualizationViewModel ViewModel
	{
		get => (GraphVisualizationViewModel)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	public GraphLegendControl()
	{
		this.InitializeComponent();
	}

}