// ARQUIVO: ContextWinUI/Features/GraphParser/Models/SegmentType.cs
namespace ContextWinUI.Features.GraphParser.Models;

public enum SegmentType
{
	Namespace,
	Using,
	Class,
	FileHeader,     // Usings, Comentários de topo
	ClassHeader,    // Assinatura da classe e chave de abertura
	Method,         // Um método completo (pode ser atômico ou quebrado se quiser profundidade)
	Property,       // Uma propriedade completa
	Field,          // Campos
	Trivia,         // Espaços em branco, quebras de linha entre membros
	CloseBrace,     // Fechamento de escopo '}'
	Gap,             // Qualquer código não identificado especificamente
	Enum,
	Constructor,
	Comment
}

// Enums de apoio (caso não tenha definido em outro lugar)
