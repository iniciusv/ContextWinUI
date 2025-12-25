using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Helpers;
using ContextWinUI.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;

namespace ContextWinUI.Views.Components;

public sealed partial class CodeEditorControl : UserControl
{
	private readonly FastEditorHighlightService _fastEditorService;
	private readonly RegexHighlightService _regexHighlightService;
	private CancellationTokenSource? _editCts;
	private bool _isInternalUpdate = false;

	public event EventHandler? SaveRequested;

	public static readonly DependencyProperty TextProperty =
		DependencyProperty.Register(nameof(Text), typeof(string), typeof(CodeEditorControl), new PropertyMetadata(string.Empty, OnTextChanged));

	public string Text
	{
		get => (string)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public static readonly DependencyProperty FileExtensionProperty =
		DependencyProperty.Register(nameof(FileExtension), typeof(string), typeof(CodeEditorControl), new PropertyMetadata(".txt"));

	public string FileExtension
	{
		get => (string)GetValue(FileExtensionProperty);
		set => SetValue(FileExtensionProperty, value);
	}

	public CodeEditorControl()
	{
		this.InitializeComponent();
		_fastEditorService = new FastEditorHighlightService();
		_regexHighlightService = new RegexHighlightService();

		// Aplica tema inicial
		ApplyThemeAttributes(CodeEditor);
	}

	private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var control = (CodeEditorControl)d;
		if (control._isInternalUpdate) return;

		var newText = e.NewValue as string ?? string.Empty;

		// Evita resetar o cursor se o texto for o mesmo (binding loop safety)
		control.CodeEditor.Document.GetText(TextGetOptions.None, out string currentEditorText);
		if (currentEditorText != newText)
		{
			control._isInternalUpdate = true;
			control.CodeEditor.Document.SetText(TextSetOptions.None, newText);
			control.UpdateLineNumbers(newText);
			control.RequestEditorHighlighting();
			control._isInternalUpdate = false;
		}
	}

	private void CodeEditor_TextChanged(object sender, RoutedEventArgs e)
	{
		if (_isInternalUpdate) return;

		CodeEditor.Document.GetText(TextGetOptions.None, out string currentText);

		// Atualiza a propriedade Text (TwoWay binding support)
		_isInternalUpdate = true;
		Text = currentText;
		UpdateLineNumbers(currentText);
		_isInternalUpdate = false;

		RequestEditorHighlighting();
	}

	private void RequestEditorHighlighting()
	{
		_editCts?.Cancel();
		_editCts = new CancellationTokenSource();
		var token = _editCts.Token;

		string currentText = Text;
		string ext = FileExtension?.ToLower() ?? ".txt";
		bool isDark = ThemeHelper.IsDarkTheme();

		_ = Task.Delay(50, token).ContinueWith(async _ =>
		{
			if (token.IsCancellationRequested) return;

			try
			{
				List<HighlightSpan> spans;
				if (ext == ".cs")
				{
					spans = await _fastEditorService.CalculateHighlightsAsync(currentText, isDark);
				}
				else
				{
					var currentTheme = ThemeHelper.GetCurrentThemeStyle();
					spans = await _regexHighlightService.CalculateHighlightsAsync(currentText, ext, currentTheme);
				}

				if (token.IsCancellationRequested) return;

				DispatcherQueue.TryEnqueue(() =>
				{
					if (token.IsCancellationRequested) return;
					try
					{
						if (ext == ".cs")
							_fastEditorService.ApplyHighlights(CodeEditor, spans);
						else
							_regexHighlightService.ApplyHighlights(CodeEditor, spans);
					}
					catch { }
				});
			}
			catch { }
		}, TaskScheduler.Default);
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

	private void CodeEditor_KeyDown(object sender, KeyRoutedEventArgs e)
	{
		var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
		bool isCtrlPressed = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

		if (isCtrlPressed && e.Key == VirtualKey.S)
		{
			SaveRequested?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
	}

	private void ApplyThemeAttributes(Control control)
	{
		var theme = ThemeHelper.GetCurrentThemeStyle();
		if (theme.Contains(ColorCode.Common.ScopeName.PlainText))
		{
			var style = theme[ColorCode.Common.ScopeName.PlainText];
			if (!string.IsNullOrEmpty(style.Foreground))
				control.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(ThemeHelper.GetColorFromHex(style.Foreground));
		}
	}
}