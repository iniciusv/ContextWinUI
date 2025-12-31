using Windows.UI;

namespace ContextWinUI.Features.CodeEditor;

public struct HighlightSpan
{
	public int Start;
	public int Length;
	public Color Color;
	public SpanType Type { get; set; }

	// ADICIONE ESTE CONSTRUTOR
	public HighlightSpan(int start, int length, Color color)
	{
		Start = start;
		Length = length;
		Color = color;
	}
}

public enum SpanType
{
	Keyword,
	Comment,
	String,
	Number,
	PlainText,
	Class,      
	Method,     
	Interface   
}