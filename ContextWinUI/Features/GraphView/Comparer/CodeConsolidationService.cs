using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphView;

public class CodeConsolidationService : ICodeConsolidationService
{
	public string MergeMultipleContexts(string originalContent, string fullSnippet, IEnumerable<ContextActionViewModel> actions)
	{
		if (string.IsNullOrEmpty(originalContent)) return fullSnippet;

		// Ordenamos do FIM para o INÍCIO (Descending) para não invalidar os Offsets
		var sortedActions = actions.OrderByDescending(a => a.Match.FileScope.StartPosition).ToList();

		var result = new StringBuilder(originalContent);

		foreach (var action in sortedActions)
		{
			var fileScope = action.Match.FileScope;
			var snippetScope = action.Match.SnippetScope;

			// Extraímos a parte do snippet que corresponde a este match
			string snippetPart = fullSnippet.Substring(snippetScope.StartPosition, snippetScope.Length);

			switch (action.SelectedAction)
			{
				case "V": // SUBSTITUIR
					result.Remove(fileScope.StartPosition, fileScope.Length);
					result.Insert(fileScope.StartPosition, snippetPart);
					break;

				case "+": // INSERIR (Mantém o original e adiciona o novo abaixo)
					result.Insert(fileScope.StartPosition + fileScope.Length, "\n\n" + snippetPart);
					break;

				case "*": // MESCLAGEM (Simulado: Adiciona comentário de revisão)
						  // No futuro, aqui chamaria o TokenDiffEngine para mesclar linha a linha
					string merged = $"/* REVISAR MESCLAGEM */\n{snippetPart}\n/* FIM REVISÃO */";
					result.Remove(fileScope.StartPosition, fileScope.Length);
					result.Insert(fileScope.StartPosition, merged);
					break;
			}
		}

		return result.ToString();
	}
}