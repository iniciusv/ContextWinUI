using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Services;

public class TextDiffService
{
	public List<DiffLine> ComputeDiff(string oldText, string newText)
	{
		var oldLines = SplitLines(oldText);
		var newLines = SplitLines(newText);

		// Algoritmo LCS simplificado para Diff
		var matrix = LCSMatrix(oldLines, newLines);
		var diff = new List<DiffLine>();

		Backtrack(matrix, oldLines, newLines, oldLines.Count, newLines.Count, diff);

		diff.Reverse(); // O backtrack gera invertido
		return diff;
	}

	private List<string> SplitLines(string text)
	{
		if (string.IsNullOrEmpty(text)) return new List<string>();
		return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
	}

	private int[,] LCSMatrix(List<string> oldL, List<string> newL)
	{
		int[,] c = new int[oldL.Count + 1, newL.Count + 1];
		for (int i = 1; i <= oldL.Count; i++)
		{
			for (int j = 1; j <= newL.Count; j++)
			{
				if (oldL[i - 1] == newL[j - 1])
					c[i, j] = c[i - 1, j - 1] + 1;
				else
					c[i, j] = Math.Max(c[i, j - 1], c[i - 1, j]);
			}
		}
		return c;
	}

	private void Backtrack(int[,] C, List<string> oldL, List<string> newL, int i, int j, List<DiffLine> diff)
	{
		while (i > 0 || j > 0)
		{
			if (i > 0 && j > 0 && oldL[i - 1] == newL[j - 1])
			{
				diff.Add(new DiffLine
				{
					Text = "  " + oldL[i - 1], // Prefixo de espaço para alinhar
					Type = DiffType.Unchanged,
					OriginalLineNumber = i,
					NewLineNumber = j
				});
				i--; j--;
			}
			else if (j > 0 && (i == 0 || C[i, j - 1] >= C[i - 1, j]))
			{
				diff.Add(new DiffLine
				{
					Text = "+ " + newL[j - 1],
					Type = DiffType.Added,
					OriginalLineNumber = null,
					NewLineNumber = j
				});
				j--;
			}
			else if (i > 0 && (j == 0 || C[i, j - 1] < C[i - 1, j]))
			{
				diff.Add(new DiffLine
				{
					Text = "- " + oldL[i - 1],
					Type = DiffType.Deleted,
					OriginalLineNumber = i,
					NewLineNumber = null
				});
				i--;
			}
		}
	}
}