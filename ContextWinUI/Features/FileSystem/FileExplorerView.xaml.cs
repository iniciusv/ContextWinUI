using ContextWinUI.Core.Helpers;
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

	private Color HexToColor(string hex)
	{
		hex = hex.Replace("#", "");
		byte a = 255;
		byte r = 255;
		byte g = 255;
		byte b = 255;

		if (hex.Length == 8)
		{
			a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
			r = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
			g = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
			b = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
		}
		else if (hex.Length == 6)
		{
			r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
			g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
			b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
		}
		return Color.FromArgb(a, r, g, b);
	}

	// Método principal para gerenciar cores
	public SolidColorBrush GetTagBrush(string tagName)
	{
		var sessionManager = ExplorerViewModel._sessionManager; // Você precisará expor isso no VM como public ou internal

		// 1. Tenta pegar a cor salva
		if (sessionManager.TagColors.TryGetValue(tagName, out string hexColor))
		{
			return new SolidColorBrush(HexToColor(hexColor));
		}

		// 2. Se não existir, atribui uma nova da paleta
		// Usa o hash da string para pegar uma cor determinística da paleta _paletteColors
		int index = Math.Abs(tagName.GetHashCode()) % _paletteColors.Count;
		Color selectedColor = _paletteColors[index];

		// Converte para Hex
		string newHex = $"#{selectedColor.A:X2}{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";

		// 3. Salva no SessionManager para persistência futura
		sessionManager.TagColors.TryAdd(tagName, newHex);

		return new SolidColorBrush(selectedColor);
	}

	// Dependency Properties
	public static readonly DependencyProperty ExplorerViewModelProperty =
		DependencyProperty.Register(nameof(ExplorerViewModel), typeof(FileExplorerViewModel), typeof(FileExplorerView), new PropertyMetadata(null));

	public FileExplorerViewModel ExplorerViewModel
	{
		get => (FileExplorerViewModel)GetValue(ExplorerViewModelProperty);
		set => SetValue(ExplorerViewModelProperty, value);
	}

	// [CORREÇÃO AQUI] Alterar o tipo registrado de FileSelectionViewModel para ContextSelectionViewModel
	public static readonly DependencyProperty SelectionViewModelProperty =
		DependencyProperty.Register(nameof(SelectionViewModel), typeof(ContextSelectionViewModel), typeof(FileExplorerView), new PropertyMetadata(null));

	// [CORREÇÃO AQUI] Alterar o tipo da propriedade C#
	public ContextSelectionViewModel SelectionViewModel
	{
		get => (ContextSelectionViewModel)GetValue(SelectionViewModelProperty);
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

	}

	private void OnTagMenuOpening(object sender, object e)
	{
		// O evento Opening do Flyout padrão não nos dá acesso fácil ao elemento clicado no TreeView se usarmos ContextFlyout.
		// Mas notei no XAML que você usa:
		// <TreeViewItem.ContextFlyout>
		//     <Flyout Opening="OnTagMenuOpening"> ...

		// O Sender é o Flyout. O Target é o elemento da UI (TreeViewItem ou Grid interna).
		// O DataContext deve ser o FileSystemItem.

		if (sender is Flyout flyout && flyout.Target.DataContext is FileSystemItem item)
		{


			// ==> A melhor abordagem para o seu código atual:
			flyout.Content = TagMenuBuilder.BuildContent(
				new List<FileSystemItem> { item },
				ExplorerViewModel.TagService,
				this.XamlRoot,
				() => flyout.Hide() // Ação para fechar
			);
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

    public static IEnumerable<FileSystemItem> GetExplorerChildren(IEnumerable<FileSystemItem> children, bool isDirectory) => isDirectory ? children : null;

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