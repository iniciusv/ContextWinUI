using ColorCode.Styling;
using ContextWinUI.Helpers;
using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ContextWinUI.Views;

public sealed partial class AiChangesView : UserControl
{
	// Serviços (idealmente injetados, aqui instanciados por brevidade)
	private readonly RoslynHighlightService _highlightService = new();
	//private readonly TextDiffService _diffService = new();
	private readonly RoslynSemanticDiffService _diffService = new(); // <--- Mudou de TextDiffService para RoslynSemanticDiffService

	// Controle de Debounce para não travar UI
	private CancellationTokenSource? _renderCts;

	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(nameof(ViewModel), typeof(AiChangesViewModel), typeof(AiChangesView), new PropertyMetadata(null, OnViewModelChanged));

	public AiChangesViewModel ViewModel
	{
		get => (AiChangesViewModel)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	public AiChangesView()
	{
		this.InitializeComponent();
	}

	private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var view = (AiChangesView)d;
		if (e.NewValue is AiChangesViewModel vm)
		{
			// Assina evento de mudança de seleção para atualizar o editor
			vm.PropertyChanged += view.OnViewModelPropertyChanged;
		}
		if (e.OldValue is AiChangesViewModel oldVm)
		{
			oldVm.PropertyChanged -= view.OnViewModelPropertyChanged;
		}
	}

	private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AiChangesViewModel.SelectedChange))
		{
			RenderDiffAsync();
		}
	}

	// ARQUIVO: Views/AiChangesView.xaml.cs

	private async void RenderDiffAsync()
	{
		// Cenario 1: Nenhuma seleção -> Limpa o editor
		if (ViewModel?.SelectedChange == null)
		{
			CodeDiffEditor.IsReadOnly = false;
			CodeDiffEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, "");
			CodeDiffEditor.IsReadOnly = true;
			return;
		}

		// Cancelamento de tarefas anteriores (Debounce)
		_renderCts?.Cancel();
		_renderCts = new CancellationTokenSource();
		var token = _renderCts.Token;

		var change = ViewModel.SelectedChange;

		// --- FASE 1: PREPARAÇÃO DA UI ---
		// Destrava para permitir alteração via código
		CodeDiffEditor.IsReadOnly = false;

		// Define o texto novo no editor
		CodeDiffEditor.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, change.NewContent);

		try
		{
			// Normalização Crítica: O RichEditBox converte tudo para \r internamente.
			// Precisamos que os serviços de análise vejam o texto da mesma forma para alinhar índices.
			string normalizedOriginal = change.OriginalContent.Replace("\r\n", "\r");
			string normalizedNew = change.NewContent.Replace("\r\n", "\r");

			// Pequeno delay para permitir que o controle renderize o texto preto inicial
			await Task.Delay(50, token);

			// --- FASE 2: CÁLCULOS EM BACKGROUND ---

			// A. Cálculo do Diff Semântico (Roslyn)
			// Passamos o texto normalizado. O serviço vai montar a árvore, ignorar espaços e dizer o que mudou logicamente.
			var diffLines = await _diffService.ComputeSemanticDiffAsync(normalizedOriginal, normalizedNew);

			// B. Cálculo do Highlight de Sintaxe (Cores do código)
			var isDark = ContextWinUI.Helpers.ThemeHelper.IsDarkTheme();
			var theme = isDark ? ColorCode.Styling.StyleDictionary.DefaultDark : ColorCode.Styling.StyleDictionary.DefaultLight;

			var highlights = await _highlightService.CalculateHighlightsAsync(normalizedNew, theme, isDark);

			if (token.IsCancellationRequested) return;

			// --- FASE 3: APLICAÇÃO VISUAL ---
			// Aplicamos as cores com o editor ainda destravado (IsReadOnly = false) para garantir funcionamento

			// 1. Aplica cores de sintaxe (Foreground)
			_highlightService.ApplyHighlights(CodeDiffEditor, highlights);

			// 2. Aplica cores de diff (Background Verde nas linhas novas/alteradas)
			_highlightService.ApplyDiffHighlights(CodeDiffEditor, diffLines);
		}
		catch (OperationCanceledException)
		{
			// Ignora cancelamentos normais de digitação rápida/troca de seleção
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro ao renderizar diff: {ex.Message}");
		}
		finally
		{
			// --- FASE 4: FINALIZAÇÃO ---
			// Trava o editor novamente para o usuário não digitar acidentalmente
			CodeDiffEditor.IsReadOnly = true;
		}
	}
}