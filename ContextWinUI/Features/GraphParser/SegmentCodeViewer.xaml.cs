// ARQUIVO: ContextWinUI/Features/GraphParser/Views/Components/SegmentCodeViewer.xaml.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using ContextWinUI.Services;
using ContextWinUI.Helpers;
using ContextWinUI.Features.CodeEditor;
using Microsoft.UI;

namespace ContextWinUI.Features.GraphParser.Views.Components;


public sealed partial class SegmentCodeViewer : UserControl
{
	private readonly FastEditorHighlightService _fastEditorService;
	private readonly RegexHighlightService _regexHighlightService;
	private CancellationTokenSource? _editCts;
	private bool _isInternalUpdate = false;
	public event EventHandler? SaveRequested;
	public event EventHandler<int>? CaretPositionChanged;
	public static readonly DependencyProperty TextProperty =
		DependencyProperty.Register(nameof(Text), typeof(string), typeof(SegmentCodeViewer), new PropertyMetadata(string.Empty, OnTextChanged));
	public static readonly DependencyProperty FileExtensionProperty =
		DependencyProperty.Register(nameof(FileExtension), typeof(string), typeof(SegmentCodeViewer), new PropertyMetadata(".txt"));
	public string Text
	{
		get => (string)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}
	public string FileExtension
	{
		get => (string)GetValue(FileExtensionProperty);
		set => SetValue(FileExtensionProperty, value);
	}
	public SegmentCodeViewer()
	{
		this.InitializeComponent();
		_fastEditorService = new FastEditorHighlightService();
		_regexHighlightService = new RegexHighlightService();
		ApplyThemeAttributes(CodeEditor);
		CodeEditor.SelectionChanged += CodeEditor_SelectionChanged;
		this.ActualThemeChanged += (s, e) => ApplyThemeAttributes(CodeEditor);
	}
	public event EventHandler? ContentModified;
	private void CodeEditor_BringIntoViewRequested(UIElement sender, BringIntoViewRequestedEventArgs args)
	{
		args.Handled = true;
	}
	public void ReplaceTextRange(int start, int length, string newText)
	{
		try
		{
			double currentVOffset = EditorScrollViewer.VerticalOffset;
			double currentHOffset = EditorScrollViewer.HorizontalOffset;
			_isInternalUpdate = true;
			CodeEditor.Document.Selection.SetRange(start, start + length);
			CodeEditor.Document.Selection.SetText(Microsoft.UI.Text.TextSetOptions.None, newText);
			_isInternalUpdate = false;
			CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string currentText);
			string normalized = currentText.Replace("\r", "\r\n");
			Text = normalized;
			EditorScrollViewer.ChangeView(currentHOffset, currentVOffset, null, true);
			RequestEditorHighlighting();
		}
		catch (Exception ex)
		{
			_isInternalUpdate = false;
			System.Diagnostics.Debug.WriteLine($"Erro ao substituir texto: {ex.Message}");
		}
	}
	private void CodeEditor_SelectionChanged(object sender, RoutedEventArgs e)
	{
		int start = CodeEditor.Document.Selection.StartPosition;
		CaretPositionChanged?.Invoke(this, start);
	}
	private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var control = (SegmentCodeViewer)d;
		if (control._isInternalUpdate) return;
		var newText = e.NewValue as string ?? string.Empty;
		control.CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string currentEditorText);
		string currentEditorTextNormalized = currentEditorText.Replace("\r", "\r\n");
		if (currentEditorTextNormalized != newText)
		{
			control._isInternalUpdate = true;
			control.CodeEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, newText);
			control._isInternalUpdate = false;
			control.RequestEditorHighlighting();
		}
	}
	private void CodeEditor_TextChanged(object sender, RoutedEventArgs e)
	{
		if (_isInternalUpdate) return;
		try
		{
			CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string currentText);
			string normalizedText = currentText.Replace("\r", "\r\n");
			_isInternalUpdate = true;
			Text = normalizedText;
			_isInternalUpdate = false;
			RequestEditorHighlighting();
			ContentModified?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro em CodeEditor_TextChanged: {ex.Message}");
		}
	}
	private void RequestEditorHighlighting()
	{
		_editCts?.Cancel();
		_editCts = new CancellationTokenSource();
		var token = _editCts.Token;
		string currentText = Text;
		string ext = FileExtension?.ToLower() ?? ".txt";
		bool isDark = ThemeHelper.IsDarkTheme();
		_ = Task.Delay(250, token).ContinueWith(async _ =>
		{
			if (token.IsCancellationRequested) return;
			try
			{
				List<HighlightSpan> syntaxSpans;
				if (ext == ".cs")
					syntaxSpans = await _fastEditorService.CalculateHighlightsAsync(currentText, isDark);
				else
					syntaxSpans = await _regexHighlightService.CalculateHighlightsAsync(currentText, ext, ThemeHelper.GetCurrentThemeStyle());
				if (token.IsCancellationRequested) return;
				DispatcherQueue.TryEnqueue(() =>
				{
					if (token.IsCancellationRequested) return;
					ApplyHighlights(syntaxSpans);
				});
			}
			catch { }
		}, TaskScheduler.Default);
	}
	private void ApplyHighlights(List<HighlightSpan> syntaxSpans)
	{
		if (CodeEditor == null || CodeEditor.Document == null) return;
		try
		{
			CodeEditor.Document.BatchDisplayUpdates();
			CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string editorText);
			if (string.IsNullOrEmpty(editorText)) return;
			int editorLength = editorText.Length;
			var reuseRange = CodeEditor.Document.GetRange(0, 0);
			reuseRange.SetRange(0, editorLength);
			var defaultFg = ThemeHelper.IsDarkTheme() ? Colors.White : Colors.Black;
			reuseRange.CharacterFormat.BackgroundColor = Colors.Transparent;
			reuseRange.CharacterFormat.ForegroundColor = defaultFg;
			foreach (var span in syntaxSpans)
			{
				ApplySpanOptimized(reuseRange, span, editorText, editorLength, isBackground: false);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Highlight Error: {ex.Message}");
		}
		finally
		{
			try { CodeEditor.Document.ApplyDisplayUpdates(); } catch { }
		}
	}
	private void ApplySpanOptimized(Microsoft.UI.Text.ITextRange range, HighlightSpan span, string editorText, int editorLength, bool isBackground)
	{
		int visualStart = MapRoslynToVisualIndices(span.Start, editorText);
		int visualEndRaw = MapRoslynToVisualIndices(span.Start + span.Length, editorText);
		int visualLength = visualEndRaw - visualStart;
		int safeStart = Math.Clamp(visualStart, 0, editorLength);
		int safeEnd = Math.Clamp(visualStart + visualLength, 0, editorLength);
		if (safeEnd > safeStart)
		{
			range.SetRange(safeStart, safeEnd);
			if (isBackground)
				range.CharacterFormat.BackgroundColor = span.Color;
			else
				range.CharacterFormat.ForegroundColor = span.Color;
		}
	}
	private int MapRoslynToVisualIndices(int roslynIndex, string editorText)
	{
		int currentRoslynCount = 0;
		int len = editorText.Length;
		for (int i = 0; i < len; i++)
		{
			if (currentRoslynCount >= roslynIndex) return i;
			if (editorText[i] == '\r')
				currentRoslynCount += 2;
			else
				currentRoslynCount += 1;
		}
		return len;
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
				control.Foreground = new SolidColorBrush(ThemeHelper.GetColorFromHex(style.Foreground));
		}
	}
}