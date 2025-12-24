using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Windows.Input;
using Windows.System;

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

	public static readonly DependencyProperty SubmitCommandProperty =
		DependencyProperty.Register(nameof(SubmitCommand), typeof(ICommand), typeof(SearchBar), new PropertyMetadata(null));

	public ICommand SubmitCommand
	{
		get => (ICommand)GetValue(SubmitCommandProperty);
		set => SetValue(SubmitCommandProperty, value);
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
	private void OnKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key == VirtualKey.Enter && sender is TextBox tb)
		{
			if (SubmitCommand != null && SubmitCommand.CanExecute(tb.Text))
			{
				SubmitCommand.Execute(tb.Text);
				e.Handled = true;
			}
		}
	}
}