using ContextWinUI.Core.Shared;
using ContextWinUI.Features.Tagging; // Certifique-se que o TagMenuBuilder está visível
using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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

	public static Brush GetIconColor(bool isDirectory, bool isIgnored)
	{
		if (isIgnored) return new SolidColorBrush(Microsoft.UI.Colors.Red);
		return isDirectory
			? (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"]
			: (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
	}

	private void OnCheckBoxClick(object sender, RoutedEventArgs e)
	{
		if (sender is CheckBox chk && chk.DataContext is FileSystemItem item)
		{
			item.IsChecked = chk.IsChecked ?? false;
			SelectionViewModel?.RecalculateSelection();
		}
	}

	// --- CORREÇÃO AQUI ---
	private void OnTagMenuOpening(object sender, object e)
	{
		// Pegamos o item do contexto (o DataContext do target do Flyout)
		if (sender is Flyout flyout && flyout.Target.DataContext is FileSystemItem item)
		{
			// CORREÇÃO: Não usamos 'FlyoutContentGrid' pelo nome.
			// Acessamos o conteúdo do Flyout atual (sender).

			StackPanel rootPanel;

			// Se o conteúdo do Flyout for um Grid (conforme definimos no XAML), pegamos ele
			if (flyout.Content is Grid grid)
			{
				// Verifica se já criamos o StackPanel dentro do Grid
				if (grid.Children.Count == 0 || !(grid.Children[0] is StackPanel))
				{
					grid.Children.Clear();
					rootPanel = new StackPanel { Spacing = 4, MinWidth = 180, Padding = new Thickness(4) };
					grid.Children.Add(rootPanel);
				}
				else
				{
					rootPanel = (StackPanel)grid.Children[0];
				}

				// Chama o Builder
				TagMenuBuilder.BuildComplexMenu(
					rootPanel,
					new[] { item },
					ExplorerViewModel.TagService,
					this.XamlRoot,
					() => flyout.Hide());
			}
		}
	}

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
			ExplorerViewModel.ExpandItemCommand.Execute(item);
	}

	private void TreeView_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
	{
		if (args.Item is FileSystemItem item) item.IsExpanded = false;
	}
}