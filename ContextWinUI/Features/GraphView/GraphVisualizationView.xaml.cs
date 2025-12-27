using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ContextWinUI.Features.GraphView;

public sealed partial class GraphVisualizationView : UserControl
{
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

	private void ActionButton_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button btn && btn.DataContext is ContextActionViewModel actionVm)
		{
			// Define qual a ação escolhida para este bloco específico
			actionVm.SelectedAction = btn.Content.ToString() ?? "V";

			// Força o ViewModel a atualizar a string do código unificado
			ViewModel?.RefreshUnifiedView();
		}
	}
}