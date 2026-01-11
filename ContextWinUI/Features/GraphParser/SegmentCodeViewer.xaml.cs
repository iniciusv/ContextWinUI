// ARQUIVO: SegmentCodeViewer.xaml.cs
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Shared;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.CodeEditor.Highlight;
using ContextWinUI.Features.GraphParser;
using ContextWinUI.Helpers;
using ContextWinUI.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace ContextWinUI.Features.GraphParser.Views.Components;

public sealed partial class SegmentCodeViewer : UserControl
{
	public bool IsReadOnly
	{
		get => (bool)GetValue(IsReadOnlyProperty);
		set => SetValue(IsReadOnlyProperty, value);
	}

	public static readonly DependencyProperty IsReadOnlyProperty =
		DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(SegmentCodeViewer),
			new PropertyMetadata(false, OnIsReadOnlyChanged));

	private readonly IHighlightingOrchestrator _orchestrator;
	private SemanticHighlightService? _semanticService;
	private CancellationTokenSource? _editCts;
	private bool _isInternalUpdate = false;

	public event EventHandler? SaveRequested;
	public event EventHandler<int>? CaretPositionChanged;
	public event EventHandler? ContentModified;

	public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(SegmentCodeViewer),new PropertyMetadata(string.Empty, OnTextChanged));

	public static readonly DependencyProperty FileExtensionProperty =
		DependencyProperty.Register(nameof(FileExtension), typeof(string), typeof(SegmentCodeViewer),
			new PropertyMetadata(".txt"));

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

	private SemanticHighlightService? GetSemanticService()
	{
		// 1. Se já existe, retorna rápido
		if (_semanticService != null) return _semanticService;

		// 2. Se não existe, tenta criar acessando o App.Current
		if (Application.Current is ContextWinUI.App app)
		{
			// Pega a interface do container
			var indexInterface = app.Services.GetService(typeof(ISemanticIndexService));

			// Faz o cast seguro para a classe concreta que o SemanticHighlightService espera
			if (indexInterface is SemanticIndexService indexConcrete)
			{
				_semanticService = new SemanticHighlightService(indexConcrete, ThemeService.Instance);
			}
		}

		return _semanticService;
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

		// Usando a interface em vez da implementação concreta
		_orchestrator = new HighlightingOrchestrator(); // Pode ser injetado via DI no futuro

		ApplyBaseTheme();
		CodeEditor.SelectionChanged += (s, e) =>
			CaretPositionChanged?.Invoke(this, CodeEditor.Document.Selection.StartPosition);

		this.ActualThemeChanged += (s, e) => ApplyBaseTheme();
		this.Loaded += OnLoaded;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		ApplyBaseTheme();
		TriggerHighlightUpdate();
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

			if (cleanText != Text)
			{
				_isInternalUpdate = true;
				Text = cleanText;
				_isInternalUpdate = false;
				TriggerHighlightUpdate();
				ContentModified?.Invoke(this, EventArgs.Empty);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro em CodeEditor_TextChanged: {ex.Message}");
		}
	}

	private void TriggerHighlightUpdate()
	{
		_editCts?.Cancel();
		_editCts = new CancellationTokenSource();
		var token = _editCts.Token;

		// Pega o texto atual
		CodeEditor.Document.GetText(TextGetOptions.None, out string rawText);
		rawText = CleanRichText(rawText);
		string ext = FileExtension?.ToLower() ?? ".txt";
		bool isDark = ThemeHelper.IsDarkTheme();
		var themeStyles = ThemeHelper.GetCurrentThemeStyle();

		var semanticServiceInstance = GetSemanticService();
		// --------------------

		_ = Task.Delay(250, token).ContinueWith(async _ =>
		{
			if (token.IsCancellationRequested) return;

			await _orchestrator.HighlightEditorAsync(
				CodeEditor,
				rawText,
				ext,
				semanticServiceInstance, // Passamos a instância resolvida
				isDark,
				themeStyles,
				token);
		}, TaskScheduler.Default);
	}

	private void UpdateEditorContentSafely(string newText)
	{
		CodeEditor.Document.GetText(TextGetOptions.None, out string current);
		string cleanCurrent = CleanRichText(current);

		if (cleanCurrent != newText.Replace("\r\n", "\n").Replace("\r", "\n"))
		{
			bool wasReadOnly = CodeEditor.IsReadOnly;
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
		args.Handled = true;
	}
}