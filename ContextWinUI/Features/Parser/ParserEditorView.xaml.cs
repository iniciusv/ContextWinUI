// ARQUIVO: ParserEditorView.xaml.cs
using ContextWinUI.Core.Models;
using ContextWinUI.Features.Parser.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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

			// Assina os eventos do ViewModel
			ViewModel.RequestTextReplacement += ViewModel_RequestTextReplacement;
			ViewModel.RequestRefactoringConfirmation += ViewModel_RequestRefactoringConfirmation; // <--- NOVO

			if (!string.IsNullOrEmpty(ViewModel.CodeContent))
			{
				this.Bindings.Update();
			}

			// Tenta analisar o clipboard assim que carrega a view
			_ = ViewModel.AnalyzeClipboardCommand.ExecuteAsync(null);
		}
	}
	private void ViewModel_RequestTextReplacement(object? sender, (int Start, int Length, string Text) e)
	{
		EditorControl.ReplaceTextRange(e.Start, e.Length, e.Text);
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		if (ViewModel != null)
		{
			ViewModel.RequestTextReplacement -= ViewModel_RequestTextReplacement;
			// REMOVER ASSINATURA
			ViewModel.RequestRefactoringConfirmation -= ViewModel_RequestRefactoringConfirmation;
		}
	}

	// Handler para mostrar a sugestão
	private async void ViewModel_RequestRefactoringConfirmation(object? sender, RefactoringSuggestion suggestion)
	{
		// Cria um ScrollViewer para o código caso seja muito grande
		var codeScrollViewer = new ScrollViewer
		{
			Height = 250,
			Content = new TextBlock
			{
				Text = suggestion.NewCode,
				FontFamily = new FontFamily("Consolas"),
				FontSize = 12,
				TextWrapping = TextWrapping.Wrap
			}
		};

		var stackPanel = new StackPanel { Spacing = 10 };

		// Cabeçalho com informações de confiança
		var infoHeader = new TextBlock
		{
			Text = $"{suggestion.MatchTypeDescription} (Confiança: {suggestion.Confidence:P0})",
			Foreground = new SolidColorBrush(Colors.Orange),
			FontWeight = Microsoft.UI.Text.FontWeights.Bold
		};

		stackPanel.Children.Add(infoHeader);
		stackPanel.Children.Add(new TextBlock { Text = "Código Sugerido:", Opacity = 0.7 });
		stackPanel.Children.Add(codeScrollViewer);

		ContentDialog dialog = new ContentDialog
		{
			Title = $"Refatorar '{suggestion.TargetSymbol.Name}'?",
			Content = stackPanel,
			PrimaryButtonText = "Aplicar Mudança",
			CloseButtonText = "Cancelar",
			DefaultButton = ContentDialogButton.Primary,
			XamlRoot = this.XamlRoot // Necessário em WinUI 3
		};

		var result = await dialog.ShowAsync();

		if (result == ContentDialogResult.Primary)
		{
			ViewModel.ApplyRefactoring(suggestion);
		}
	}

	public static Visibility IsTypeVisible(SymbolType currentType, string targetTypeString)
	{
		return currentType.ToString().Equals(targetTypeString, StringComparison.OrdinalIgnoreCase)
			? Visibility.Visible
			: Visibility.Collapsed;
	}

	private void OnCaretPositionChanged(object sender, int position)
	{
		// Notifica o VM que o cursor moveu para atualizar a lista de símbolos ativos
		ViewModel?.OnCaretMoved(position);
	}

}