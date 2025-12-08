using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ContextWinUI.Views;

public sealed partial class FileExplorerView : UserControl
{
	public static readonly DependencyProperty ExplorerViewModelProperty =
		DependencyProperty.Register(nameof(ExplorerViewModel), typeof(FileExplorerViewModel), typeof(FileExplorerView), new PropertyMetadata(null));

	public FileExplorerViewModel ExplorerViewModel
	{
		get => (FileExplorerViewModel)GetValue(ExplorerViewModelProperty);
		set => SetValue(ExplorerViewModelProperty, value);
	}

	public static readonly DependencyProperty SelectionViewModelProperty =
		DependencyProperty.Register(nameof(SelectionViewModel), typeof(FileSelectionViewModel), typeof(FileExplorerView), new PropertyMetadata(null));

	public FileSelectionViewModel SelectionViewModel
	{
		get => (FileSelectionViewModel)GetValue(SelectionViewModelProperty);
		set => SetValue(SelectionViewModelProperty, value);
	}

	public event EventHandler<FileSystemItem>? FileSelected;

	public FileExplorerView()
	{
		this.InitializeComponent();
	}

	// --- CORREÇÃO: Garante atualização instantânea da contagem ---
	private void OnCheckBoxClick(object sender, RoutedEventArgs e)
	{
		if (sender is CheckBox chk && chk.DataContext is FileSystemItem item)
		{
			// Força a atualização do Model
			item.IsChecked = chk.IsChecked ?? false;

			// Recalcula contagem no ViewModel
			SelectionViewModel?.RecalculateSelection();
		}
	}
	// -----------------------------------------------------------

	private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		if (args.InvokedItem is FileSystemItem item)
		{
			ExplorerViewModel.SelectFile(item);
			FileSelected?.Invoke(this, item);
		}
	}

	private void TreeView_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
	{
		if (args.Item is FileSystemItem item && ExplorerViewModel.ExpandItemCommand.CanExecute(item))
		{
			ExplorerViewModel.ExpandItemCommand.Execute(item);
		}
	}

	private void TreeView_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
	{
		if (args.Item is FileSystemItem item) item.IsExpanded = false;
	}
}