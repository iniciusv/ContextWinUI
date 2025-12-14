using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Views;

public sealed partial class FileExplorerView : UserControl
{

	private readonly List<string> _standardTags = new() { "Importante", "Revisar", "Documentação", "Bug", "Refatorar" };

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

	// Assinatura atualizada para receber o status IsIgnored
	public static Brush GetIconColor(bool isDirectory, bool isIgnored)
	{
		if (isIgnored)
		{
			return new SolidColorBrush(Colors.Red);
		}

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

	private void OnTagMenuOpening(object sender, object e)
	{
		if (sender is MenuFlyout flyout && flyout.Target.DataContext is FileSystemItem item)
		{

			flyout.Items.Clear();

			// --- Menu Ignorar Pasta (Novo) ---
			if (item.IsDirectory)
			{
				var ignoreItem = new ToggleMenuFlyoutItem
				{
					Text = "Ignorar Pasta",
					IsChecked = item.SharedState.IsIgnored,
					Icon = new FontIcon { Glyph = "\uE74D" } // Delete icon style
				};

				ignoreItem.Click += (s, args) =>
				{
					item.SharedState.IsIgnored = !item.SharedState.IsIgnored;
				};

				flyout.Items.Add(ignoreItem);
				flyout.Items.Add(new MenuFlyoutSeparator());
			}
			// ---------------------------------

			var newTagItem = new MenuFlyoutItem { Text = "Nova Tag...", Icon = new FontIcon { Glyph = "\uE710" } };
			newTagItem.Click += (s, args) => _ = ExplorerViewModel.TagService.PromptAndAddTagAsync(item.SharedState.Tags, this.XamlRoot);
			flyout.Items.Add(newTagItem);

			flyout.Items.Add(new MenuFlyoutSeparator());

			var allTagsDisplay = _standardTags.Union(item.SharedState.Tags).OrderBy(x => x).ToList();

			foreach (var tag in allTagsDisplay)
			{

				var isChecked = item.SharedState.Tags.Contains(tag);

				var toggleItem = new ToggleMenuFlyoutItem
				{
					Text = tag,
					IsChecked = isChecked
				};

				toggleItem.Click += (s, args) =>
				{
					ExplorerViewModel.TagService.ToggleTag(item.SharedState.Tags, tag);
				};

				flyout.Items.Add(toggleItem);
			}

			if (item.SharedState.Tags.Any())
			{
				flyout.Items.Add(new MenuFlyoutSeparator());
				var clearItem = new MenuFlyoutItem { Text = "Limpar Tags", Icon = new FontIcon { Glyph = "\uE74D" } };
				clearItem.Click += (s, args) => ExplorerViewModel.TagService.ClearTags(item.SharedState.Tags);
				flyout.Items.Add(clearItem);
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