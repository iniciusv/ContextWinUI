namespace ContextWinUI.Features.GraphParser;
public interface ISymbolResolutionService
{
	string? ResolveSymbolAtPosition(string text, int cursorIndex, int blockStartPos, string filePath);
}
