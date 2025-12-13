using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Views;

public sealed partial class FileExplorerView : UserControl
{
	// Tags padrão sugeridas
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

	// --- LÓGICA DE UI E CONVERSORES VISUAIS ---

	public static Brush GetIconColor(bool isDirectory)
	{
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

	// --- GERENCIAMENTO DE TAGS DINÂMICO ---

	private void OnTagMenuOpening(object sender, object e)
	{
		if (sender is MenuFlyout flyout && flyout.Target.DataContext is FileSystemItem item)
		{
			// 1. Limpa itens anteriores para reconstruir
			flyout.Items.Clear();

			// 2. Opção fixa: Nova Tag Customizada
			var newTagItem = new MenuFlyoutItem { Text = "Nova Tag...", Icon = new FontIcon { Glyph = "\uE710" } };
			newTagItem.Click += (s, args) => _ = ExplorerViewModel.TagService.PromptAndAddTagAsync(item.SharedState.Tags, this.XamlRoot);
			flyout.Items.Add(newTagItem);

			flyout.Items.Add(new MenuFlyoutSeparator());

			// 3. Constrói lista: Tags Padrão + Tags que o item já tem (Union remove duplicatas)
			var allTagsDisplay = _standardTags.Union(item.SharedState.Tags).OrderBy(x => x).ToList();

			foreach (var tag in allTagsDisplay)
			{
				// Verifica se o item possui essa tag
				var isChecked = item.SharedState.Tags.Contains(tag);

				// Cria um item de menu que funciona como Checkbox
				var toggleItem = new ToggleMenuFlyoutItem
				{
					Text = tag,
					IsChecked = isChecked
				};

				// Ação: Toggle (Adicionar/Remover)
				toggleItem.Click += (s, args) =>
				{
					ExplorerViewModel.TagService.ToggleTag(item.SharedState.Tags, tag);
				};

				flyout.Items.Add(toggleItem);
			}

			// 4. Opção de Limpar (só se tiver tags)
			if (item.SharedState.Tags.Any())
			{
				flyout.Items.Add(new MenuFlyoutSeparator());
				var clearItem = new MenuFlyoutItem { Text = "Limpar Tags", Icon = new FontIcon { Glyph = "\uE74D" } };
				clearItem.Click += (s, args) => ExplorerViewModel.TagService.ClearTags(item.SharedState.Tags);
				flyout.Items.Add(clearItem);
			}
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