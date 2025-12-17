using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Windows.Input;
using Windows.System;

namespace ContextWinUI.Views;

public sealed partial class FileContentView : UserControl
{
	// Serviços para EDIÇÃO (Pesados)
	private readonly RoslynHighlightService _roslynHighlightService;
	private readonly RegexHighlightService _regexHighlightService;

	// Serviço para VISUALIZAÇÃO (Leve, gera blocos estáticos)
	private readonly SyntaxHighlightService _syntaxViewerService;

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
		_roslynHighlightService = new RoslynHighlightService();
		_regexHighlightService = new RegexHighlightService();
		_syntaxViewerService = new SyntaxHighlightService(); // Certifique-se que esta classe existe (do seu contexto original)

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

		// --- CORREÇÃO AQUI ---
		// 1. Aplica o Background no ScrollViewer (pois o RichTextBlock é transparente)
		ApplyThemeAttributes(ViewScrollViewer);

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
		ViewScrollViewer.Visibility = Visibility.Collapsed;
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
		ViewScrollViewer.Visibility = Visibility.Visible;
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

		CodeEditor.Document.GetText(TextGetOptions.None, out string currentText);

		_isInternalUpdate = true;
		if (ContentViewModel != null) ContentViewModel.FileContent = currentText;
		_isInternalUpdate = false;

		// Opcional: só fazer highlight ao digitar se a performance permitir
		// RequestEditorHighlighting(); 
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

	private void RequestEditorHighlighting()
	{
		if (ContentViewModel?.SelectedItem == null) return;

		string ext = System.IO.Path.GetExtension(ContentViewModel.SelectedItem.FullPath).ToLower();
		EnsureScrollViewer();

		CodeEditor.Document.GetText(TextGetOptions.None, out string textToHighlight);

		// UI Thread vars
		var currentTheme = ContextWinUI.Helpers.ThemeHelper.GetCurrentThemeStyle();
		bool isDark = ContextWinUI.Helpers.ThemeHelper.IsDarkTheme();

		double currentVOffset = _editorScrollViewer?.VerticalOffset ?? 0;
		double currentHOffset = _editorScrollViewer?.HorizontalOffset ?? 0;

		_ = System.Threading.Tasks.Task.Run(async () =>
		{
			try
			{
				if (ext == ".cs")
				{
					var spans = await _roslynHighlightService.CalculateHighlightsAsync(textToHighlight, currentTheme, isDark);
					DispatcherQueue.TryEnqueue(() =>
					{
						if (_isEditing) // Só aplica se ainda estiver editando
						{
							_roslynHighlightService.ApplyHighlights(CodeEditor, spans);
							// Tenta manter scroll
							try { _editorScrollViewer?.ChangeView(currentHOffset, currentVOffset, null, true); } catch { }
						}
					});
				}
				else
				{
					var spans = await _regexHighlightService.CalculateHighlightsAsync(textToHighlight, ext, currentTheme);
					DispatcherQueue.TryEnqueue(() =>
					{
						if (_isEditing)
						{
							_regexHighlightService.ApplyHighlights(CodeEditor, spans);
							try { _editorScrollViewer?.ChangeView(currentHOffset, currentVOffset, null, true); } catch { }
						}
					});
				}
			}
			catch { }
		});
	}
}