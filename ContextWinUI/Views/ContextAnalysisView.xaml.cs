// ==================== C:\Users\vinic\source\repos\ContextWinUI\ContextWinUI\Views\ContextAnalysisView.xaml.cs ====================

using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ContextWinUI.Views;

public sealed partial class ContextAnalysisView : UserControl
{
	public static readonly DependencyProperty ContextViewModelProperty =
		DependencyProperty.Register(nameof(ContextViewModel), typeof(ContextAnalysisViewModel), typeof(ContextAnalysisView), new PropertyMetadata(null));

	public ContextAnalysisViewModel ContextViewModel
	{
		get => (ContextAnalysisViewModel)GetValue(ContextViewModelProperty);
		set => SetValue(ContextViewModelProperty, value);
	}

	public ContextAnalysisView()
	{
		this.InitializeComponent();
		this.Name = "RootAnalysisView";
	}

	// O método OnDeepAnalyzeClick foi removido pois agora usamos Command/CommandParameter no XAML

	private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		if (args.InvokedItem is FileSystemItem item)
		{
			ContextViewModel?.SelectFileForPreview(item);
		}
	}
}