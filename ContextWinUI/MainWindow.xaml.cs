using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO;
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
		this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 700));

		// Inscrever-se para mudanças no conteúdo do arquivo
		ViewModel.FileContent.PropertyChanged += FileContent_PropertyChanged;
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

		var extension = Path.GetExtension(selectedItem.FullPath).ToLower();

		// Se for C#, usa o Roslyn (Poderoso!)
		if (extension == ".cs")
		{
			await _roslynHighlightService.HighlightAsync(CodeRichTextBlock, content);
		}
		else
		{
			// Para outros arquivos (XML, JSON, etc), continua usando o ColorCode antigo
			_syntaxHighlightService.ApplySyntaxHighlighting(CodeRichTextBlock, content, extension);
		}
	}

	private void TreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
	{
		if (args.InvokedItem is FileSystemItem item)
		{
			ViewModel.OnFileSelected(item);
		}
	}

	private void TreeView_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
	{
		if (args.Item is FileSystemItem item)
		{
			_ = ViewModel.FileExplorer.ExpandItemCommand.ExecuteAsync(item);
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
	private async Task AnalyzeMethodsAsync()
	{
		await ViewModel.AnalyzeFileMethodsAsync(ViewModel.FileContent.SelectedItem);
	}
}