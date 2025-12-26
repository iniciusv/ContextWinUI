// ARQUIVO: GraphVisualizationViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;
using ContextWinUI.Features.GraphView;
using ContextWinUI.Services; // Namespace dos novos serviços

namespace ContextWinUI.ViewModels;

public partial class GraphVisualizationViewModel : ObservableObject
{
	private readonly MainViewModel _mainViewModel;
	private readonly ISyntaxAnalysisService _analysisService;
	private readonly IClipboardService _clipboardService;

	// --- Propriedades de Estado ---
	[ObservableProperty] private string fileContent = string.Empty;
	[ObservableProperty] private IHighlighterStrategy? strategy;
	[ObservableProperty] private string currentExtension = ".txt";

	[ObservableProperty] private string snippetContent = string.Empty;
	[ObservableProperty] private IHighlighterStrategy? snippetStrategy;
	[ObservableProperty] private Visibility snippetOverlayVisibility = Visibility.Visible;

	[ObservableProperty] private bool showScopes = true;
	[ObservableProperty] private bool showTokens = true;

	// Injeção de Dependência via Construtor
	public GraphVisualizationViewModel(
		MainViewModel mainViewModel,
		ISyntaxAnalysisService analysisService,
		IClipboardService clipboardService)
	{
		_mainViewModel = mainViewModel;
		_analysisService = analysisService;
		_clipboardService = clipboardService;

		_mainViewModel.FileContent.PropertyChanged += FileContent_PropertyChanged;
	}

	private void FileContent_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(FileContentViewModel.FileContent) ||
			e.PropertyName == nameof(FileContentViewModel.SelectedItem))
		{
			UpdateVisualization();
		}
	}

	// --- Triggers de Atualização ---
	partial void OnShowScopesChanged(bool value) => RefreshAll();
	partial void OnShowTokensChanged(bool value) => RefreshAll();

	private void RefreshAll()
	{
		UpdateVisualization();
		if (!string.IsNullOrEmpty(SnippetContent)) AnalyzeSnippet(SnippetContent);
	}

	// --- Lógica Principal ---

	public async void UpdateVisualization()
	{
		var item = _mainViewModel.FileContent.SelectedItem;
		FileContent = _mainViewModel.FileContent.FileContent;
		var dispatcher = DispatcherQueue.GetForCurrentThread();

		if (item != null && !string.IsNullOrEmpty(FileContent) && dispatcher != null)
		{
			CurrentExtension = item.SharedState.Extension;
			var filePath = item.FullPath;

			// Análise em Background via Serviço
			var result = await _analysisService.AnalyzeFileAsync(FileContent, filePath);

			// Atualização de UI na Thread Principal
			dispatcher.TryEnqueue(() =>
			{
				Strategy = new LayeredHighlighterStrategy(
					result.Scopes,
					result.Tokens,
					ShowScopes,
					ShowTokens);
			});
		}
		else
		{
			Strategy = null;
		}
	}

	[RelayCommand]
	private async Task PasteSnippetAsync()
	{
		// 1. Limpa estado anterior para dar feedback visual
		SnippetContent = "Lendo área de transferência...";
		SnippetOverlayVisibility = Visibility.Collapsed;

		string? text = await _clipboardService.GetTextContentAsync();

		if (!string.IsNullOrEmpty(text))
		{
			SnippetContent = text;

			// 2. Garante que a estratégia é resetada enquanto analisamos
			SnippetStrategy = null;

			AnalyzeSnippet(text);
		}
		else
		{
			SnippetContent = string.Empty;
			SnippetOverlayVisibility = Visibility.Visible;
		}
	}

	private async void AnalyzeSnippet(string text)
	{
		var dispatcher = DispatcherQueue.GetForCurrentThread();
		if (string.IsNullOrEmpty(text) || dispatcher == null) return;

		// Análise em Background via Serviço
		var result = await _analysisService.AnalyzeSnippetAsync(text);

		dispatcher.TryEnqueue(() =>
		{
			SnippetStrategy = new LayeredHighlighterStrategy(
				result.Scopes,
				result.Tokens,
				ShowScopes,
				ShowTokens);
		});
	}



}