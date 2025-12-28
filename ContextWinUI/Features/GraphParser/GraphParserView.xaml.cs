using ContextWinUI.Features.GraphParser.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ContextWinUI.Features.GraphParser.Views;

public sealed partial class GraphParserView : UserControl
{
	public GraphParserViewModel ViewModel
	{
		get => (GraphParserViewModel)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(
			nameof(ViewModel),
			typeof(GraphParserViewModel),
			typeof(GraphParserView),
			new PropertyMetadata(null, OnViewModelChanged)); // <--- Adicionamos um Callback

	// Este método roda sempre que o MainWindow conseguir injetar a ViewModel
	private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is GraphParserView view)
		{
			// Força o x:Bind a se atualizar com o novo valor
			view.Bindings.Update();
		}
	}

	public GraphParserView()
	{
		this.InitializeComponent();
	}
}