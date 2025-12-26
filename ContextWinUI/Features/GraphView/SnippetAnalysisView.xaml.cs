using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ContextWinUI.ViewModels;

namespace ContextWinUI.Features.GraphView
{
	public sealed partial class SnippetAnalysisView : UserControl
	{
		public static readonly DependencyProperty ViewModelProperty =
			DependencyProperty.Register(nameof(ViewModel), typeof(GraphVisualizationViewModel), typeof(SnippetAnalysisView), new PropertyMetadata(null));

		public GraphVisualizationViewModel ViewModel
		{
			get => (GraphVisualizationViewModel)GetValue(ViewModelProperty);
			set => SetValue(ViewModelProperty, value);
		}

		public SnippetAnalysisView() => this.InitializeComponent();
	}
}