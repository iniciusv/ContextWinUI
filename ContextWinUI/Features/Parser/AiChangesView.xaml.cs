using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ContextWinUI.ViewModels;

// ARQUIVO: Views/AiChangesView.xaml.cs
namespace ContextWinUI.Views;

public sealed partial class AiChangesView : UserControl
{
	// Registra a DependencyProperty para permitir binding no XAML pai (MainWindow)
	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(
			nameof(ViewModel),
			typeof(AiChangesViewModel),
			typeof(AiChangesView),
			new PropertyMetadata(null));

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