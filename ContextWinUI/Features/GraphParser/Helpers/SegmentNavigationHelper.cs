using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.Helpers;

public class SegmentNavigationHelper
{
    private readonly ISymbolResolutionService _symbolService;

    public SegmentNavigationHelper(ISymbolResolutionService symbolService)
    {
        _symbolService = symbolService;
    }

    public async Task<SymbolResolutionResult?> ResolveSymbolAtPositionAsync(string text, int cursorIndex, int startPos, string filePath)
    {
        return await _symbolService.ResolveSymbolAtPositionAsync(text, cursorIndex, startPos, filePath);
    }

    public async Task<NavigationResult> HandleNavigationRequestAsync(string text, int cursorIndex, int startPos, string filePath, bool isImplementation)
    {
        var result = new NavigationResult();

        if (isImplementation)
        {
            var implementations = await _symbolService.FindImplementationsAtPositionAsync(text, cursorIndex, startPos, filePath);

            if (implementations.Count == 1)
            {
                result.SingleTarget = implementations[0];
            }
            else if (implementations.Count > 1)
            {
                result.Candidates = implementations;
                result.Message = $"{implementations.Count} implementations found. Select in footer.";
            }
            else
            {
                result.Message = "No implementation found.";
            }
        }
        else
        {
            // Definition
            var def = await _symbolService.ResolveSymbolAtPositionAsync(text, cursorIndex, startPos, filePath);
            if (def != null)
            {
                result.SingleTarget = def;
            }
        }

        return result;
    }
}

public class NavigationResult
{
    public SymbolResolutionResult? SingleTarget { get; set; }
    public List<SymbolResolutionResult> Candidates { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
