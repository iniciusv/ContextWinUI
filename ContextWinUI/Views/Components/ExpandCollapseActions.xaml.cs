// ==================== C:\Users\vinic\source\repos\ContextWinUI\ContextWinUI\Views\Components\ExpandCollapseActions.xaml.cs ====================

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace ContextWinUI.Views.Components;

public sealed partial class ExpandCollapseActions : UserControl
{
	public static readonly DependencyProperty ExpandAllCommandProperty =
		DependencyProperty.Register(nameof(ExpandAllCommand), typeof(ICommand), typeof(ExpandCollapseActions), new PropertyMetadata(null));

	public ICommand ExpandAllCommand
	{
		get => (ICommand)GetValue(ExpandAllCommandProperty);
		set => SetValue(ExpandAllCommandProperty, value);
	}

	public static readonly DependencyProperty CollapseAllCommandProperty =
		DependencyProperty.Register(nameof(CollapseAllCommand), typeof(ICommand), typeof(ExpandCollapseActions), new PropertyMetadata(null));

	public ICommand CollapseAllCommand
	{
		get => (ICommand)GetValue(CollapseAllCommandProperty);
		set => SetValue(CollapseAllCommandProperty, value);
	}

	public ExpandCollapseActions()
	{
		this.InitializeComponent();
	}
}