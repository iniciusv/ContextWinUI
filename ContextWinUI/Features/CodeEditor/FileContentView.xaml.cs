using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using Microsoft.UI.Dispatching;
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
	private readonly RoslynHighlightService _roslynHighlightService;
	private bool _isInternalUpdate = false;
	private ScrollViewer? _innerScrollViewer;

	public static readonly DependencyProperty ContentViewModelProperty =
		DependencyProperty.Register(nameof(ContentViewModel), typeof(FileContentViewModel), typeof(FileContentView), new PropertyMetadata(null, OnViewModelChanged));
	public FileContentViewModel ContentViewModel
	{
		get => (FileContentViewModel)GetValue(ContentViewModelProperty);
		set => SetValue(ContentViewModelProperty, value);
	}

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
		this.Loaded += (s, e) => EnsureScrollViewer();
	}

	private void EnsureScrollViewer()
	{
		if (_innerScrollViewer == null)
		{
			_innerScrollViewer = FindChild<ScrollViewer>(CodeEditor);
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
			newVm.PropertyChanged += control.OnViewModelPropertyChanged;
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_isInternalUpdate) return;
		if (e.PropertyName == nameof(FileContentViewModel.FileContent))
		{
			LoadContentToEditor();
		}
	}

	// MÉTODO NOVO: Aplica a cor de fundo do tema no controle
	private void ApplyEditorTheme()
	{
		var theme = ContextWinUI.Helpers.ThemeHelper.GetCurrentThemeStyle();
		if (theme.Contains(ColorCode.Common.ScopeName.PlainText))
		{
			var plainTextStyle = theme[ColorCode.Common.ScopeName.PlainText];
			if (!string.IsNullOrEmpty(plainTextStyle.Background))
			{
				var color = ContextWinUI.Helpers.ThemeHelper.GetColorFromHex(plainTextStyle.Background);
				CodeEditor.Background = new SolidColorBrush(color);
			}
		}
	}

	private void LoadContentToEditor()
	{
		if (ContentViewModel == null) return;
		_isInternalUpdate = true;

		// 1. Aplica o Fundo Correto
		ApplyEditorTheme();

		// 2. Carrega texto
		CodeEditor.Document.SetText(TextSetOptions.None, ContentViewModel.FileContent ?? string.Empty);

		// 3. Highlight
		RequestHighlighting();

		_isInternalUpdate = false;
	}

	private void CodeEditor_TextChanged(object sender, RoutedEventArgs e)
	{
		if (_isInternalUpdate) return;

		CodeEditor.Document.GetText(TextGetOptions.None, out string currentText);

		_isInternalUpdate = true;
		if (ContentViewModel != null) ContentViewModel.FileContent = currentText;
		_isInternalUpdate = false;
	}

	private void CodeEditor_KeyDown(object sender, KeyRoutedEventArgs e)
	{
		var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
		bool isCtrlPressed = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

		if (isCtrlPressed && e.Key == VirtualKey.C)
		{
			var selection = CodeEditor.Document.Selection;
			if (selection.EndPosition > selection.StartPosition)
			{
				selection.Copy();
				e.Handled = true;
			}
		}

		if (isCtrlPressed && e.Key == VirtualKey.S)
		{
			if (ContentViewModel != null && ContentViewModel.SaveContentCommand.CanExecute(null))
			{
				ContentViewModel.SaveContentCommand.Execute(null);
			}
			RequestHighlighting();
			e.Handled = true;
		}
	}

	private void RequestHighlighting()
	{
		if (ContentViewModel == null || ContentViewModel.SelectedItem == null) return;

		string ext = System.IO.Path.GetExtension(ContentViewModel.SelectedItem.FullPath).ToLower();
		if (ext != ".cs") return;

		EnsureScrollViewer();

		// Garante que o fundo está correto antes de processar
		ApplyEditorTheme();

		CodeEditor.Document.GetText(TextGetOptions.None, out string textToHighlight);
		var currentTheme = ContextWinUI.Helpers.ThemeHelper.GetCurrentThemeStyle();
		bool isDark = ContextWinUI.Helpers.ThemeHelper.IsDarkTheme();

		_ = System.Threading.Tasks.Task.Run(async () =>
		{
			try
			{
				var spans = await _roslynHighlightService.CalculateHighlightsAsync(textToHighlight, currentTheme, isDark);

				DispatcherQueue.TryEnqueue(() =>
				{
					double currentVOffset = _innerScrollViewer?.VerticalOffset ?? 0;
					double currentHOffset = _innerScrollViewer?.HorizontalOffset ?? 0;

					CodeEditor.Document.GetText(TextGetOptions.None, out string currentText);
					if (currentText.Length == textToHighlight.Length)
					{
						_roslynHighlightService.ApplyHighlights(CodeEditor, spans);
						_innerScrollViewer?.ChangeView(currentHOffset, currentVOffset, null, true);
					}
				});
			}
			catch { }
		});
	}
}