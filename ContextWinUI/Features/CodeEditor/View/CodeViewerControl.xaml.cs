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
	private readonly SyntaxHighlightService _syntaxViewerService;
	private readonly CodeTransformationService _transformationService;

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

	// Passamos as opções de transformação como objeto de dependência
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
		_syntaxViewerService = new SyntaxHighlightService();
		_transformationService = new CodeTransformationService();
		ApplyThemeAttributes();
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

		// Aplica transformações (ocultar comentários, colapsar) se for C#
		if (ext == ".cs" && TransformOptions != null)
		{
			contentToDisplay = _transformationService.TransformCode(originalContent, TransformOptions);
		}

		UpdateLineNumbers(contentToDisplay);
		_syntaxViewerService.ApplySyntaxHighlighting(CodeViewer, contentToDisplay, ext);
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