using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace ContextWinUI.Views.Components;

public sealed partial class SearchBar : UserControl
{
	public static readonly DependencyProperty SearchCommandProperty =
		DependencyProperty.Register(nameof(SearchCommand), typeof(ICommand), typeof(SearchBar), new PropertyMetadata(null));

	public ICommand SearchCommand
	{
		get => (ICommand)GetValue(SearchCommandProperty);
		set => SetValue(SearchCommandProperty, value);
	}

	public SearchBar()
	{
		this.InitializeComponent();
	}

	private void OnTextChanged(object sender, TextChangedEventArgs e)
	{
		if (sender is TextBox tb && SearchCommand != null)
		{
			if (SearchCommand.CanExecute(tb.Text))
			{
				SearchCommand.Execute(tb.Text);
			}
		}
	}
}