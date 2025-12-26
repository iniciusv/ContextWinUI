using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Models;
public record SymbolLink(string TargetId, LinkType Type, int Start, int Length);

public class SymbolNode
{
	public string Id { get; set; } = Guid.NewGuid().ToString();
	public string Name { get; set; } = string.Empty;
	public string FilePath { get; set; } = string.Empty;
	public SymbolType Type { get; set; }
	public int StartPosition { get; set; }
	public int Length { get; set; }

	// Mantém compatibilidade com o grafo de dependências original
	public HashSet<SymbolLink> OutgoingLinks { get; set; } = new();

	// --- NOVAS PROPRIEDADES PARA HIERARQUIA (Correção do erro) ---

	// Referência ao pai (ex: O IfStatement sabe que está dentro do Method)
	public SymbolNode? Parent { get; set; }

	// Lista de filhos na ordem em que aparecem no código
	public List<SymbolNode> Children { get; set; } = new();
}
