// ARQUIVO: ContextWinUI/Features/GraphParser/Views/Components/SegmentCodeViewer.xaml.cs
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Features.CodeEditor.Highlight;
using ContextWinUI.Helpers;
using ContextWinUI.Services;
using Microsoft.UI;
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

namespace ContextWinUI.Features.GraphParser.Views.Components;


public sealed partial class SegmentCodeViewer : UserControl
{
	private readonly FastEditorHighlightService _fastEditorService;
	private readonly RegexHighlightService _regexHighlightService;
	private CancellationTokenSource? _editCts;
	private bool _isInternalUpdate = false;
	public event EventHandler? SaveRequested;
	public event EventHandler<int>? CaretPositionChanged;
	public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(SegmentCodeViewer), new PropertyMetadata(string.Empty, OnTextChanged));
	public static readonly DependencyProperty FileExtensionProperty = DependencyProperty.Register(nameof(FileExtension), typeof(string), typeof(SegmentCodeViewer), new PropertyMetadata(".txt"));
	private SemanticHighlightService? _semanticHighlightService;

	private void InitializeServices()
	{
		if (ContextWinUI.App.Current is ContextWinUI.App app)
		{
			var indexService = app.Services.GetService(typeof(SemanticIndexService)) as SemanticIndexService;
			// ThemeService é Singleton no seu código original
			var themeService = ThemeService.Instance;

			if (indexService != null && themeService != null)
			{
				_semanticHighlightService = new SemanticHighlightService(indexService, themeService);
			}
		}
	}
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
		this.Loaded += OnLoaded;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		ApplyThemeAttributes(CodeEditor);

		RequestEditorHighlighting();
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

		// Normalização para comparação: O RichEditBox sempre adiciona um \r no final.
		// Precisamos ignorar esse \r extra na comparação para não travar o loop.
		string currentEditorTextNormalized = currentEditorText;
		if (currentEditorTextNormalized.EndsWith("\r"))
		{
			currentEditorTextNormalized = currentEditorTextNormalized.Substring(0, currentEditorTextNormalized.Length - 1);
		}

		// CORREÇÃO: Normaliza quebras de linha para comparar maçãs com maçãs
		var newTextNorm = newText.Replace("\r\n", "\n").Replace("\r", "\n");
		var currentEditorNorm = currentEditorTextNormalized.Replace("\r\n", "\n").Replace("\r", "\n");

		// Só atualiza o editor se o texto for REALMENTE diferente semânticamente
		if (newTextNorm != currentEditorNorm)
		{
			control._isInternalUpdate = true;
			control.CodeEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, newText);
			control._isInternalUpdate = false;
			control.RequestEditorHighlighting();
		}
	}

	// 2. Modifique o evento do Editor
	private void CodeEditor_TextChanged(object sender, RoutedEventArgs e)
	{
		if (_isInternalUpdate) return;

		try
		{
			CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string currentText);

			// O RichEditBox do WinUI retorna um \r no final do texto. Removemos para não sujar o Model.
			if (currentText.EndsWith("\r"))
			{
				currentText = currentText.Substring(0, currentText.Length - 1);
			}

			// Verifica se realmente mudou em relação à propriedade Text atual (Dependency Property)
			// Isso evita que o evento dispare loops de atualização desnecessários
			if (currentText != Text)
			{
				_isInternalUpdate = true;
				Text = currentText; // Atualiza o ViewModel (TwoWay binding)
				_isInternalUpdate = false;

				// Dispara eventos apenas se necessário
				RequestEditorHighlighting();
				ContentModified?.Invoke(this, EventArgs.Empty);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro em CodeEditor_TextChanged: {ex.Message}");
		}
	}
	private void RequestEditorHighlighting()
	{
		// 1. Cancelamento da tarefa anterior para evitar sobreposição
		_editCts?.Cancel();
		_editCts = new CancellationTokenSource();
		var token = _editCts.Token;

		// 2. CAPTURA DE DADOS NA UI THREAD (Correção do erro COMException)
		// Precisamos ler tudo o que é visual ou do sistema de UI *antes* de entrar na Task.

		// Leitura do texto do editor
		CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string rawEditorText);

		// Normalização crítica para linhas únicas (Propriedades/Campos)
		// O RichEditBox sempre retorna um \r no final, o que atrapalha o cálculo de índices
		if (rawEditorText.EndsWith("\r"))
		{
			rawEditorText = rawEditorText.Substring(0, rawEditorText.Length - 1);
		}

		string ext = FileExtension?.ToLower() ?? ".txt";

		// Captura do tema e estilos AGORA (na UI Thread)
		bool isDark = ThemeHelper.IsDarkTheme();
		var currentThemeStyles = ThemeHelper.GetCurrentThemeStyle();

		// Inicializa serviços necessários
		InitializeServices();

		// 3. Processamento em Background (Debounce de 250ms para não travar digitação)
		_ = Task.Delay(250, token).ContinueWith(async _ =>
		{
			// Se foi cancelado (usuário digitou de novo), para aqui
			if (token.IsCancellationRequested) return;

			try
			{
				List<HighlightSpan> syntaxSpans = new();

				// A. Highlight Sintático (Roslyn ou Regex)
				if (ext == ".cs")
				{
					syntaxSpans = await _fastEditorService.CalculateHighlightsAsync(rawEditorText, isDark);
				}
				else
				{
					// Passamos o estilo capturado, não chama ThemeHelper aqui dentro
					syntaxSpans = await _regexHighlightService.CalculateHighlightsAsync(rawEditorText, ext, currentThemeStyles);
				}

				// B. Highlight Semântico (Refatorado e Thread-Safe)
				// Só executa se for C# e o serviço estiver disponível
				if (ext == ".cs" && _semanticHighlightService != null)
				{
					// O serviço agora recebe o 'currentThemeStyles' puro, sem acessar UI
					var semanticSpans = _semanticHighlightService.CalculateHighlights(rawEditorText, 0, currentThemeStyles);

					// Mescla: A semântica tem preferência sobre o básico (ex: Class sobre PlainText)
					MergeHighlights(syntaxSpans, semanticSpans);
				}

				if (token.IsCancellationRequested) return;

				// 4. Aplicação na UI Thread
				DispatcherQueue.TryEnqueue(() =>
				{
					if (token.IsCancellationRequested) return;

					// Aplica as cores no RichEditBox
					ApplyHighlights(syntaxSpans, rawEditorText);
				});
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Erro no RequestEditorHighlighting: {ex.Message}");
			}
		}, TaskScheduler.Default);
	}

	private void MergeHighlights(List<HighlightSpan> basic, List<HighlightSpan> semantic)
	{
		foreach (var sem in semantic)
		{
			// Verifica colisão: Não queremos pintar por cima de Comentários, Strings ou Keywords.
			// O SemanticIndexService usa Regex simples, então ele pode achar que "class" é um tipo se houver uma classe chamada "class" (impossível em C#, mas a lógica previne erros).

			bool hasConflict = basic.Any(b =>
				b.Start <= sem.Start &&
				(b.Start + b.Length) >= (sem.Start + sem.Length) &&
				(b.Type == SpanType.String ||
				 b.Type == SpanType.Comment ||
				 b.Type == SpanType.Keyword ||
				 b.Type == SpanType.Number));

			if (!hasConflict)
			{
				// Adiciona ao final da lista. Como ApplyHighlights desenha sequencialmente,
				// o que está no final da lista é desenhado "por cima".
				// Isso fará com que um identificador que o Roslyn achou "genérico" vire "Class" ou "Interface".
				basic.Add(sem);
			}
		}
	}

	// Adicione o parâmetro 'editorText' para termos certeza do tamanho
	private void ApplyHighlights(List<HighlightSpan> syntaxSpans, string editorText)
	{
		if (CodeEditor == null || CodeEditor.Document == null) return;

		try
		{
			CodeEditor.Document.BatchDisplayUpdates();

			// Usamos o tamanho do texto que foi usado para calcular o highlight
			int editorLength = editorText.Length;

			var reuseRange = CodeEditor.Document.GetRange(0, 0);

			// Limpa formatação anterior
			reuseRange.SetRange(0, editorLength);
			var defaultFg = ThemeHelper.IsDarkTheme() ? Colors.White : Colors.Black;
			reuseRange.CharacterFormat.ForegroundColor = defaultFg;

			foreach (var span in syntaxSpans)
			{
				ApplySpanOptimized(reuseRange, span, editorLength, isBackground: false);
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

	private void ApplySpanOptimized(Microsoft.UI.Text.ITextRange range, HighlightSpan span, int editorLength, bool isBackground)
	{
		// SEM MAPAS: O índice do Roslyn (span.Start) agora é idêntico ao visual.
		int safeStart = Math.Clamp(span.Start, 0, editorLength);
		int safeEnd = Math.Clamp(span.Start + span.Length, 0, editorLength);

		if (safeEnd > safeStart)
		{
			range.SetRange(safeStart, safeEnd);

			if (isBackground)
				range.CharacterFormat.BackgroundColor = span.Color;
			else
				range.CharacterFormat.ForegroundColor = span.Color;
		}
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