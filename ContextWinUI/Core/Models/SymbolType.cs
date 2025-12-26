namespace ContextWinUI.Core.Models; // Ajuste para seu namespace real se for diferente

public enum SymbolType
{
	// Estruturais
	Class,
	Interface,
	Method,
	Property,
	Field,
	Constructor,

	// Granulares (Novos)
	LocalVariable,
	Parameter,
	ControlFlow,     // if, for, while
	Statement,       // return, throw
	Keyword,         // public, static, var
	StringLiteral,   // "texto"
	NumericLiteral   // 123
}