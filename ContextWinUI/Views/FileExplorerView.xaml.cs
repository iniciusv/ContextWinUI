using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ContextWinUI.Views;

public sealed partial class FileExplorerView : UserControl
{
	// Dependency Properties para Binding no MainWindow
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

	// Evento para comunicar a seleção ao pai (MainWindow/MainViewModel)
	public event EventHandler<FileSystemItem>? FileSelected;

	public FileExplorerView()
	{
		this.InitializeComponent();
	}

	private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		if (args.InvokedItem is FileSystemItem item)
		{
			// Seleciona no VM do Explorer
			ExplorerViewModel.SelectFile(item);
			// Dispara evento para quem estiver ouvindo (MainWindow) carregar o conteúdo
			FileSelected?.Invoke(this, item);
		}
	}

	private void TreeView_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
	{
		if (args.Item is FileSystemItem item)
		{
			// CORREÇÃO: Usar Execute em vez de ExecuteAsync
			// O RelayCommand gerencia a Task internamente
			if (ExplorerViewModel.ExpandItemCommand.CanExecute(item))
			{
				ExplorerViewModel.ExpandItemCommand.Execute(item);
			}
		}
	}

	private void TreeView_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
	{
		if (args.Item is FileSystemItem item)
		{
			item.IsExpanded = false;
		}
	}
}