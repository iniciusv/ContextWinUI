using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Features.CodeEditor.Highlight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser;

public interface IHighlightingOrchestrator
{
	Task<List<HighlightSpan>> CalculateHighlightsAsync(string text, string extension, bool isDarkTheme, SemanticHighlightService? semanticService, Dictionary<string, object> themeStyles);
}
