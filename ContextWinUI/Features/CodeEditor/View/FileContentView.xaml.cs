using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Windows.Input;

namespace ContextWinUI.Views;

public sealed partial class FileContentView : UserControl
{
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
		UpdateViewOptions();
	}

	private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var control = (FileContentView)d;
		// Reinicia o estado para visualização ao trocar de VM/Arquivo
		control.SwitchToViewMode();
	}

	private void UpdateViewOptions()
	{
		// Segurança: Se o ViewerControl ainda não foi inicializado (embora raro após InitializeComponent), retorna.
		if (ViewerControl == null) return;

		var options = new CodeTransformationService.TransformationOptions
		{
			HideComments = ToggleComments?.IsOn ?? false, // Padrão false se null
			CollapseMethods = ToggleCollapse?.IsOn ?? false, // Padrão false se null
			MaxLinesForCollapse = (int)(NumCollapseLines?.Value ?? 15) // Padrão 15 se null
		};

		ViewerControl.TransformOptions = options;
	}

	private void OnViewOptionChanged(object sender, RoutedEventArgs e) => UpdateViewOptions();

	private void OnViewOptionChanged_Number(NumberBox sender, NumberBoxValueChangedEventArgs args) => UpdateViewOptions();

	private void BtnEdit_Click(object sender, RoutedEventArgs e)
	{
		// Alterna UI para modo edição
		ViewerControl.Visibility = Visibility.Collapsed;
		EditorControl.Visibility = Visibility.Visible;

		BtnEdit.Visibility = Visibility.Collapsed;
		BtnSave.Visibility = Visibility.Visible;
		BtnCancel.Visibility = Visibility.Visible;
	}

	private void BtnCancel_Click(object sender, RoutedEventArgs e)
	{
		// Ao cancelar, recarregamos o arquivo original do ViewModel para descartar mudanças não salvas na UI
		// Se desejar apenas sair do modo edição mantendo o texto modificado (mas não salvo em disco), remova a linha abaixo.
		if (ContentViewModel?.SelectedItem != null)
		{
			_ = ContentViewModel.LoadFileAsync(ContentViewModel.SelectedItem);
		}
		SwitchToViewMode();
	}

	private void SwitchToViewMode()
	{
		EditorControl.Visibility = Visibility.Collapsed;
		ViewerControl.Visibility = Visibility.Visible;

		BtnEdit.Visibility = Visibility.Visible;
		BtnSave.Visibility = Visibility.Collapsed;
		BtnCancel.Visibility = Visibility.Collapsed;
	}

	private void OnEditorSaveRequested(object? sender, System.EventArgs e)
	{
		if (ContentViewModel != null && ContentViewModel.SaveContentCommand.CanExecute(null))
		{
			ContentViewModel.SaveContentCommand.Execute(null);
		}
	}
}