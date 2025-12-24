namespace ContextWinUI.Services;

public class DiffLine
{
	public string Text { get; set; } = string.Empty;
	public DiffType Type { get; set; }
	public int? OriginalLineNumber { get; set; }
	public int? NewLineNumber { get; set; }
}
