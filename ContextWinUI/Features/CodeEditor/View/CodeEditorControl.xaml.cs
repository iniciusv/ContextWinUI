// ARQUIVO: CodeEditorControl.xaml.cs
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeEditor; // Certifique-se que HighlightSpan está aqui
using ContextWinUI.Helpers;       // Certifique-se que ThemeHelper está aqui
using ContextWinUI.Services;      // Certifique-se que seus services estão aqui
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

	private static void OnContextNodesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var control = (CodeEditorControl)d;

		// 1. Remove ouvinte da lista antiga (se houver) para evitar memory leaks
		if (e.OldValue is ObservableCollection<SymbolNode> oldList)
		{
			oldList.CollectionChanged -= control.OnContextNodesCollectionChanged;
		}

		// 2. Adiciona ouvinte na lista nova
		if (e.NewValue is ObservableCollection<SymbolNode> newList)
		{
			newList.CollectionChanged += control.OnContextNodesCollectionChanged;
			// Força atualização imediata pois acabamos de receber a lista
			control.RequestEditorHighlighting();
		}
	}

	// Novo método que reage quando itens são adicionados/removidos da lista
	private void OnContextNodesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
	{
		RequestEditorHighlighting();
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

	public static readonly DependencyProperty ContextNodesProperty =
		DependencyProperty.Register(
			nameof(ContextNodes),
			typeof(ObservableCollection<SymbolNode>),
			typeof(CodeEditorControl),
			new PropertyMetadata(null, OnContextNodesChanged));

	public CodeEditorControl()
	{
		this.InitializeComponent();

		_fastEditorService = new FastEditorHighlightService();
		_regexHighlightService = new RegexHighlightService();
		_semanticHighlightService = new SemanticHighlightService();

		ApplyThemeAttributes(CodeEditor);
		CodeEditor.SelectionChanged += CodeEditor_SelectionChanged;

		// Garante que o tema seja reaplicado se mudar durante a execução (opcional, mas recomendado)
		this.ActualThemeChanged += (s, e) => ApplyThemeAttributes(CodeEditor);
	}

	// --- CORREÇÃO DO SCROLL (WinUI 3) ---
	private void CodeEditor_BringIntoViewRequested(UIElement sender, BringIntoViewRequestedEventArgs args)
	{
		// Impede o "pulo" do ScrollViewer externo ao clicar ou digitar
		args.Handled = true;
	}

	// Método usado para substituir texto programaticamente (Refatoração Inteligente)
	public void ReplaceTextRange(int start, int length, string newText)
	{
		try
		{
			// Salva posição do scroll para evitar resets visuais bruscos nesta operação específica
			double currentVOffset = EditorScrollViewer.VerticalOffset;
			double currentHOffset = EditorScrollViewer.HorizontalOffset;

			_isInternalUpdate = true;
			CodeEditor.Document.Selection.SetRange(start, start + length);
			CodeEditor.Document.Selection.SetText(Microsoft.UI.Text.TextSetOptions.None, newText);

			_isInternalUpdate = false;

			// Atualiza propriedade Text para refletir a mudança interna
			CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string currentText);
			Text = currentText;
			UpdateLineNumbers(currentText);

			// Restaura o scroll
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

	private void CodeEditor_TextChanged(object sender, RoutedEventArgs e)
	{
		if (_isInternalUpdate) return;

		CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string currentText);

		// CORREÇÃO: Normalizar para CRLF. O RichEditBox retorna apenas \r.
		// Isso garante que o Roslyn conte 2 chars por quebra de linha, alinhando com o MapRoslynToVisualIndices.
		string normalizedText = currentText.Replace("\r", "\r\n");

		_isInternalUpdate = true;
		Text = normalizedText; // Atualiza a propriedade Text com CRLF
		UpdateLineNumbers(normalizedText);
		_isInternalUpdate = false;

		RequestEditorHighlighting();
	}

	private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var control = (CodeEditorControl)d;
		if (control._isInternalUpdate) return;

		var newText = e.NewValue as string ?? string.Empty;

		control.CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string currentEditorText);

		// Normaliza o texto do editor para comparar corretamente
		string currentEditorTextNormalized = currentEditorText.Replace("\r", "\r\n");

		if (currentEditorTextNormalized != newText)
		{
			control._isInternalUpdate = true;
			// O RichEditBox aceita \r\n e lida bem com isso
			control.CodeEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, newText);
			control.UpdateLineNumbers(newText);
			control._isInternalUpdate = false;

			control.RequestEditorHighlighting();
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

		// Captura referência segura para a thread
		var currentContexts = ContextNodes?.ToList();

		_ = Task.Delay(50, token).ContinueWith(async _ =>
		{
			if (token.IsCancellationRequested) return;
			try
			{
				// 1. Sintaxe (Texto)
				List<HighlightSpan> syntaxSpans;
				if (ext == ".cs")
					syntaxSpans = await _fastEditorService.CalculateHighlightsAsync(currentText, isDark);
				else
					syntaxSpans = await _regexHighlightService.CalculateHighlightsAsync(currentText, ext, ThemeHelper.GetCurrentThemeStyle());

				// 2. Semântica (Fundo) - Apenas se houver nós carregados
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

	// ARQUIVO: CodeEditorControl.xaml.cs

	// Substitua o método ApplyHybridHighlights por este:
	private void ApplyHybridHighlights(List<HighlightSpan> syntaxSpans, List<HighlightSpan> contextSpans)
	{
		if (CodeEditor == null || CodeEditor.Document == null) return;

		try
		{
			CodeEditor.Document.BatchDisplayUpdates();

			// Pega o texto CRU do editor (que contém apenas \r geralmente) para calcular os índices visuais
			CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string editorText);

			if (string.IsNullOrEmpty(editorText)) return;

			int editorLength = editorText.Length;
			var fullRange = CodeEditor.Document.GetRange(0, editorLength);

			// 1. Limpa a formatação anterior (Reseta para a cor padrão)
			if (fullRange != null)
			{
				var defaultFg = ThemeHelper.IsDarkTheme() ? Colors.White : Colors.Black;
				fullRange.CharacterFormat.BackgroundColor = Colors.Transparent;
				fullRange.CharacterFormat.ForegroundColor = defaultFg;
			}

			// 2. Aplica Highlights de Contexto (Fundo/Background) - JÁ ESTAVA CORRETO
			foreach (var span in contextSpans)
			{
				ApplySpan(span, editorText, editorLength, isBackground: true);
			}

			// 3. Aplica Highlights de Sintaxe (Regex ou Roslyn Rápido) - CORREÇÃO AQUI
			// Antes, isso era aplicado direto (span.Start), agora usa o Mapeamento.
			foreach (var span in syntaxSpans)
			{
				ApplySpan(span, editorText, editorLength, isBackground: false);
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

	// Método auxiliar para evitar duplicação de lógica e aplicar o Mapeamento
	private void ApplySpan(HighlightSpan span, string editorText, int editorLength, bool isBackground)
	{
		// O SEGREDO: MapRoslynToVisualIndices converte o índice "Físico" (calculado com \r\n)
		// para o índice "Visual" que o RichEditBox entende.
		int visualStart = MapRoslynToVisualIndices(span.Start, editorText);

		// Calculamos o fim também mapeado, para garantir que o comprimento (Length)
		// se ajuste caso o span atravesse uma quebra de linha (raro em highlight simples, mas possível em strings multi-linha)
		int visualEndRaw = MapRoslynToVisualIndices(span.Start + span.Length, editorText);
		int visualLength = visualEndRaw - visualStart;

		int safeStart = Math.Clamp(visualStart, 0, editorLength);
		int safeEnd = Math.Clamp(visualStart + visualLength, 0, editorLength);

		if (safeEnd > safeStart)
		{
			var range = CodeEditor.Document.GetRange(safeStart, safeEnd);
			if (isBackground)
			{
				range.CharacterFormat.BackgroundColor = span.Color;
			}
			else
			{
				range.CharacterFormat.ForegroundColor = span.Color;
			}
		}
	}


	// Função auxiliar local para fazer o mapeamento reverso usando apenas o texto do editor
	private int MapRoslynToVisualIndices(int roslynIndex, string editorText)
	{
		// O Roslyn conta 2 chars para cada nova linha (\r\n).
		// O Editor tem apenas 1 char (\r).
		// Queremos encontrar o índice 'i' no editor tal que:
		// i + (número de \r antes de i) == roslynIndex

		int currentRoslynCount = 0;
		for (int i = 0; i < editorText.Length; i++)
		{
			if (currentRoslynCount >= roslynIndex) return i;

			if (editorText[i] == '\r')
				currentRoslynCount += 2; // Roslyn veria isso como \r\n
			else
				currentRoslynCount += 1;
		}
		return editorText.Length;
	}


	private void UpdateLineNumbers(string content)
	{
		if (string.IsNullOrEmpty(content))
		{
			LineNumbersDisplay.Text = "1";
			return;
		}
		// Conta quebras de linha. Dependendo do OS pode ser \r, \n ou \r\n
		int lineCount = content.Count(c => c == '\r') + 1;

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