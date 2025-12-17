using ContextWinUI.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ContextWinUI.Helpers;

public static class StructureGeneratorHelper
{
	/// <summary>
	/// Gera uma representação textual da árvore de arquivos.
	/// </summary>
	public static string GenerateTree(IEnumerable<FileSystemItem> items, bool onlyFolders)
	{
		if (items == null || !items.Any()) return string.Empty;

		var sb = new StringBuilder();
		foreach (var item in items)
		{
			BuildNode(sb, item, "", true, onlyFolders);
		}
		return sb.ToString();
	}

	private static void BuildNode(StringBuilder sb, FileSystemItem item, string indent, bool isLast, bool onlyFolders)
	{
		// Se for para mostrar só pastas e o item não é diretório, ignora
		if (onlyFolders && !item.IsDirectory) return;

		// Se for diretório e estiver vazio (ou todos filhos filtrados), talvez queira ocultar?
		// Por enquanto, mostramos a pasta mesmo vazia se ela existe na estrutura carregada.

		sb.Append(indent);
		sb.Append(isLast ? "└── " : "├── ");
		sb.AppendLine(item.Name);

		var nextIndent = indent + (isLast ? "    " : "│   ");

		// Filtra filhos com base na configuração
		var children = item.Children
			.Where(c => !onlyFolders || c.IsDirectory)
			.ToList();

		for (int i = 0; i < children.Count; i++)
		{
			BuildNode(sb, children[i], nextIndent, i == children.Count - 1, onlyFolders);
		}
	}
}