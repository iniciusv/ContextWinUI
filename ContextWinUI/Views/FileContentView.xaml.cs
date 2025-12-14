using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Windows.Input;

namespace ContextWinUI.Views;

public sealed partial class FileContentView : UserControl
{
	private readonly SyntaxHighlightService _syntaxHighlightService;
	private readonly RoslynHighlightService _roslynHighlightService;

	// ViewModel de Conteúdo
	public static readonly DependencyProperty ContentViewModelProperty =
		DependencyProperty.Register(nameof(ContentViewModel), typeof(FileContentViewModel), typeof(FileContentView), new PropertyMetadata(null, OnViewModelChanged));

	public FileContentViewModel ContentViewModel
	{
		get => (FileContentViewModel)GetValue(ContentViewModelProperty);
		set => SetValue(ContentViewModelProperty, value);
	}

	// Comandos
	public static readonly DependencyProperty AnalyzeCommandProperty =
		DependencyProperty.Register(nameof(AnalyzeCommand), typeof(ICommand), typeof(FileContentView), new PropertyMetadata(null));
	public ICommand AnalyzeCommand
	{
		get => (ICommand)GetValue(AnalyzeCommandProperty);
		set => SetValue(AnalyzeCommandProperty, value);
	}

	public static readonly DependencyProperty CopySelectedCommandProperty =
		DependencyProperty.Register(nameof(CopySelectedCommand), typeof(ICommand), typeof(FileContentView), new PropertyMetadata(null));
	public ICommand CopySelectedCommand
	{
		get => (ICommand)GetValue(CopySelectedCommandProperty);
		set => SetValue(CopySelectedCommandProperty, value);
	}

	public FileContentView()
	{
		this.InitializeComponent();
		_syntaxHighlightService = new SyntaxHighlightService();
		_roslynHighlightService = new RoslynHighlightService();
	}

	private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var control = (FileContentView)d;

		if (e.OldValue is FileContentViewModel oldVm)
			oldVm.PropertyChanged -= control.OnViewModelPropertyChanged;

		if (e.NewValue is FileContentViewModel newVm)
			newVm.PropertyChanged += control.OnViewModelPropertyChanged;
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(FileContentViewModel.FileContent))
		{
			ApplySyntaxHighlighting();
		}
	}

	private async void ApplySyntaxHighlighting()
	{
		// Verifica se o ViewModel está nulo antes de prosseguir
		if (ContentViewModel == null) return;

		var content = ContentViewModel.FileContent;
		var selectedItem = ContentViewModel.SelectedItem;

		// Limpa o RichTextBlock se não houver conteúdo
		if (string.IsNullOrEmpty(content) || selectedItem == null)
		{
			// AQUI OCORRIA O ERRO: Certifique-se que o x:Name está no XAML
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
}