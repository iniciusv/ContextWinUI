using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Helpers;
using ContextWinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Linq;

namespace ContextWinUI.Views.Components;

public sealed partial class CodeViewerControl : UserControl
{
	// REMOVIDO: private readonly SyntaxHighlightService _syntaxViewerService;
	private readonly CodeTransformationService _transformationService;

	// NOVO: DependencyProperty para a Estratégia
	public static readonly DependencyProperty HighlighterStrategyProperty =
		DependencyProperty.Register(
			nameof(HighlighterStrategy),
			typeof(IHighlighterStrategy),
			typeof(CodeViewerControl),
			new PropertyMetadata(null, OnStrategyChanged));

	public IHighlighterStrategy HighlighterStrategy
	{
		get => (IHighlighterStrategy)GetValue(HighlighterStrategyProperty);
		set => SetValue(HighlighterStrategyProperty, value);
	}

	public static readonly DependencyProperty TextProperty =
		DependencyProperty.Register(nameof(Text), typeof(string), typeof(CodeViewerControl), new PropertyMetadata(string.Empty, OnViewParametersChanged));

	public string Text
	{
		get => (string)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public static readonly DependencyProperty FileExtensionProperty =
		DependencyProperty.Register(nameof(FileExtension), typeof(string), typeof(CodeViewerControl), new PropertyMetadata(".txt", OnViewParametersChanged));

	public string FileExtension
	{
		get => (string)GetValue(FileExtensionProperty);
		set => SetValue(FileExtensionProperty, value);
	}

	public static readonly DependencyProperty TransformOptionsProperty =
		DependencyProperty.Register(nameof(TransformOptions), typeof(CodeTransformationService.TransformationOptions), typeof(CodeViewerControl), new PropertyMetadata(null, OnViewParametersChanged));

	public CodeTransformationService.TransformationOptions TransformOptions
	{
		get => (CodeTransformationService.TransformationOptions)GetValue(TransformOptionsProperty);
		set => SetValue(TransformOptionsProperty, value);
	}

	public CodeViewerControl()
	{
		this.InitializeComponent();

		// MODIFICADO: Define a estratégia padrão para manter compatibilidade com o resto do app
		HighlighterStrategy = new SyntaxHighlightService();

		_transformationService = new CodeTransformationService();
		ApplyThemeAttributes();
	}

	// NOVO: Re-renderiza se a estratégia mudar dinamicamente
	private static void OnStrategyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((CodeViewerControl)d).RenderContent();
	}

	private static void OnViewParametersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((CodeViewerControl)d).RenderContent();
	}

	private void RenderContent()
	{
		string originalContent = Text ?? string.Empty;
		string ext = FileExtension ?? ".txt";

		string contentToDisplay = originalContent;

		// A transformação de código continua funcionando independentemente da cor
		if (ext == ".cs" && TransformOptions != null)
		{
			contentToDisplay = _transformationService.TransformCode(originalContent, TransformOptions);
		}

		UpdateLineNumbers(contentToDisplay);

		// MODIFICADO: Usa a estratégia injetada em vez do serviço privado fixo
		if (HighlighterStrategy != null)
		{
			HighlighterStrategy.ApplyHighlighting(CodeViewer, contentToDisplay, ext);
		}

		ApplyThemeAttributes();
	}

	private void UpdateLineNumbers(string content)
	{
		if (string.IsNullOrEmpty(content))
		{
			LineNumbersDisplay.Text = "1";
			return;
		}
		int lineCount = content.Count(c => c == '\n') + 1;
		var sb = new System.Text.StringBuilder();
		for (int i = 1; i <= lineCount; i++) sb.AppendLine(i.ToString());
		LineNumbersDisplay.Text = sb.ToString();
	}

	private void ApplyThemeAttributes()
	{
		var theme = ThemeHelper.GetCurrentThemeStyle();
		if (theme.Contains(ThemeHelper.ScopePlainText))
		{
			var style = theme[ThemeHelper.ScopePlainText];

			// Foreground vai no RichTextBlock
			if (!string.IsNullOrEmpty(style.Foreground))
			{
				CodeViewer.Foreground = new SolidColorBrush(ThemeHelper.GetColorFromHex(style.Foreground));
			}

			// Background vai no GRID (Correção do erro CS1061)
			if (!string.IsNullOrEmpty(style.Background))
			{
				RootGrid.Background = new SolidColorBrush(ThemeHelper.GetColorFromHex(style.Background));
			}
		}
	}
}