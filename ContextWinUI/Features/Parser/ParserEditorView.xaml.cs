// ARQUIVO: ParserEditorView.xaml.cs
using ContextWinUI.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ContextWinUI.Features.Parser;

public sealed partial class ParserEditorView : UserControl
{
	public ParserEditorViewModel ViewModel { get; private set; }

	public ParserEditorView()
	{
		this.InitializeComponent();

		// Injeção manual via DataContext global (Window) para garantir consistência
		this.Loaded += OnLoaded;
		this.Unloaded += OnUnloaded;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (App.MainWindow is MainWindow mainWindow)
		{
			ViewModel = mainWindow.ViewModel.ParserEditor;
			// Inscreve no evento de substituição de texto
			ViewModel.RequestTextReplacement += ViewModel_RequestTextReplacement;

			// Força atualização inicial se já houver arquivo carregado
			if (!string.IsNullOrEmpty(ViewModel.CodeContent))
			{
				// Dispara uma atualização visual se necessário
				this.Bindings.Update();
			}
		}
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		if (ViewModel != null)
		{
			ViewModel.RequestTextReplacement -= ViewModel_RequestTextReplacement;
		}
	}

	private void OnCaretPositionChanged(object sender, int position)
	{
		// Notifica o VM que o cursor moveu para atualizar a lista de símbolos ativos
		ViewModel?.OnCaretMoved(position);
	}

	private void ViewModel_RequestTextReplacement(object? sender, (int Start, int Length, string Text) e)
	{
		// Executa a substituição física no controle CodeEditor
		// O controle CodeEditor gerencia a atualização do binding TwoWay da propriedade Text
		EditorControl.ReplaceTextRange(e.Start, e.Length, e.Text);
	}

	// Função estática auxiliar para visibilidade de ícones no XAML
	public static Visibility IsTypeVisible(SymbolType currentType, string targetTypeString)
	{
		return currentType.ToString().Equals(targetTypeString, StringComparison.OrdinalIgnoreCase)
			? Visibility.Visible
			: Visibility.Collapsed;
	}
}