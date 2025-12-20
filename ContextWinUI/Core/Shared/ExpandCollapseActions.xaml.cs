using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace ContextWinUI.Views.Components;

public sealed partial class ExpandCollapseActions : UserControl
{
	public ExpandCollapseActions()
	{
		this.InitializeComponent();
	}

	// =========================================================
	// DEPENDENCY PROPERTIES (A "cola" para o Binding funcionar)
	// =========================================================

	public static readonly DependencyProperty SyncFocusCommandProperty =
		DependencyProperty.Register(nameof(SyncFocusCommand), typeof(ICommand), typeof(ExpandCollapseActions), new PropertyMetadata(null));

	public ICommand SyncFocusCommand
	{
		get => (ICommand)GetValue(SyncFocusCommandProperty);
		set => SetValue(SyncFocusCommandProperty, value);
	}

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

}