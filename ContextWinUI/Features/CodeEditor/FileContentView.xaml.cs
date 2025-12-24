using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using Windows.System;
using Windows.UI.Core;

namespace ContextWinUI.Views;

public sealed partial class FileContentView : UserControl
{
	private readonly RoslynHighlightService _fullRoslynService; // Para Visualização (Lento, Bonito)
	private readonly FastEditorHighlightService _fastEditorService; // Para Edição (Rápido, Estável)
	private readonly RegexHighlightService _regexHighlightService; // Para outros arquivos
	private readonly SyntaxHighlightService _syntaxViewerService;
	private System.Threading.CancellationTokenSource? _editCts;

	private bool _isInternalUpdate = false;
	private bool _isEditing = false;
	private ScrollViewer? _editorScrollViewer;

	public static readonly DependencyProperty ContentViewModelProperty =
		DependencyProperty.Register(nameof(ContentViewModel), typeof(FileContentViewModel), typeof(FileContentView), new PropertyMetadata(null, OnViewModelChanged));
	public FileContentViewModel ContentViewModel
	{
		get => (FileContentViewModel)GetValue(ContentViewModelProperty);
		set => SetValue(ContentViewModelProperty, value);
	}

	// (Outras DependencyProperties: AnalyzeCommand, CopySelectedCommand mantidas...)
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
		_fullRoslynService = new RoslynHighlightService();
		_fastEditorService = new FastEditorHighlightService();
		_regexHighlightService = new RegexHighlightService();
		_syntaxViewerService = new SyntaxHighlightService();

		this.Loaded += (s, e) => EnsureScrollViewer();
	}

	private void EnsureScrollViewer()
	{
		if (_editorScrollViewer == null)
		{
			_editorScrollViewer = FindChild<ScrollViewer>(CodeEditor);
		}
	}

	private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
	{
		if (parent == null) return null;
		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childrenCount; i++)
		{
			var child = VisualTreeHelper.GetChild(parent, i);
			if (child is T typedChild) return typedChild;
			var found = FindChild<T>(child);
			if (found != null) return found;
		}
		return null;
	}

	private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var control = (FileContentView)d;
		if (e.NewValue is FileContentViewModel newVm)
		{
			newVm.PropertyChanged += control.OnViewModelPropertyChanged;
			// Se o VM mudar, reseta para modo visualização
			control.SwitchToViewMode();
		}
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_isInternalUpdate) return;

		if (e.PropertyName == nameof(FileContentViewModel.FileContent))
		{
			// Se o conteúdo mudou externamente (ex: carregou arquivo), atualiza a view
			LoadContentToView();
		}
	}

	private void LoadContentToView()
	{
		if (ContentViewModel == null) return;

		var content = ContentViewModel.FileContent ?? string.Empty;
		var ext = ContentViewModel.SelectedItem?.SharedState.Extension ?? ".txt";

		UpdateLineNumbers(content);

		// --- CORREÇÃO AQUI ---
		// 1. Aplica o Background no ScrollViewer (pois o RichTextBlock é transparente)
		ApplyThemeAttributes(MainScrollViewer);

		// 2. Aplica o Foreground (texto) no RichTextBlock
		ApplyThemeAttributes(CodeViewer);

		// 3. Aplica ambos no Editor (que é um Control completo)
		ApplyThemeAttributes(CodeEditor);
		// ---------------------

		if (_isEditing)
		{
			_isInternalUpdate = true;
			CodeEditor.Document.SetText(TextSetOptions.None, content);
			RequestEditorHighlighting();
			_isInternalUpdate = false;
		}
		else
		{
			_syntaxViewerService.ApplySyntaxHighlighting(CodeViewer, content, ext);
		}
	}

	private void ApplyThemeAttributes(FrameworkElement element)
	{
		var theme = ContextWinUI.Helpers.ThemeHelper.GetCurrentThemeStyle();

		// Verifica se existe estilo para Texto Puro (Base do editor)
		if (theme.Contains(ColorCode.Common.ScopeName.PlainText))
		{
			var plainTextStyle = theme[ColorCode.Common.ScopeName.PlainText];

			SolidColorBrush? bgBrush = null;
			SolidColorBrush? fgBrush = null;

			if (!string.IsNullOrEmpty(plainTextStyle.Background))
			{
				var color = ContextWinUI.Helpers.ThemeHelper.GetColorFromHex(plainTextStyle.Background);
				bgBrush = new SolidColorBrush(color);
			}
			if (!string.IsNullOrEmpty(plainTextStyle.Foreground))
			{
				var color = ContextWinUI.Helpers.ThemeHelper.GetColorFromHex(plainTextStyle.Foreground);
				fgBrush = new SolidColorBrush(color);
			}

			// Lógica específica para cada tipo de controle
			if (element is Control control)
			{
				// RichEditBox, ScrollViewer, Grid, etc.
				if (bgBrush != null) control.Background = bgBrush;
				if (fgBrush != null) control.Foreground = fgBrush;
			}
			else if (element is RichTextBlock rtb)
			{
				// RichTextBlock SÓ tem Foreground, não tem Background
				if (fgBrush != null) rtb.Foreground = fgBrush;
			}
			else if (element is Panel panel)
			{
				// Grids ou StackPanels
				if (bgBrush != null) panel.Background = bgBrush;
			}
		}
	}

	private void BtnEdit_Click(object sender, RoutedEventArgs e)
	{
		_isEditing = true;

		// Copia texto do ViewModel para o Editor
		_isInternalUpdate = true;
		CodeEditor.Document.SetText(TextSetOptions.None, ContentViewModel.FileContent ?? string.Empty);
		_isInternalUpdate = false;

		// Atualiza UI
		CodeEditor.Visibility = Visibility.Visible;
		BtnEdit.Visibility = Visibility.Collapsed;
		BtnSave.Visibility = Visibility.Visible;
		BtnCancel.Visibility = Visibility.Visible;

		// Inicia highlight pesado
		RequestEditorHighlighting();
		CodeEditor.Focus(FocusState.Programmatic);
	}

	private void BtnCancel_Click(object sender, RoutedEventArgs e)
	{
		// Descarta alterações (recarrega do VM)
		SwitchToViewMode();
	}

	private void SwitchToViewMode()
	{
		_isEditing = false;

		// Atualiza UI
		CodeEditor.Visibility = Visibility.Collapsed;
		BtnEdit.Visibility = Visibility.Visible;
		BtnSave.Visibility = Visibility.Collapsed;
		BtnCancel.Visibility = Visibility.Collapsed;

		// Recarrega o Viewer com o conteúdo atual do VM
		LoadContentToView();
	}

	// --- LÓGICA DO EDITOR (Mantida mas isolada) ---

	private void CodeEditor_TextChanged(object sender, RoutedEventArgs e)
	{
		if (_isInternalUpdate || !_isEditing) return;

		CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string currentText);

		UpdateLineNumbers(currentText);

		_isInternalUpdate = true;
		if (ContentViewModel != null) ContentViewModel.FileContent = currentText;
		_isInternalUpdate = false;

		RequestEditorHighlighting();
	}

	private void CodeEditor_KeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (!_isEditing) return;

		var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
		bool isCtrlPressed = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

		if (isCtrlPressed && e.Key == VirtualKey.S)
		{
			if (ContentViewModel != null && ContentViewModel.SaveContentCommand.CanExecute(null))
			{
				ContentViewModel.SaveContentCommand.Execute(null);
				// Após salvar, voltamos para o modo visualização? 
				// Você decide. Abaixo eu mantenho no modo edição, mas atualiza o highlight.
				RequestEditorHighlighting();
			}
			e.Handled = true;
		}
	}

	private void ZoomSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
	{
		if (MainScrollViewer != null)
		{
			MainScrollViewer.ChangeView(null, null, (float)e.NewValue);
		}
	}

	private void RequestEditorHighlighting()
	{
		// 1. Validação básica: se não tem arquivo, não faz nada
		if (ContentViewModel?.SelectedItem == null) return;

		// 2. Mecanismo de Cancelamento (Debounce)
		// Se uma nova tecla for pressionada antes do processamento anterior terminar,
		// cancelamos a tarefa antiga. Isso é CRUCIAL para a performance na edição.
		_editCts?.Cancel();
		_editCts = new System.Threading.CancellationTokenSource();
		var token = _editCts.Token;

		// 3. Coleta de dados na Thread UI (Rápido)
		string ext = System.IO.Path.GetExtension(ContentViewModel.SelectedItem.FullPath).ToLower();

		// Pega o texto cru do editor sem formatação
		CodeEditor.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out string currentText);

		// Verifica o estado do tema (Dark/Light)
		bool isDark = ContextWinUI.Helpers.ThemeHelper.IsDarkTheme();

		// 4. Inicia processamento em Background com pequeno atraso (Throttle)
		// O delay de 50ms serve para agrupar digitações rápidas em uma única atualização visual.
		_ = System.Threading.Tasks.Task.Delay(50, token).ContinueWith(async _ =>
		{
			// Se foi cancelado durante o delay, aborta.
			if (token.IsCancellationRequested) return;

			try
			{
				List<HighlightSpan> spans = new();

				// 5. Cálculo dos Highlights (Pesado - roda em Thread separada)
				if (ext == ".cs")
				{
					// Para C#: Usa o FastEditorHighlightService (Baseado em Roslyn SyntaxTree)
					// Vantagem: Detecta comentários // corretamente e ignora strings.
					spans = await _fastEditorService.CalculateHighlightsAsync(currentText, isDark);
				}
				else
				{
					// Para Outros (JS, XML, JSON): Usa RegexHighlightService
					var currentTheme = ContextWinUI.Helpers.ThemeHelper.GetCurrentThemeStyle();
					spans = await _regexHighlightService.CalculateHighlightsAsync(currentText, ext, currentTheme);
				}

				// Checagem dupla de cancelamento após o cálculo pesado
				if (token.IsCancellationRequested) return;

				// 6. Aplicação Visual (Volta para a Thread UI)
				DispatcherQueue.TryEnqueue(() =>
				{
					// Só aplica se:
					// a) O token ainda é válido
					// b) O usuário ainda está no modo de edição (não clicou em salvar/cancelar nesse meio tempo)
					if (token.IsCancellationRequested || !_isEditing) return;

					try
					{
						if (ext == ".cs")
						{
							_fastEditorService.ApplyHighlights(CodeEditor, spans);
						}
						else
						{
							_regexHighlightService.ApplyHighlights(CodeEditor, spans);
						}
					}
					catch
					{
						// Silencia erros de concorrência visual (raros, mas possíveis em updates muito rápidos)
					}
				});
			}
			catch
			{
				// Silencia erros de Task cancelada ou parsing inválido
			}

		}, System.Threading.Tasks.TaskScheduler.Default);
	}

	private void MainScrollViewer_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		// 1. Verifica se a tecla CTRL está pressionada
		var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
		bool isCtrlDown = (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;

		if (isCtrlDown)
		{
			// 2. Obtém a direção do scroll (Delta)
			var pointerPoint = e.GetCurrentPoint(MainScrollViewer);
			int mouseWheelDelta = pointerPoint.Properties.MouseWheelDelta;

			if (mouseWheelDelta != 0)
			{
				// 3. Define o passo do Zoom (0.1, igual ao Slider antigo)
				// Se Delta > 0 (Scroll pra cima), aumenta. Se < 0, diminui.
				float zoomStep = 0.1f;
				float currentZoom = MainScrollViewer.ZoomFactor;
				float newZoom = mouseWheelDelta > 0
					? currentZoom + zoomStep
					: currentZoom - zoomStep;

				// 4. Limita o zoom aos máximos definidos no XAML (0.5 a 4.0)
				newZoom = Math.Clamp(newZoom, 0.5f, 4.0f);

				// 5. Aplica o zoom programaticamente
				// null nos offsets mantém a posição de rolagem relativa atual
				MainScrollViewer.ChangeView(null, null, newZoom);

				// 6. IMPEDE o comportamento nativo do ScrollViewer
				// Isso evita aquele "pulo" ou inércia indesejada do Windows
				e.Handled = true;
			}
		}
		// Se CTRL não estiver pressionado, deixa o evento passar para o Scroll normal
	}
	private void UpdateLineNumbers(string content)
	{
		if (string.IsNullOrEmpty(content))
		{
			LineNumbersDisplay.Text = "1";
			return;
		}

		// Conta quebras de linha de forma simples e rápida
		int lineCount = 1;
		for (int i = 0; i < content.Length; i++)
		{
			if (content[i] == '\n') lineCount++;
		}

		// Gera a string "1\n2\n3..."
		// Usando StringBuilder para performance
		var sb = new System.Text.StringBuilder();
		for (int i = 1; i <= lineCount; i++)
		{
			sb.AppendLine(i.ToString());
		}

		LineNumbersDisplay.Text = sb.ToString();
	}
}