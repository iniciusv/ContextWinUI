using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Shared; // Para ThemeHelper
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.CodeEditor.Highlight;
using ContextWinUI.Helpers;
using ContextWinUI.Services;    // Onde está o HighlightingOrchestrator
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;

namespace ContextWinUI.Features.GraphParser.Views.Components;
public sealed partial class SegmentCodeViewer : UserControl
{
	public bool IsReadOnly
	{
		get => (bool)GetValue(IsReadOnlyProperty);
		set => SetValue(IsReadOnlyProperty, value);
	}
	public static readonly DependencyProperty IsReadOnlyProperty =	DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(SegmentCodeViewer), new PropertyMetadata(false, OnIsReadOnlyChanged));
	private readonly HighlightingOrchestrator _orchestrator;

	private SemanticHighlightService? _semanticService;

	private CancellationTokenSource? _editCts;

	private bool _isInternalUpdate = false;

	public event EventHandler? SaveRequested;
	public event EventHandler<int>? CaretPositionChanged;
	public event EventHandler? ContentModified;
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

	private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is SegmentCodeViewer ctrl)
		{
			ctrl.CodeEditor.IsReadOnly = (bool)e.NewValue;

			 ctrl.CodeEditor.Background = (bool)e.NewValue
				? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(20, 0, 0, 0))
				: new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
		}
	}


	public SegmentCodeViewer()
	{
		this.InitializeComponent();

		// Instancia o serviço que encapsula a lógica de Highlight e Pintura
		_orchestrator = new HighlightingOrchestrator();

		// Configurações iniciais de tema
		ApplyBaseTheme();

		// Hooks de eventos
		CodeEditor.SelectionChanged += (s, e) => CaretPositionChanged?.Invoke(this, CodeEditor.Document.Selection.StartPosition);
		this.ActualThemeChanged += (s, e) => ApplyBaseTheme();
		this.Loaded += OnLoaded;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		InitializeSemanticService();
		ApplyBaseTheme();
		// Dispara a primeira pintura
		TriggerHighlightUpdate();
	}

	private void InitializeSemanticService()
	{
		// O serviço semântico precisa do IndexService global, que pegamos do App.Current
		if (_semanticService != null) return;

		if (ContextWinUI.App.Current is ContextWinUI.App app)
		{
			var indexService = app.Services.GetService(typeof(SemanticIndexService)) as SemanticIndexService;
			if (indexService != null)
			{
				_semanticService = new SemanticHighlightService(indexService, ThemeService.Instance);
			}
		}
	}


	private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var ctrl = (SegmentCodeViewer)d;
		if (ctrl._isInternalUpdate) return;

		string newText = e.NewValue as string ?? string.Empty;
		ctrl.UpdateEditorContentSafely(newText);
	}

	private void CodeEditor_TextChanged(object sender, RoutedEventArgs e)
	{
		if (_isInternalUpdate) return;

		try
		{
			CodeEditor.Document.GetText(TextGetOptions.None, out string currentText);
			string cleanText = CleanRichText(currentText);

			// Só propaga se houve mudança real (ignora formatação)
			if (cleanText != Text)
			{
				_isInternalUpdate = true;
				Text = cleanText; // Atualiza a DP
				_isInternalUpdate = false;

				TriggerHighlightUpdate(); // Solicita repintura
				ContentModified?.Invoke(this, EventArgs.Empty);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro em CodeEditor_TextChanged: {ex.Message}");
		}
	}


	// SegmentCodeViewer.xaml.cs

	private void TriggerHighlightUpdate()
	{
		_editCts?.Cancel();
		_editCts = new CancellationTokenSource();
		var token = _editCts.Token;

		// ESTAMOS NA UI THREAD AQUI.
		// Capturamos os dados de UI/Tema AGORA.
		CodeEditor.Document.GetText(TextGetOptions.None, out string rawText);
		rawText = CleanRichText(rawText);
		string ext = FileExtension?.ToLower() ?? ".txt";

		// Coleta o tema ANTES de entrar na Task
		bool isDark = ThemeHelper.IsDarkTheme();
		var themeStyles = ThemeHelper.GetCurrentThemeStyle();

		_ = Task.Delay(250, token).ContinueWith(async _ =>
		{
			if (token.IsCancellationRequested) return;

			// Passamos os dados de tema (imutáveis) para o serviço
			await _orchestrator.HighlightEditorAsync(
				CodeEditor,
				rawText,
				ext,
				_semanticService,
				isDark,
				themeStyles,
				token);

		}, TaskScheduler.Default);
	}

	// --- UTILITÁRIOS PÚBLICOS ---

	public void ReplaceTextRange(int start, int length, string newText)
	{
		try
		{
			double vOffset = EditorScrollViewer.VerticalOffset;
			double hOffset = EditorScrollViewer.HorizontalOffset;

			_isInternalUpdate = true;

			CodeEditor.Document.Selection.SetRange(start, start + length);
			CodeEditor.Document.Selection.SetText(TextSetOptions.None, newText);

			// Sincroniza propriedade Text
			CodeEditor.Document.GetText(TextGetOptions.None, out string txt);
			Text = CleanRichText(txt);

			// Restaura Scroll
			EditorScrollViewer.ChangeView(hOffset, vOffset, null, true);

			_isInternalUpdate = false;

			TriggerHighlightUpdate();
		}
		catch (Exception ex)
		{
			_isInternalUpdate = false;
			System.Diagnostics.Debug.WriteLine($"Erro ao substituir texto: {ex.Message}");
		}
	}


	private void UpdateEditorContentSafely(string newText)
	{
		CodeEditor.Document.GetText(TextGetOptions.None, out string current);
		string cleanCurrent = CleanRichText(current);

		// Normaliza quebras de linha para comparação
		if (cleanCurrent != newText.Replace("\r\n", "\n").Replace("\r", "\n"))
		{
			// 1. Verifica se estava travado
			bool wasReadOnly = CodeEditor.IsReadOnly;

			// 2. Se estiver travado, destrava temporariamente para permitir a edição via código
			if (wasReadOnly)
			{
				CodeEditor.IsReadOnly = false;
			}

			try
			{
				_isInternalUpdate = true;
				CodeEditor.Document.SetText(TextSetOptions.None, newText);
			}
			finally
			{
				_isInternalUpdate = false;

				// 3. Restaura o estado original (trava novamente se necessário)
				if (wasReadOnly)
				{
					CodeEditor.IsReadOnly = true;
				}
			}

			TriggerHighlightUpdate();
		}
	}
	private string CleanRichText(string input)
	{
		if (string.IsNullOrEmpty(input)) return string.Empty;
		// RichEditBox sempre retorna um \r no final do texto que não faz parte do conteúdo real
		return input.EndsWith("\r") ? input.Substring(0, input.Length - 1) : input;
	}

	private void ApplyBaseTheme()
	{
		var theme = ThemeHelper.GetCurrentThemeStyle();
		if (theme.Contains(ColorCode.Common.ScopeName.PlainText))
		{
			var hex = theme[ColorCode.Common.ScopeName.PlainText].Foreground;
			if (!string.IsNullOrEmpty(hex))
			{
				CodeEditor.Foreground = new SolidColorBrush(ThemeHelper.GetColorFromHex(hex));
			}
		}
	}

	private void CodeEditor_KeyDown(object sender, KeyRoutedEventArgs e)
	{
		// Atalho CTRL+S para salvar
		var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
		bool isCtrlPressed = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

		if (isCtrlPressed && e.Key == VirtualKey.S)
		{
			SaveRequested?.Invoke(this, EventArgs.Empty);
			e.Handled = true;
		}
	}

	private void CodeEditor_BringIntoViewRequested(UIElement sender, BringIntoViewRequestedEventArgs args)
	{
		// Impede que o controle faça scroll automático indesejado ao receber foco
		args.Handled = true;
	}
}