using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace ContextWinUI.Features.Tagging;

public static class TagMenuBuilder
{
	private static readonly List<string> _standardTags = new() { "Importante", "Revisar", "Documentação", "Bug", "Refatorar" };

	// Cores extraídas do FileExplorerView original
	private static readonly List<Color> _paletteColors = new()
	{
		Color.FromArgb(255, 255, 82, 82), Color.FromArgb(255, 255, 64, 129), Color.FromArgb(255, 224, 64, 251),
		Color.FromArgb(255, 124, 77, 255), Color.FromArgb(255, 83, 109, 254), Color.FromArgb(255, 68, 138, 255),
		Color.FromArgb(255, 64, 196, 255), Color.FromArgb(255, 24, 255, 255), Color.FromArgb(255, 100, 255, 218),
		Color.FromArgb(255, 105, 240, 174), Color.FromArgb(255, 178, 255, 89), Color.FromArgb(255, 238, 255, 65),
		Color.FromArgb(255, 255, 255, 0), Color.FromArgb(255, 255, 215, 64), Color.FromArgb(255, 255, 171, 64),
		Color.FromArgb(255, 255, 110, 64)
	};

	public static void BuildMenu(
		MenuFlyout flyout,
		IEnumerable<FileSystemItem> targetItems,
		ITagManagementUiService tagService,
		XamlRoot xamlRoot)
	{
		flyout.Items.Clear();
		var itemsList = targetItems.ToList();
		if (!itemsList.Any()) return;

		var firstItem = itemsList.First();

		// 1. Opção Ignorar (Apenas para diretórios individuais)
		if (itemsList.Count == 1 && firstItem.IsDirectory)
		{
			var chkIgnore = new ToggleMenuFlyoutItem
			{
				Text = "Ignorar Pasta",
				IsChecked = firstItem.SharedState.IsIgnored
			};
			chkIgnore.Click += (s, a) => firstItem.SharedState.IsIgnored = !firstItem.SharedState.IsIgnored;
			flyout.Items.Add(chkIgnore);
			flyout.Items.Add(new MenuFlyoutSeparator());
		}

		// 2. Criar Nova Tag
		string headerText = itemsList.Count > 1 ? $"Nova Tag ({itemsList.Count} itens)..." : "Nova Tag...";
		var newTagItem = new MenuFlyoutItem { Text = headerText, Icon = new FontIcon { Glyph = "\uE710" } };
		newTagItem.Click += (s, args) => _ = tagService.PromptAndAddTagToBatchAsync(itemsList, xamlRoot);
		flyout.Items.Add(newTagItem);

		flyout.Items.Add(new MenuFlyoutSeparator());

		// 3. Listagem de Tags com Color Picker
		// Unifica tags padrão com as tags que os itens já possuem
		var allTagsDisplay = _standardTags.Union(itemsList.SelectMany(x => x.SharedState.Tags)).Distinct().OrderBy(x => x).ToList();

		foreach (var tag in allTagsDisplay)
		{


			var isChecked = itemsList.All(i => i.SharedState.Tags.Contains(tag));
			var toggleItem = new ToggleMenuFlyoutItem { Text = tag, IsChecked = isChecked };

			// Opcional: Adicionar ícone colorido na tag
			var tagColor = TagColorService.Instance.GetColorForTag(tag);
			toggleItem.Icon = new FontIcon { Glyph = "\uE91F", Foreground = new SolidColorBrush(tagColor) }; // Bullet point

			toggleItem.Click += (s, args) => tagService.BatchToggleTag(itemsList, tag);
			flyout.Items.Add(toggleItem);
		}

		// 4. Limpar Tags
		if (itemsList.Any(i => i.SharedState.Tags.Any()))
		{
			flyout.Items.Add(new MenuFlyoutSeparator());
			var clearItem = new MenuFlyoutItem { Text = "Limpar Tags", Icon = new FontIcon { Glyph = "\uE74D" } };
			clearItem.Click += (s, args) => { foreach (var item in itemsList) tagService.ClearTags(item.SharedState.Tags); };
			flyout.Items.Add(clearItem);
		}
	}

	// Sobrecarga para lidar com o Flyout genérico do FileExplorerView que permite controles complexos (Color Picker)
	public static void BuildComplexMenu(
		StackPanel rootPanel,
		IEnumerable<FileSystemItem> targetItems,
		ITagManagementUiService tagService,
		XamlRoot xamlRoot,
		Action closeFlyoutAction)
	{
		rootPanel.Children.Clear();
		var itemsList = targetItems.ToList();
		if (!itemsList.Any()) return;

		var firstItem = itemsList.First();

		// 1. Ignorar
		if (itemsList.Count == 1 && firstItem.IsDirectory)
		{
			var chk = new CheckBox { Content = "Ignorar Pasta", IsChecked = firstItem.SharedState.IsIgnored, Margin = new Thickness(4, 2, 4, 2) };
			chk.Click += (s, a) => firstItem.SharedState.IsIgnored = (chk.IsChecked == true);
			rootPanel.Children.Add(chk);
			rootPanel.Children.Add(new MenuFlyoutSeparator());
		}

		// 2. Nova Tag
		var btnNew = new Button { Content = "Nova Tag...", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0) };
		btnNew.Click += (s, a) => { closeFlyoutAction(); _ = tagService.PromptAndAddTagToBatchAsync(itemsList, xamlRoot); };
		rootPanel.Children.Add(btnNew);
		rootPanel.Children.Add(new MenuFlyoutSeparator());

		// 3. Lista com Color Picker
		var allTags = _standardTags.Union(itemsList.SelectMany(x => x.SharedState.Tags)).Distinct().OrderBy(x => x);
		foreach (var tag in allTags)
		{
			var row = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, new ColumnDefinition { Width = GridLength.Auto } }, Margin = new Thickness(4, 2, 4, 2) };

			var chk = new CheckBox { Content = tag, IsChecked = itemsList.All(i => i.SharedState.Tags.Contains(tag)), MinWidth = 120 };
			chk.Click += (s, a) => tagService.BatchToggleTag(itemsList, tag);
			Grid.SetColumn(chk, 0);

			// Color Picker Button
			var colorBtn = new Button { Width = 22, Height = 22, Padding = new Thickness(0), Background = new SolidColorBrush(TagColorService.Instance.GetColorForTag(tag)), CornerRadius = new CornerRadius(4) };

			// Palette Flyout logic
			var pFlyout = new Flyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.RightEdgeAlignedTop };
			var pGrid = new VariableSizedWrapGrid { MaximumRowsOrColumns = 4, Orientation = Orientation.Horizontal, ItemHeight = 28, ItemWidth = 28 };

			foreach (var c in _paletteColors)
			{
				var swatch = new Button { Width = 24, Height = 24, Background = new SolidColorBrush(c), CornerRadius = new CornerRadius(12), Margin = new Thickness(1) };
				swatch.Click += (s, a) => {
					TagColorService.Instance.SetColorForTag(tag, c);
					colorBtn.Background = new SolidColorBrush(c);
					pFlyout.Hide();
				};
				pGrid.Children.Add(swatch);
			}
			pFlyout.Content = pGrid;
			colorBtn.Flyout = pFlyout;
			Grid.SetColumn(colorBtn, 1);

			row.Children.Add(chk);
			row.Children.Add(colorBtn);
			rootPanel.Children.Add(row);
		}

		// 4. Limpar
		if (itemsList.Any(i => i.SharedState.Tags.Any()))
		{
			rootPanel.Children.Add(new MenuFlyoutSeparator());
			var btnClear = new Button { Content = "Limpar Tags", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), Foreground = new SolidColorBrush(Colors.Red) };
			btnClear.Click += (s, a) => { foreach (var item in itemsList) tagService.ClearTags(item.SharedState.Tags); closeFlyoutAction(); };
			rootPanel.Children.Add(btnClear);
		}
	}
}