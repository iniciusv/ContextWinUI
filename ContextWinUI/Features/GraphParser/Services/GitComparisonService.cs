using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.Services;

public class GitComparisonService : IGitComparisonService
{
    private readonly ICodeBlockParserService _parserService;

    public GitComparisonService(ICodeBlockParserService parserService)
    {
        _parserService = parserService;
    }

    public async Task<(ObservableCollection<SegmentRowViewModel> Rows, ObservableCollection<GlobalVersion> History)> InitializeComparisonAsync(
        string filePath,
        string contentCurrent,
        string contentOld,
        string oldVersionLabel)
    {
        // 1. Parse current content directly (assumed to be "Right" side / Newer)
        var parsedItems = await _parserService.ParseFileAsync(filePath, contentCurrent);

        var bufferList = new List<SegmentRowViewModel>();
        foreach (var item in parsedItems)
        {
            // Note: Event attachment logic remains in ViewModel or can be attached here if references are passed, 
            // but pure logic suggests just returning data. ViewModel should attach events.
            bufferList.Add(new SegmentRowViewModel(item));
        }
        
        var rows = new ObservableCollection<SegmentRowViewModel>(bufferList);

        // 2. Setup Global History
        var globalHistory = new ObservableCollection<GlobalVersion>();

        // Version 0: OLD (Left side)
        globalHistory.Add(new GlobalVersion
        {
            Description = oldVersionLabel,
            IsOriginal = true,
            Timestamp = DateTime.MinValue
        });

        // Version 1: CURRENT (Right side)
        globalHistory.Add(new GlobalVersion
        {
            Description = "Commit Selecionado",
            IsOriginal = false,
            Timestamp = DateTime.Now
        });

        // 3. Parse Old Content
        var oldBlocks = await _parserService.ParseFileAsync(filePath, contentOld);
        var oldBlocksMap = oldBlocks.ToDictionary(b => b.StableId);

        // 4. Align Old Content into Version 0 of Current Blocks
        foreach (var row in rows)
        {
            var currentBlock = row.Current;
            if (oldBlocksMap.TryGetValue(currentBlock.StableId, out var oldBlock))
            {
                var v0 = new CodeBlockVersion
                {
                    Content = oldBlock.Content,
                    Description = oldVersionLabel,
                    Timestamp = DateTime.MinValue,
                    IsOriginal = true
                };
                currentBlock.Versions.Insert(0, v0);
                oldBlocksMap.Remove(currentBlock.StableId);
            }
            else
            {
                // New Block
                var v0 = new CodeBlockVersion
                {
                    Content = string.Empty, // Represents empty/missing in old version
                    Description = oldVersionLabel,
                    Timestamp = DateTime.MinValue,
                    IsOriginal = true
                };
                currentBlock.Versions.Insert(0, v0);
            }
        }

        // Note: Deleted blocks handling is currently skipped as per original logic.

        return (rows, globalHistory);
    }
}
