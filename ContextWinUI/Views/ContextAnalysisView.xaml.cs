using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ContextWinUI.Views;

public sealed partial class ContextAnalysisView : UserControl
{
	private readonly List<string> _standardTags = new() { "Importante", "Revisar", "Documentação", "Bug", "Refatorar" };

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

	// --- LÓGICA DE LOTE (Detecta se veio da Tree ou da List) ---

	private void OnTagMenuOpening(object sender, object e)
	{
		if (sender is MenuFlyout flyout && flyout.Target.DataContext is FileSystemItem rightClickedItem)
		{
			flyout.Items.Clear();

			List<FileSystemItem> targetItems = new();

			// Pega itens selecionados de ambos os controles
			// (Nota: Em WinUI, só um deles estará ativo visualmente por vez, mas a propriedade SelectedItems pode reter estado.
			// A verificação 'Contains' resolve isso).
			var treeSelection = AnalysisTreeView.SelectedItems.Cast<FileSystemItem>().ToList();
			var listSelection = AnalysisListView.SelectedItems.Cast<FileSystemItem>().ToList();

			if (treeSelection.Contains(rightClickedItem))
			{
				targetItems = treeSelection;
			}
			else if (listSelection.Contains(rightClickedItem))
			{
				targetItems = listSelection;
			}
			else
			{
				targetItems.Add(rightClickedItem);
			}

			string headerText = targetItems.Count > 1 ? $"Nova Tag ({targetItems.Count} itens)..." : "Nova Tag...";

			var newTagItem = new MenuFlyoutItem { Text = headerText, Icon = new FontIcon { Glyph = "\uE710" } };
			newTagItem.Click += (s, args) => _ = ContextViewModel.TagService.PromptAndAddTagToBatchAsync(targetItems, this.XamlRoot);
			flyout.Items.Add(newTagItem);

			flyout.Items.Add(new MenuFlyoutSeparator());

			var allTagsDisplay = _standardTags.Union(targetItems.SelectMany(x => x.SharedState.Tags)).Distinct().OrderBy(x => x).ToList();

			foreach (var tag in allTagsDisplay)
			{
				var isChecked = targetItems.All(i => i.SharedState.Tags.Contains(tag));

				var toggleItem = new ToggleMenuFlyoutItem
				{
					Text = tag,
					IsChecked = isChecked
				};

				toggleItem.Click += (s, args) =>
				{
					ContextViewModel.TagService.BatchToggleTag(targetItems, tag);
				};

				flyout.Items.Add(toggleItem);
			}

			if (targetItems.Any(i => i.SharedState.Tags.Any()))
			{
				flyout.Items.Add(new MenuFlyoutSeparator());
				var clearItem = new MenuFlyoutItem { Text = "Limpar Tags", Icon = new FontIcon { Glyph = "\uE74D" } };
				clearItem.Click += (s, args) =>
				{
					foreach (var item in targetItems)
						ContextViewModel.TagService.ClearTags(item.SharedState.Tags);
				};
				flyout.Items.Add(clearItem);
			}
		}
	}
}