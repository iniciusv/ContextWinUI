using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ContextWinUI.ViewModels; // <--- ESTE USING É CRUCIAL PARA CORRIGIR O ERRO CS0246

namespace ContextWinUI.Views
{
	public sealed partial class GraphVisualizationView : UserControl
	{
		// Define a DependencyProperty para permitir binding no XAML pai
		public static readonly DependencyProperty ViewModelProperty =
			DependencyProperty.Register(
				nameof(ViewModel),
				typeof(GraphVisualizationViewModel),
				typeof(GraphVisualizationView),
				new PropertyMetadata(null));

		public GraphVisualizationViewModel ViewModel
		{
			get => (GraphVisualizationViewModel)GetValue(ViewModelProperty);
			set => SetValue(ViewModelProperty, value);
		}

		public GraphVisualizationView()
		{
			this.InitializeComponent();
		}
	}
}