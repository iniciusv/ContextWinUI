using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Services;

/// <summary>
/// Tipos de alteração para cada linha.
/// </summary>
public enum DiffType
{
	Unchanged, // Linha existe em ambos e é idêntica
	Added,     // Linha existe apenas no novo (verde)
	Deleted,   // Linha existia no antigo (não exibido no editor do novo arquivo)
	Modified   // (Opcional) Pode ser tratado como Delete + Add
}

/// <summary>
/// Representa uma linha processada pelo algoritmo de Diff.
/// </summary>
public class DiffLine
{
	public string Text { get; set; } = string.Empty;
	public DiffType Type { get; set; }
	public int Index { get; set; } // Índice da linha no contexto analisado
}

public class TextDiffService
{
	/// <summary>
	/// Compara o texto antigo com o novo e retorna uma lista de linhas
	/// alinhada com a estrutura do 'newText'.
	/// </summary>
	public List<DiffLine> ComputeDiff(string oldText, string newText)
	{
		// 1. Normalização e Split
		// É vital tratar nulos e quebras de linha de forma unificada
		var oldLines = SplitLines(oldText ?? "");
		var newLines = SplitLines(newText ?? "");

		// 2. Construção da Matriz LCS (Longest Common Subsequence)
		// A matriz ajuda a encontrar a maior sequência de linhas idênticas
		int[,] matrix = new int[oldLines.Count + 1, newLines.Count + 1];

		for (int i = 1; i <= oldLines.Count; i++)
		{
			for (int j = 1; j <= newLines.Count; j++)
			{
				// Comparação exata de string (Case sensitive)
				if (oldLines[i - 1] == newLines[j - 1])
				{
					matrix[i, j] = matrix[i - 1, j - 1] + 1;
				}
				else
				{
					matrix[i, j] = Math.Max(matrix[i - 1, j], matrix[i, j - 1]);
				}
			}
		}

		// 3. Backtracking para gerar a lista de Diff
		// Percorremos a matriz de trás para frente para reconstruir o caminho
		var rawDiff = new List<DiffLine>();
		int x = oldLines.Count;
		int y = newLines.Count;

		while (x > 0 && y > 0)
		{
			string oldL = oldLines[x - 1];
			string newL = newLines[y - 1];

			if (oldL == newL)
			{
				// Match: A linha existe em ambos
				rawDiff.Add(new DiffLine { Text = newL, Type = DiffType.Unchanged, Index = y - 1 });
				x--;
				y--;
			}
			else if (matrix[x - 1, y] >= matrix[x, y - 1])
			{
				// Movimento para Cima: Significa que a linha estava em 'old' mas não em 'new'.
				// É uma REMOÇÃO. 
				// Como estamos exibindo o arquivo NOVO, ignoramos linhas removidas na lista visual.
				x--;
			}
			else
			{
				// Movimento para Esquerda: Significa que a linha está em 'new' mas não em 'old'.
				// É uma ADIÇÃO.
				rawDiff.Add(new DiffLine { Text = newL, Type = DiffType.Added, Index = y - 1 });
				y--;
			}
		}

		// Se sobraram linhas no eixo Y (New), são adições no início do arquivo
		while (y > 0)
		{
			rawDiff.Add(new DiffLine { Text = newLines[y - 1], Type = DiffType.Added, Index = y - 1 });
			y--;
		}

		// Como o backtrack é reverso, invertemos para a ordem correta de leitura
		rawDiff.Reverse();

		return rawDiff;
	}

	/// <summary>
	/// Divide o texto em linhas lidando robustamente com \r, \n e \r\n.
	/// Isso evita falsos positivos no Diff por causa de encoding.
	/// </summary>
	private List<string> SplitLines(string text)
	{
		// Normaliza tudo para \n primeiro, depois divide
		// O Replace duplo garante que \r\n vire \n, e \r isolado (Mac antigo/WinUI interno) vire \n
		string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");

		// Split mantendo linhas vazias (importante para formatação de código)
		return normalized.Split('\n').ToList();
	}
}