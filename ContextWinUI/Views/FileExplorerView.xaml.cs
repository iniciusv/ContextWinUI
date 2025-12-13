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

	// --- LÓGICA DE UI E TAGS ---

	// Método estático para ser chamado pelo x:Bind na View
	public static Microsoft.UI.Xaml.Media.Brush GetIconColor(bool isDirectory)
	{
		return isDirectory
			? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"]
			: (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
	}

	private void OnCheckBoxClick(object sender, RoutedEventArgs e)
	{
		if (sender is CheckBox chk && chk.DataContext is FileSystemItem item)
		{
			item.IsChecked = chk.IsChecked ?? false;
			SelectionViewModel?.RecalculateSelection();
		}
	}

	// --- GERENCIAMENTO DE TAGS (MENU DE CONTEXTO) ---

	private void AddTag_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuFlyoutItem menuItem &&
			menuItem.DataContext is FileSystemItem item &&
			menuItem.Tag is string tag)
		{
			if (!item.SharedState.Tags.Contains(tag))
				item.SharedState.Tags.Add(tag);
		}
	}

	private async void AddNewTag_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is FileSystemItem item)
		{
			var textBox = new TextBox { PlaceholderText = "Ex: Precisa Refatorar" };
			var dialog = new ContentDialog
			{
				Title = "Nova Tag",
				Content = textBox,
				PrimaryButtonText = "Adicionar",
				CloseButtonText = "Cancelar",
				DefaultButton = ContentDialogButton.Primary,
				XamlRoot = this.XamlRoot // Importante para WinUI 3
			};

			var result = await dialog.ShowAsync();

			if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
			{
				var newTag = textBox.Text.Trim();
				if (!item.SharedState.Tags.Contains(newTag))
					item.SharedState.Tags.Add(newTag);
			}
		}
	}

	private void ClearTags_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is FileSystemItem item)
		{
			item.SharedState.Tags.Clear();
		}
	}

	// --- EVENTOS DO TREEVIEW ---

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