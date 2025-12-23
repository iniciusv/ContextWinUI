using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;

using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace ContextWinUI.Core.Helpers;

public static class TagMenuBuilder
{
	private static readonly List<string> _standardTags = new() { "Importante", "Revisar", "Documentação", "Bug", "Refatorar" };

	private static readonly List<Color> _paletteColors = new()
	{
		Color.FromArgb(255, 255, 82, 82), Color.FromArgb(255, 255, 64, 129), Color.FromArgb(255, 224, 64, 251),
		Color.FromArgb(255, 124, 77, 255), Color.FromArgb(255, 83, 109, 254), Color.FromArgb(255, 68, 138, 255),
		Color.FromArgb(255, 64, 196, 255), Color.FromArgb(255, 24, 255, 255), Color.FromArgb(255, 100, 255, 218),
		Color.FromArgb(255, 105, 240, 174), Color.FromArgb(255, 178, 255, 89), Color.FromArgb(255, 238, 255, 65),
		Color.FromArgb(255, 255, 255, 0), Color.FromArgb(255, 255, 215, 64), Color.FromArgb(255, 255, 171, 64),
		Color.FromArgb(255, 255, 110, 64)
	};

	public static void BuildAndShowMenu(FrameworkElement targetElement, List<FileSystemItem> targetItems, ITagManagementUiService tagService, XamlRoot xamlRoot)
	{
		var flyout = new Flyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };

		// Passa a ação de fechar o próprio flyout criado aqui
		flyout.Content = BuildContent(targetItems, tagService, xamlRoot, () => flyout.Hide());

		flyout.ShowAt(targetElement);
	}

	public static StackPanel BuildContent(
		List<FileSystemItem> targetItems,
		ITagManagementUiService tagService,
		XamlRoot xamlRoot,
		Action closeFlyoutAction)
	{
		if (targetItems == null || !targetItems.Any()) return new StackPanel();

		var rootPanel = new StackPanel { Spacing = 4, MinWidth = 200, Padding = new Thickness(4) };

		// 1. Cabeçalho / Informação de Lote
		if (targetItems.Count > 1)
		{
			rootPanel.Children.Add(new TextBlock
			{
				Text = $"Editando {targetItems.Count} itens",
				FontSize = 10,
				Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
				Margin = new Thickness(4, 0, 0, 4)
			});
		}

		// 2. Opção de Ignorar (Apenas se for 1 item e for pasta)
		if (targetItems.Count == 1 && targetItems[0].IsDirectory)
		{
			var item = targetItems[0];
			var chkIgnore = new CheckBox
			{
				Content = "Ignorar Pasta",
				IsChecked = item.SharedState.IsIgnored,
				Margin = new Thickness(4, 2, 4, 2)
			};
			chkIgnore.Click += (s, args) => item.SharedState.IsIgnored = (chkIgnore.IsChecked == true);
			rootPanel.Children.Add(chkIgnore);
			rootPanel.Children.Add(new MenuFlyoutSeparator());
		}

		// 3. Botão Nova Tag
		var btnNewTag = new Button
		{
			Content = "Nova Tag...",
			HorizontalAlignment = HorizontalAlignment.Stretch,
			HorizontalContentAlignment = HorizontalAlignment.Left,
			Background = new SolidColorBrush(Colors.Transparent),
			BorderThickness = new Thickness(0)
		};
		btnNewTag.Click += (s, args) =>
		{
			closeFlyoutAction?.Invoke();
			_ = tagService.PromptAndAddTagToBatchAsync(targetItems, xamlRoot);
		};
		rootPanel.Children.Add(btnNewTag);
		rootPanel.Children.Add(new MenuFlyoutSeparator());

		// 4. Lista de Tags
		var existingTags = targetItems.SelectMany(x => x.SharedState.Tags).Distinct();
		var allTagsDisplay = _standardTags.Union(existingTags).OrderBy(x => x).ToList();

		foreach (var tag in allTagsDisplay)
		{
			var rowGrid = CreateTagRow(tag, targetItems, tagService, closeFlyoutAction);
			rootPanel.Children.Add(rowGrid);
		}

		// 5. Opção Limpar Tags
		if (targetItems.Any(i => i.SharedState.Tags.Any()))
		{
			rootPanel.Children.Add(new MenuFlyoutSeparator());
			var btnClear = new Button
			{
				Content = "Limpar Tags",
				HorizontalAlignment = HorizontalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Left,
				Background = new SolidColorBrush(Colors.Transparent),
				BorderThickness = new Thickness(0),
				Foreground = new SolidColorBrush(Colors.Red)
			};
			btnClear.Click += (s, args) =>
			{
				foreach (var item in targetItems) tagService.ClearTags(item.SharedState.Tags);
				closeFlyoutAction?.Invoke();
			};
			rootPanel.Children.Add(btnClear);
		}

		return rootPanel;
	}

	private static Grid CreateTagRow(string tag, List<FileSystemItem> targetItems, ITagManagementUiService tagService, Action closeAction)
	{
		var grid = new Grid
		{
			ColumnDefinitions =
			{
				new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
				new ColumnDefinition { Width = GridLength.Auto }
			},
			Margin = new Thickness(4, 2, 4, 2)
		};

		bool isChecked = targetItems.All(i => i.SharedState.Tags.Contains(tag));

		var chkTag = new CheckBox
		{
			Content = tag,
			IsChecked = isChecked,
			MinWidth = 120
		};

		chkTag.Click += (s, args) => tagService.BatchToggleTag(targetItems, tag);

		Grid.SetColumn(chkTag, 0);
		grid.Children.Add(chkTag);

		// Botão de Cor
		var currentColor = TagColorService.Instance.GetColorForTag(tag);
		var colorBtn = new Button
		{
			Width = 22,
			Height = 22,
			Padding = new Thickness(0),
			Background = new SolidColorBrush(currentColor),
			BorderThickness = new Thickness(1),
			BorderBrush = new SolidColorBrush(Colors.Gray),
			CornerRadius = new CornerRadius(4)
		};

		// CORREÇÃO APLICADA AQUI:
		ToolTipService.SetToolTip(colorBtn, "Alterar cor da tag");

		var paletteFlyout = new Flyout { Placement = FlyoutPlacementMode.RightEdgeAlignedTop };
		var paletteGrid = new VariableSizedWrapGrid
		{
			MaximumRowsOrColumns = 4,
			Orientation = Orientation.Horizontal,
			ItemHeight = 28,
			ItemWidth = 28,
			Margin = new Thickness(4)
		};

		foreach (var color in _paletteColors)
		{
			var swatchBtn = new Button
			{
				Width = 24,
				Height = 24,
				Padding = new Thickness(0),
				Margin = new Thickness(0),
				Background = new SolidColorBrush(color),
				CornerRadius = new CornerRadius(12),
				BorderThickness = new Thickness(1),
				BorderBrush = new SolidColorBrush(Colors.LightGray)
			};
			swatchBtn.Click += (s, args) =>
			{
				TagColorService.Instance.SetColorForTag(tag, color);
				colorBtn.Background = new SolidColorBrush(color);
				paletteFlyout.Hide();
			};
			paletteGrid.Children.Add(swatchBtn);
		}
		paletteFlyout.Content = paletteGrid;
		colorBtn.Flyout = paletteFlyout;

		Grid.SetColumn(colorBtn, 1);
		grid.Children.Add(colorBtn);

		return grid;
	}
}