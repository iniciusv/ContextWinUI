using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ContextWinUI.Views;

public sealed partial class PrePromptView : UserControl
{
	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(nameof(ViewModel), typeof(PrePromptViewModel), typeof(PrePromptView), new PropertyMetadata(null));

	public PrePromptViewModel ViewModel
	{
		get => (PrePromptViewModel)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	public PrePromptView()
	{
		this.InitializeComponent();
	}
}