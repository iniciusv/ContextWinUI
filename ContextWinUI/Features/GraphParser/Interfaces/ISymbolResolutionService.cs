using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser;
public interface ISymbolResolutionService
{
	Task<Models.SymbolResolutionResult?> ResolveSymbolAtPositionAsync(string text, int cursorIndex, int blockStartPos, string filePath);
    Task<List<Models.SymbolResolutionResult>> FindImplementationsAtPositionAsync(string text, int cursorIndex, int blockStartPos, string filePath);
}
