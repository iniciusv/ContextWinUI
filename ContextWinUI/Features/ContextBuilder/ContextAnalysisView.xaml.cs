using ContextWinUI.Features.Tagging; // Importante
using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;

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

	private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		if (args.InvokedItem is FileSystemItem item)
		{
			ContextViewModel?.SelectFileForPreview(item);
		}
	}

	private void OnListViewItemClick(object sender, ItemClickEventArgs e)
	{
		if (e.ClickedItem is FileSystemItem item)
		{
			ContextViewModel?.SelectFileForPreview(item);
		}
	}

	private void OnTagMenuOpening(object sender, object e)
	{
		// View totalmente limpa. Lógica movida para Features.Tagging.TagMenuBuilder
		if (sender is MenuFlyout flyout && flyout.Target.DataContext is FileSystemItem rightClickedItem)
		{
			var targetItems = new List<FileSystemItem>();

			// Determina se aplica a múltiplos itens selecionados ou apenas ao clicado
			var treeSelection = AnalysisTreeView.SelectedItems.Cast<FileSystemItem>().ToList();
			var listSelection = AnalysisListView.SelectedItems.Cast<FileSystemItem>().ToList();
			var gitSelection = GitListView.SelectedItems.Cast<FileSystemItem>().ToList();

			if (treeSelection.Contains(rightClickedItem)) targetItems = treeSelection;
			else if (listSelection.Contains(rightClickedItem)) targetItems = listSelection;
			else if (gitSelection.Contains(rightClickedItem)) targetItems = gitSelection;
			else targetItems.Add(rightClickedItem);

			// Delega construção
			TagMenuBuilder.BuildMenu(flyout, targetItems, ContextViewModel.TagService, this.XamlRoot);
		}
	}
}