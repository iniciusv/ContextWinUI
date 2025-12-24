using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ContextWinUI.ViewModels;

namespace ContextWinUI.Views;

public sealed partial class AiChangesView : UserControl
{
	// Permite fazer binding no pai: <views:AiChangesView ViewModel="{x:Bind ...}"/>
	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(nameof(ViewModel), typeof(AiChangesViewModel), typeof(AiChangesView), new PropertyMetadata(null));

	public AiChangesViewModel ViewModel
	{
		get => (AiChangesViewModel)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	public AiChangesView()
	{
		this.InitializeComponent();
	}
}