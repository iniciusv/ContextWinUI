using System.Collections.Generic;
using System.Linq;
using ContextWinUI.Core.Models;
using Microsoft.CodeAnalysis;
namespace ContextWinUI.Features.GraphView;



public static class AnalysisExtensions
{
	public static int BinarySearchFirstOccurrence(this List<SymbolNode> list, int startPosition)
	{
		int min = 0;
		int max = list.Count - 1;
		int result = -1;

		while (min <= max)
		{
			int mid = min + (max - min) / 2;
			if (list[mid].StartPosition >= startPosition)
			{
				result = mid; // Candidato encontrado, mas tentamos achar um anterior
				max = mid - 1;
			}
			else
			{
				min = mid + 1;
			}
		}
		return result;
	}

	public static string GetOptimizedNodeHash(this SyntaxNode node)
	{
		unchecked
		{
			int hash = 17;
			hash = hash * 23 + node.RawKind;

			if (!node.ChildNodes().Any())
			{
				hash = hash * 23 + node.ToString().Trim().GetHashCode();
			}

			return $"{node.GetType().Name}_{hash}";
		}
	}
}