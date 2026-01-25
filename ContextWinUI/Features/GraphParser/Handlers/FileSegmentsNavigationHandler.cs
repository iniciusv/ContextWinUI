using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.GraphParser.Helpers;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;

namespace ContextWinUI.Features.GraphParser.Handlers;

public class FileSegmentsNavigationHandler
{
    private readonly ISymbolResolutionService _symbolService;
    private readonly ICodeBlockParserService _parserService;
    private readonly IFileSegmentsContract _viewModel;

    public FileSegmentsNavigationHandler(
        IFileSegmentsContract viewModel,
        ISymbolResolutionService symbolService,
        ICodeBlockParserService parserService)
    {
        _viewModel = viewModel;
        _symbolService = symbolService;
        _parserService = parserService;
    }

    public async Task ResolveSymbolHeuristic(int cursorIndexInBlock)
    {
        if (_viewModel.SelectedBlock == null) return;

        var helper = new SegmentNavigationHelper(_symbolService);

        var result = await helper.ResolveSymbolAtPositionAsync(
            _viewModel.SelectedBlock.Content,
            cursorIndexInBlock,
            _viewModel.SelectedBlock.AbsoluteStartPosition,
            _viewModel.FilePath
        );

        if (result != null)
        {
            _viewModel.CurrentSymbolInfo = $"[{result.SymbolType}] {result.SymbolName}\nDefinido em: {Path.GetFileName(result.FilePath)}";
        }
        else
        {
            _viewModel.CurrentSymbolInfo = string.Empty;
        }
    }

    public void RefreshReferences()
    {
        // 1. Clear all references
        foreach (var row in _viewModel.Rows) row.Current.IsReferenced = false;

        // 2. Identify all source blocks (Selected + ShowReferences)
        var sourceBlocks = _viewModel.Rows.Where(r => r.Current.IsSelected && r.Current.ShowReferences).ToList();

        if (!sourceBlocks.Any()) return;

        var allSymbols = _viewModel.Rows.Select(r => r.Current.Name).ToList();

        // 3. Accumulate references from all sources
        foreach (var source in sourceBlocks)
        {
            var referencedNames = _parserService.FindReferences(source.Current.Content, allSymbols);

            if (referencedNames.Any())
            {
                var referencedRows = _viewModel.Rows.Where(r => referencedNames.Contains(r.Current.Name));
                foreach (var row in referencedRows)
                {
                    if (row.Current != source.Current)
                        row.Current.IsReferenced = true;
                }
            }
        }
    }

    public async void OnSymbolNavigationRequested(int cursorIndexInBlock, bool isImplementation)
    {
        if (_viewModel.SelectedBlock == null) return;

        var helper = new SegmentNavigationHelper(_symbolService);

        var result = await helper.HandleNavigationRequestAsync(
            _viewModel.SelectedBlock.Content,
            cursorIndexInBlock,
            _viewModel.SelectedBlock.AbsoluteStartPosition,
            _viewModel.FilePath,
            isImplementation
        );

        if (result.SingleTarget != null)
        {
            _viewModel.TriggerNavigationRequest(result.SingleTarget.FilePath, result.SingleTarget.AbsolutePosition);
        }

        if (result.Candidates.Any())
        {
            _viewModel.ImplementationCandidates.Clear();
            foreach (var c in result.Candidates) _viewModel.ImplementationCandidates.Add(c);
        }

        if (!string.IsNullOrEmpty(result.Message))
        {
            _viewModel.CurrentSymbolInfo = result.Message;
        }
    }
    
    public void ScrollToPosition(int absolutePosition)
    {
         // Logic to find the block is simpler in VM because it has Rows directly, 
         // but we can do it here too since we have access to Rows via Contract.
         
        var targetRow = _viewModel.Rows.FirstOrDefault(r => 
            absolutePosition >= r.Current.AbsoluteStartPosition && 
            absolutePosition <= (r.Current.AbsoluteStartPosition + r.Current.Content.Length + 10));

        if (targetRow != null)
        {
            _viewModel.TriggerScrollTo(targetRow.Current);
        }
    }
    
    // Helper to request navigation (Proposed for contract)
    public void TriggerNavigation(string filePath, int position)
    {
         // Contract should have NavigateTo
    }
}
