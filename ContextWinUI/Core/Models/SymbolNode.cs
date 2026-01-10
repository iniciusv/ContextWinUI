using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Models;

public readonly record struct SymbolLink(int TargetId, LinkType Type, int Start, int Length);

public class SymbolNode
{
	// OTIMIZAÇÃO #4: Chave primária int (muito mais leve que GUID string)
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public int FileId { get; set; }
	public int SignatureHash { get; set; }

	public SymbolType Type { get; set; }
	public int StartPosition { get; set; }
	public int Length { get; set; }

	public HashSet<SymbolLink> OutgoingLinks { get; set; } = new();

	public SymbolNode? Parent { get; set; }
	public List<SymbolNode> Children { get; set; } = new();

	public SymbolNode(int id, int fileId)
	{
		Id = id;
		FileId = fileId;
	}
}
