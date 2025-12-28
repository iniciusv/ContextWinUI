using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Helpers;
using ContextWinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI;

namespace ContextWinUI.Views.Components;

public sealed partial class CodeEditorControl : UserControl
{
	private readonly FastEditorHighlightService _fastEditorService;
	private readonly RegexHighlightService _regexHighlightService;
	private CancellationTokenSource? _editCts;
	private bool _isInternalUpdate = false;

	public event EventHandler? SaveRequested;
	public event EventHandler<int>? CaretPositionChanged;

	private readonly SemanticHighlightService _semanticHighlightService;

	public static readonly DependencyProperty TextProperty =
		DependencyProperty.Register(nameof(Text), typeof(string), typeof(CodeEditorControl), new PropertyMetadata(string.Empty, OnTextChanged));

	public static readonly DependencyProperty FileExtensionProperty =
		DependencyProperty.Register(nameof(FileExtension), typeof(string), typeof(CodeEditorControl), new PropertyMetadata(".txt"));

	public ObservableCollection<SymbolNode> ContextNodes
	{
		get => (ObservableCollection<SymbolNode>)GetValue(ContextNodesProperty);
		set => SetValue(ContextNodesProperty, value);
	}

	public static readonly DependencyProperty ContextNodesProperty =
		DependencyProperty.Register(
			nameof(ContextNodes),
			typeof(ObservableCollection<SymbolNode>),
			typeof(CodeEditorControl),
			new PropertyMetadata(null, OnContextNodesChanged));

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

	public CodeEditorControl()
	{
		this.InitializeComponent();
		_fastEditorService = new FastEditorHighlightService();
		_regexHighlightService = new RegexHighlightService();
		_semanticHighlightService = new SemanticHighlightService();
		ApplyThemeAttributes(CodeEditor);

		CodeEditor.SelectionChanged += CodeEditor_SelectionChanged;
		this.ActualThemeChanged += (s, e) => ApplyThemeAttributes(CodeEditor);
	}

	private static void OnContextNodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var control = (CodeEditorControl)d;
		if (e.NewValue is ObservableCollection<SymbolNode> newList)
		{
			newList.CollectionChanged -= control.OnContextNodesCollectionChanged; // Remove anterior para evitar duplicação
			newList.CollectionChanged += control.OnContextNodesCollectionChanged;
			control.RequestEditorHighlighting();
		}
	}

	private void OnContextNodesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		RequestEditorHighlighting();
	}

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

			// Normalização crucial
			string normalized = currentText.Replace("\r", "\r\n");
			Text = normalized;

			UpdateLineNumbers(normalized);
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
		var control = (CodeEditorControl)d;
		if (control._isInternalUpdate) return;

		var newText = e.NewValue as string ?? string.Empty;
		control.CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string currentEditorText);

		// Normalização para comparação
		string currentEditorTextNormalized = currentEditorText.Replace("\r", "\r\n");

		if (currentEditorTextNormalized != newText)
		{
			control._isInternalUpdate = true;
			control.CodeEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, newText);
			control.UpdateLineNumbers(newText);
			control._isInternalUpdate = false;
			control.RequestEditorHighlighting();
		}
	}

	private void CodeEditor_TextChanged(object sender, RoutedEventArgs e)
	{
		if (_isInternalUpdate) return;

		CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string currentText);

		// Normalização forçada: O Editor usa \r, o Roslyn quer \r\n
		string normalizedText = currentText.Replace("\r", "\r\n");

		_isInternalUpdate = true;
		Text = normalizedText;
		UpdateLineNumbers(normalizedText);
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
		var currentContexts = ContextNodes?.ToList();

		// OTIMIZAÇÃO 1: Aumentamos o delay para 250ms (Debounce)
		// Isso evita que o highlight rode a cada letra digitada rapidamente, travando a UI.
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

				List<HighlightSpan> contextSpans = new();
				if (currentContexts != null && currentContexts.Any())
				{
					contextSpans = _semanticHighlightService.CalculateContextHighlights(currentContexts, isDark);
				}

				if (token.IsCancellationRequested) return;

				DispatcherQueue.TryEnqueue(() =>
				{
					if (token.IsCancellationRequested) return;
					ApplyHybridHighlights(syntaxSpans, contextSpans);
				});
			}
			catch { }
		}, TaskScheduler.Default);
	}

	private void ApplyHybridHighlights(List<HighlightSpan> syntaxSpans, List<HighlightSpan> contextSpans)
	{
		if (CodeEditor == null || CodeEditor.Document == null) return;

		try
		{
			CodeEditor.Document.BatchDisplayUpdates(); // Congela a pintura visual

			CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string editorText);
			if (string.IsNullOrEmpty(editorText)) return;

			int editorLength = editorText.Length;

			// OTIMIZAÇÃO 2: Criamos UM objeto de Range e o reutilizamos.
			// Chamar GetRange() milhares de vezes em um loop é o que causa o lag.
			var reuseRange = CodeEditor.Document.GetRange(0, 0);

			// 1. Reseta a cor de tudo de uma vez (Rápido)
			reuseRange.SetRange(0, editorLength);
			var defaultFg = ThemeHelper.IsDarkTheme() ? Colors.White : Colors.Black;
			reuseRange.CharacterFormat.BackgroundColor = Colors.Transparent;
			reuseRange.CharacterFormat.ForegroundColor = defaultFg;

			// 2. Aplica Highlights de Contexto
			foreach (var span in contextSpans)
			{
				ApplySpanOptimized(reuseRange, span, editorText, editorLength, isBackground: true);
			}

			// 3. Aplica Highlights de Sintaxe
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

	// OTIMIZAÇÃO 3: Método helper que recebe o range já instanciado
	private void ApplySpanOptimized(Microsoft.UI.Text.ITextRange range, HighlightSpan span, string editorText, int editorLength, bool isBackground)
	{
		int visualStart = MapRoslynToVisualIndices(span.Start, editorText);
		int visualEndRaw = MapRoslynToVisualIndices(span.Start + span.Length, editorText);
		int visualLength = visualEndRaw - visualStart;

		int safeStart = Math.Clamp(visualStart, 0, editorLength);
		int safeEnd = Math.Clamp(visualStart + visualLength, 0, editorLength);

		if (safeEnd > safeStart)
		{
			// Apenas movemos os ponteiros do objeto Range existente. Isso é muito leve.
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
				currentRoslynCount += 2; // O Roslyn conta \r\n (2 chars), o editor tem apenas \r
			else
				currentRoslynCount += 1;
		}
		return len;
	}

	private void UpdateLineNumbers(string content)
	{
		if (string.IsNullOrEmpty(content))
		{
			LineNumbersDisplay.Text = "1";
			return;
		}
		int lineCount = content.Count(c => c == '\n') + 1;

		// Otimização simples de string se o arquivo for muito grande
		if (lineCount > 2000)
		{
			// Evita travar gerando string gigante para arquivos enormes
			LineNumbersDisplay.Text = "1...";
			return;
		}

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
				control.Foreground = new SolidColorBrush(ThemeHelper.GetColorFromHex(style.Foreground));
		}
	}
}