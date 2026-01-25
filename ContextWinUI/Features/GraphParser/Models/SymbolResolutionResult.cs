namespace ContextWinUI.Features.GraphParser.Models;

public class SymbolResolutionResult
{
    public string SymbolName { get; set; } = string.Empty;
    public string SymbolType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int AbsolutePosition { get; set; }
    public bool IsImplementation { get; set; }

    public SymbolResolutionResult() { }

    public SymbolResolutionResult(string symbolName, string symbolType, string filePath, int absolutePosition, bool isImplementation)
    {
        SymbolName = symbolName;
        SymbolType = symbolType;
        FilePath = filePath;
        AbsolutePosition = absolutePosition;
        IsImplementation = isImplementation;
    }
}
