using ContextWinUI.Models;
using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace ContextWinUI.Views;

public sealed partial class FileExplorerView : UserControl
{
	// Tags padrão sugeridas
	private readonly List<string> _standardTags = new() { "Importante", "Revisar", "Documentação", "Bug", "Refatorar" };

	// Paleta de cores vibrantes (Estilo Trello/GitHub) para o seletor rápido
	private readonly List<Color> _paletteColors = new()
	{
		Color.FromArgb(255, 255, 82, 82),   // Red
        Color.FromArgb(255, 255, 64, 129),  // Pink
        Color.FromArgb(255, 224, 64, 251),  // Purple
        Color.FromArgb(255, 124, 77, 255),  // Deep Purple
        Color.FromArgb(255, 83, 109, 254),  // Indigo
        Color.FromArgb(255, 68, 138, 255),  // Blue
        Color.FromArgb(255, 64, 196, 255),  // Light Blue
        Color.FromArgb(255, 24, 255, 255),  // Cyan
        Color.FromArgb(255, 100, 255, 218), // Teal
        Color.FromArgb(255, 105, 240, 174), // Green
        Color.FromArgb(255, 178, 255, 89),  // Light Green
        Color.FromArgb(255, 238, 255, 65),  // Lime
        Color.FromArgb(255, 255, 255, 0),   // Yellow
        Color.FromArgb(255, 255, 215, 64),  // Amber
        Color.FromArgb(255, 255, 171, 64),  // Orange
        Color.FromArgb(255, 255, 110, 64)   // Deep Orange
    };

	// Dependency Properties
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

	// Helper para ícones da árvore (usado no XAML via x:Bind)
	public static Brush GetIconColor(bool isDirectory, bool isIgnored)
	{
		if (isIgnored) return new SolidColorBrush(Colors.Red);
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

	// --- CONSTRUÇÃO DO MENU DE CONTEXTO ---
	private void OnTagMenuOpening(object sender, object e)
	{
		// O sender é um Flyout genérico, não MenuFlyout, permitindo conteúdo customizado
		if (sender is Flyout flyout && flyout.Target.DataContext is FileSystemItem item)
		{
			// Container Principal
			var rootPanel = new StackPanel { Spacing = 4, MinWidth = 180, Padding = new Thickness(4) };

			// 1. Opção: Ignorar Pasta
			if (item.IsDirectory)
			{
				var chkIgnore = CreateMenuCheckBox("Ignorar Pasta", item.SharedState.IsIgnored);
				chkIgnore.Click += (s, args) => item.SharedState.IsIgnored = (chkIgnore.IsChecked == true);

				rootPanel.Children.Add(chkIgnore);
				rootPanel.Children.Add(new MenuFlyoutSeparator());
			}

			// 2. Opção: Nova Tag
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
				flyout.Hide();
				_ = ExplorerViewModel.TagService.PromptAndAddTagAsync(item.SharedState.Tags, this.XamlRoot);
			};
			rootPanel.Children.Add(btnNewTag);
			rootPanel.Children.Add(new MenuFlyoutSeparator());

			// 3. Lista de Tags com Paleta de Cores
			var allTagsDisplay = _standardTags.Union(item.SharedState.Tags).OrderBy(x => x).ToList();

			foreach (var tag in allTagsDisplay)
			{
				// Grid da Linha: [Checkbox (Tag)] --------- [Botão (Cor)]
				var rowGrid = new Grid
				{
					ColumnDefinitions =
					{
						new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, // Expande texto
                        new ColumnDefinition { Width = GridLength.Auto } // Botão cor fixo
                    },
					Margin = new Thickness(4, 2, 4, 2)
				};

				// Coluna 0: Checkbox da Tag
				var isChecked = item.SharedState.Tags.Contains(tag);
				var chkTag = new CheckBox
				{
					Content = tag,
					IsChecked = isChecked,
					MinWidth = 120
				};
				chkTag.Click += (s, args) => ExplorerViewModel.TagService.ToggleTag(item.SharedState.Tags, tag);
				Grid.SetColumn(chkTag, 0);

				// Coluna 1: Botão de Cor (Quadradinho)
				var currentColor = TagColorService.Instance.GetColorForTag(tag);
				var colorBtn = new Button
				{
					Width = 22,
					Height = 22,
					Padding = new Thickness(0),
					Background = new SolidColorBrush(currentColor),
					BorderThickness = new Thickness(1),
					BorderBrush = new SolidColorBrush(Colors.Gray),
					CornerRadius = new CornerRadius(4),
				};
				Grid.SetColumn(colorBtn, 1);

				// --- CRIAÇÃO DA PALETA (Grid 4x4) ---
				var paletteFlyout = new Flyout { Placement = FlyoutPlacementMode.RightEdgeAlignedTop };

				var paletteGrid = new VariableSizedWrapGrid
				{
					MaximumRowsOrColumns = 4,
					Orientation = Orientation.Horizontal,
					ItemHeight = 28, // Tamanho total do slot
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
						Margin = new Thickness(0), // Margin controlada pelo Grid
						Background = new SolidColorBrush(color),
						CornerRadius = new CornerRadius(12), // Redondo
						BorderThickness = new Thickness(1),
						BorderBrush = new SolidColorBrush(Colors.LightGray)
					};

					// Ação: Selecionar Cor
					swatchBtn.Click += (s, args) =>
					{
						TagColorService.Instance.SetColorForTag(tag, color);
						colorBtn.Background = new SolidColorBrush(color); // Atualiza visual imediato
						paletteFlyout.Hide();
					};

					paletteGrid.Children.Add(swatchBtn);
				}

				paletteFlyout.Content = paletteGrid;
				colorBtn.Flyout = paletteFlyout;
				// -------------------------------------

				// Monta a linha
				rowGrid.Children.Add(chkTag);
				rowGrid.Children.Add(colorBtn);

				// Adiciona ao menu principal
				rootPanel.Children.Add(rowGrid);
			}

			// 4. Opção de Limpar
			if (item.SharedState.Tags.Any())
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
					ExplorerViewModel.TagService.ClearTags(item.SharedState.Tags);
					flyout.Hide();
				};
				rootPanel.Children.Add(btnClear);
			}

			// Define o conteúdo final do Flyout
			flyout.Content = rootPanel;
		}
	}

	// Helper para criar Checkbox estilizado para o menu
	private CheckBox CreateMenuCheckBox(string text, bool isChecked)
	{
		return new CheckBox
		{
			Content = text,
			IsChecked = isChecked,
			Margin = new Thickness(4, 2, 4, 2)
		};
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