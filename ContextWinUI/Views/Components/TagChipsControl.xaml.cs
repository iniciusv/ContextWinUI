using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

namespace ContextWinUI.Views.Components;

public sealed partial class TagChipsControl : UserControl
{
	// Dependency Property para permitir Binding no XAML pai
	public static readonly DependencyProperty TagsSourceProperty =
		DependencyProperty.Register(nameof(TagsSource), typeof(ObservableCollection<string>), typeof(TagChipsControl), new PropertyMetadata(null));

	public ObservableCollection<string> TagsSource
	{
		get => (ObservableCollection<string>)GetValue(TagsSourceProperty);
		set => SetValue(TagsSourceProperty, value);
	}

	public TagChipsControl()
	{
		this.InitializeComponent();
	}
}