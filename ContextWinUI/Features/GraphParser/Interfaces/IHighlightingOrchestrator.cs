using ColorCode.Styling;
using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Features.CodeEditor.Highlight;
using Microsoft.UI.Xaml.Controls;
using System.Threading;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser;

public interface IHighlightingOrchestrator
{
	Task HighlightEditorAsync(
		RichEditBox editor,
		string text,
		string extension,
		SemanticHighlightService? semanticService,
		bool isDarkTheme,
		StyleDictionary themeStyles,
		CancellationToken token,
		string? originalContent = null); // <--- Novo parâmetro
}