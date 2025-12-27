using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphView;

public partial class GraphVisualizationViewModel : ObservableObject
{
	private readonly MainViewModel _mainViewModel;
	private readonly ISyntaxAnalysisService _analysisService;
	private readonly IClipboardService _clipboardService;
	private readonly ISnippetFileRelationService _relationService;
	private readonly IFileSystemService _fileSystemService;
	private readonly ICodeConsolidationService _consolidationService;

	[ObservableProperty] private string fileContent = string.Empty;
	[ObservableProperty] private IHighlighterStrategy? strategy;
	[ObservableProperty] private string currentExtension = ".txt";
	[ObservableProperty] private string snippetContent = string.Empty;
	[ObservableProperty] private IHighlighterStrategy? snippetStrategy;
	[ObservableProperty] private Visibility snippetOverlayVisibility = Visibility.Visible;
	[ObservableProperty] private string unifiedContent = string.Empty;
	[ObservableProperty] private bool canApplyChanges;
	[ObservableProperty] private bool showScopes = true;
	[ObservableProperty] private bool showTokens = true;

	[ObservableProperty] private ObservableCollection<ContextActionViewModel> contextActions = new();

	public GraphVisualizationViewModel(
		MainViewModel mainViewModel,
		ISyntaxAnalysisService analysisService,
		IClipboardService clipboardService,
		ISnippetFileRelationService relationService,
		IFileSystemService fileSystemService,
		ICodeConsolidationService consolidationService)
	{
		_mainViewModel = mainViewModel;
		_analysisService = analysisService;
		_clipboardService = clipboardService;
		_relationService = relationService;
		_fileSystemService = fileSystemService;
		_consolidationService = consolidationService;

		_mainViewModel.FileContent.PropertyChanged += (s, e) => {
			if (e.PropertyName == nameof(FileContentViewModel.FileContent)) UpdateVisualization();
		};
	}

	[RelayCommand]
	private async Task PasteSnippetAsync()
	{
		string? text = await _clipboardService.GetTextContentAsync();
		if (!string.IsNullOrEmpty(text))
		{
			SnippetContent = text;
			SnippetOverlayVisibility = Visibility.Collapsed;
			var result = await _analysisService.AnalyzeSnippetAsync(text);
			SnippetStrategy = new LayeredHighlighterStrategy(result.Scopes, result.Tokens, ShowScopes, ShowTokens);
		}
	}

	[RelayCommand]
	private async Task ConsolidateChangesAsync()
	{
		var item = _mainViewModel.FileContent.SelectedItem;
		if (item == null || string.IsNullOrEmpty(SnippetContent)) return;

		_mainViewModel.IsLoading = true;
		try
		{
			var comparison = await _relationService.CompareSnippetWithFileAsync(SnippetContent, FileContent, item.FullPath);

			var actions = new List<ContextActionViewModel>();
			foreach (var match in comparison.ScopeMatches)
			{
				int line = GetLineFromOffset(FileContent, match.FileScope.StartPosition);
				actions.Add(new ContextActionViewModel(match, line));
			}

			ContextActions = new ObservableCollection<ContextActionViewModel>(actions);
			RefreshUnifiedView();
			CanApplyChanges = true;
		}
		finally { _mainViewModel.IsLoading = false; }
	}

	private int GetLineFromOffset(string text, int offset) => text.Take(offset).Count(c => c == '\n') + 1;

	public void RefreshUnifiedView()
	{
		// O serviço reconstrói o arquivo completo baseando-se nas escolhas [V, +, *]
		UnifiedContent = _consolidationService.MergeMultipleContexts(FileContent, SnippetContent, ContextActions);
		UpdateUnifiedHighlighter();
	}

	private async void UpdateUnifiedHighlighter()
	{
		if (string.IsNullOrEmpty(UnifiedContent)) return;
		var result = await _analysisService.AnalyzeFileAsync(UnifiedContent, "preview.cs");

		var dispatcher = DispatcherQueue.GetForCurrentThread();
		dispatcher?.TryEnqueue(() => {
			SnippetStrategy = new LayeredHighlighterStrategy(result.Scopes, result.Tokens, ShowScopes, ShowTokens);
		});
	}

	public async void UpdateVisualization()
	{
		var item = _mainViewModel.FileContent.SelectedItem;
		FileContent = _mainViewModel.FileContent.FileContent;
		if (item != null && !string.IsNullOrEmpty(FileContent))
		{
			CurrentExtension = item.SharedState.Extension;
			var result = await _analysisService.AnalyzeFileAsync(FileContent, item.FullPath);
			Strategy = new LayeredHighlighterStrategy(result.Scopes, result.Tokens, ShowScopes, ShowTokens);
		}
	}

	[RelayCommand]
	private async Task ApplyToFileAsync()
	{
		var item = _mainViewModel.FileContent.SelectedItem;
		if (item == null || string.IsNullOrEmpty(UnifiedContent)) return;
		await _fileSystemService.SaveFileContentAsync(item.FullPath, UnifiedContent);
		await _mainViewModel.FileContent.LoadFileAsync(item);
		_mainViewModel.StatusMessage = "Mudanças consolidadas e salvas.";
	}
}