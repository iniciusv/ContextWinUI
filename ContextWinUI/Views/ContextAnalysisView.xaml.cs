using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

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
		// Damos um nome ao UserControl para o Binding ElementName no Checkbox funcionar dentro do Template
		this.Name = "RootAnalysisView";
	}

	private async void OnDeepAnalyzeClick(object sender, RoutedEventArgs e)
	{
		if (sender is Button btn && btn.Tag is FileSystemItem item)
		{
			await ContextViewModel.AnalyzeItemDepthCommand.ExecuteAsync(item);
		}
	}

	private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		if (args.InvokedItem is FileSystemItem item)
		{
			ContextViewModel.SelectFileForPreview(item);
		}
	}
}