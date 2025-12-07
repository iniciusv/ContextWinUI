using CommunityToolkit.Mvvm.Input;
using ContextWinUI.ContextWinUI.Services;
using ContextWinUI.Models;
using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace ContextWinUI;

public sealed partial class MainWindow : Window
{
	public MainViewModel ViewModel { get; }
	private readonly SyntaxHighlightService _syntaxHighlightService;
	private readonly RoslynHighlightService _roslynHighlightService = new();

	public MainWindow()
	{
		InitializeComponent();

		ViewModel = new MainViewModel();
		_syntaxHighlightService = new SyntaxHighlightService();

		Title = "Context WinUI - Explorador de Código";

		if (Content is FrameworkElement fe)
		{
			fe.Loaded += (s, e) => { this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 700)); };
		}

		ViewModel.FileContent.PropertyChanged += FileContent_PropertyChanged;

		// WIRING DO EVENTO DE PREVIEW DO CONTEXTO
		// Quando o usuário clica em um arquivo na árvore de contexto (Direita),
		// o ViewModel de Análise dispara um evento. Nós pegamos esse evento e 
		// mandamos o ViewModel de Conteúdo carregar o arquivo.
		ViewModel.ContextAnalysis.FileSelectedForPreview += async (s, item) =>
		{
			await ViewModel.FileContent.LoadFileAsync(item);
		};
	}

	private void FileContent_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(FileContentViewModel.FileContent))
		{
			ApplySyntaxHighlighting();
		}
	}

	private async void ApplySyntaxHighlighting()
	{
		var content = ViewModel.FileContent.FileContent;
		var selectedItem = ViewModel.FileContent.SelectedItem;

		if (string.IsNullOrEmpty(content) || selectedItem == null)
		{
			CodeRichTextBlock.Blocks.Clear();
			return;
		}

		var extension = System.IO.Path.GetExtension(selectedItem.FullPath).ToLower();

		if (extension == ".cs")
		{
			await _roslynHighlightService.HighlightAsync(CodeRichTextBlock, content);
		}
		else
		{
			_syntaxHighlightService.ApplySyntaxHighlighting(CodeRichTextBlock, content, extension);
		}
	}

	// Evento do TreeView da Esquerda (Explorador de Arquivos)
	private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		if (args.InvokedItem is FileSystemItem item)
		{
			ViewModel.OnFileSelected(item);
		}
	}

	// NOVO: Evento do TreeView da Direita (Análise de Contexto)
	private void ContextTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		if (args.InvokedItem is FileSystemItem item)
		{
			// Isso chama o método no ViewModel, que dispara o evento FileSelectedForPreview que configuramos no construtor
			ViewModel.ContextAnalysis.SelectFileForPreview(item);
		}
	}

	private void TreeView_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
	{
		if (args.Item is FileSystemItem item)
		{
			_ = ViewModel.FileExplorer.ExpandItemCommand.ExecuteAsync(item);
		}
	}

	private async void OnDeepAnalyzeClick(object sender, RoutedEventArgs e)
	{
		if (sender is Button btn && btn.Tag is FileSystemItem item)
		{
			await ViewModel.ContextAnalysis.AnalyzeItemDepthCommand.ExecuteAsync(item);
		}
	}

	private void TreeView_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
	{
		if (args.Item is FileSystemItem item)
		{
			item.IsExpanded = false;
		}
	}

	[RelayCommand]
	private async Task AnalyzeContextAsync()
	{
		await ViewModel.AnalyzeContextCommandAsync();
	}
}