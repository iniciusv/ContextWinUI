//// ARQUIVO: ContextWinUI/Features/Parser/Services/SemanticBlockDiffService.cs
//using ContextWinUI.Models;
//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
//using Microsoft.CodeAnalysis.Text;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;

//namespace ContextWinUI.Services;

//public class SemanticBlockDiffService
//{
//	private const int ContextLines = 2; // Reduzi um pouco para evitar fusão excessiva

//	public async Task<List<SemanticChangeBlock>> ComputeBlocksAsync(string oldText, string newText)
//	{
//		// 1. Calcular o Diff bruto linha a linha (Texto)
//		var rawDiffs = await Task.Run(() => ComputeDiff(SplitLines(oldText), SplitLines(newText)));

//		// 2. Mapear as fronteiras dos métodos no novo arquivo (Semântica)
//		var methodBoundaries = await Task.Run(() => GetSemanticBoundaries(newText));

//		// 3. Agrupar diffs respeitando as fronteiras semânticas
//		return GroupDiffsIntoBlocks(rawDiffs, methodBoundaries);
//	}

//	private List<SemanticChangeBlock> GroupDiffsIntoBlocks(List<DiffLine> rawDiffs, List<TextSpan> boundaries)
//	{
//		var resultBlocks = new List<SemanticChangeBlock>();
//		if (!rawDiffs.Any()) return resultBlocks;

//		var currentChunk = new List<DiffLine>();
//		string lastContextId = null; // Identificador do "bloco semântico" atual

//		for (int i = 0; i < rawDiffs.Count; i++)
//		{
//			var line = rawDiffs[i];

//			// Identifica em qual método/classe essa linha está
//			string currentContextId = GetContextIdForLine(line, boundaries);

//			bool isChange = line.Type != DiffType.Unchanged;

//			// SEJAMOS ESTRITOS: Se mudou de método (contexto semântico), força a quebra do bloco anterior
//			if (isChange && lastContextId != null && currentContextId != lastContextId && currentChunk.Any(x => x.Type != DiffType.Unchanged))
//			{
//				// Finaliza o bloco anterior antes de começar o novo contexto
//				resultBlocks.Add(new SemanticChangeBlock(TrimExcessContext(currentChunk)));
//				currentChunk = new List<DiffLine>();
//			}

//			if (isChange)
//			{
//				lastContextId = currentContextId; // Atualiza onde estamos "trabalhando" agora

//				// Adiciona contexto anterior se o chunk estiver vazio
//				if (currentChunk.Count == 0)
//				{
//					int contextStart = Math.Max(0, i - ContextLines);
//					for (int k = contextStart; k < i; k++)
//					{
//						// Só adiciona se não pertencer a um bloco já fechado (lógica simplificada)
//						currentChunk.Add(rawDiffs[k]);
//					}
//				}
//				currentChunk.Add(line);
//			}
//			else
//			{
//				// Linha inalterada (Contexto ou Gap)
//				if (currentChunk.Any(x => x.Type != DiffType.Unchanged))
//				{
//					// Estamos dentro de um bloco aberto.
//					// Verifica se esta linha inalterada está dentro do limite de contexto
//					int lastChangeIndex = currentChunk.FindLastIndex(x => x.Type != DiffType.Unchanged);
//					int distance = currentChunk.Count - 1 - lastChangeIndex;

//					// Se a distância for pequena, mantemos como contexto.
//					// Se for grande, fechamos o bloco.
//					if (distance < ContextLines)
//					{
//						currentChunk.Add(line);
//					}
//					else
//					{
//						// Gap muito grande -> Fecha bloco
//						resultBlocks.Add(new SemanticChangeBlock(TrimExcessContext(currentChunk)));
//						currentChunk = new List<DiffLine>();
//						lastContextId = null; // Resetamos o contexto pois estamos num "vazio"
//					}
//				}
//			}
//		}

//		if (currentChunk.Any(x => x.Type != DiffType.Unchanged))
//		{
//			resultBlocks.Add(new SemanticChangeBlock(TrimExcessContext(currentChunk)));
//		}

//		return resultBlocks;
//	}

//	// Identifica o ID do método baseando-se no número da linha NOVA
//	private string GetContextIdForLine(DiffLine line, List<TextSpan> boundaries)
//	{
//		if (line.Type == DiffType.Deleted || !line.NewLineNumber.HasValue)
//			return "deleted_unknown"; // Deletados flutuam, geralmente agrupam com o anterior

//		int lineIndex = line.NewLineNumber.Value - 1; // 0-based para comparação

//		// Procura em qual span (método) essa linha cai
//		// O formato do TextSpan aqui é simplificado para (StartLine, EndLine)
//		var boundary = boundaries.FirstOrDefault(b => lineIndex >= b.Start && lineIndex <= b.Length);

//		if (boundary != default)
//			return $"method_{boundary.Start}"; // Retorna um ID único para aquele método

//		return "global";
//	}

//	// Usa Roslyn para encontrar onde começam/terminam métodos e propriedades
//	private List<TextSpan> GetSemanticBoundaries(string code)
//	{
//		var list = new List<TextSpan>();
//		if (string.IsNullOrWhiteSpace(code)) return list;

//		try
//		{
//			var tree = CSharpSyntaxTree.ParseText(code);
//			var root = tree.GetRoot();

//			// Pega métodos, propriedades, construtores
//			var nodes = root.DescendantNodes()
//							.Where(n => n is MethodDeclarationSyntax ||
//										n is PropertyDeclarationSyntax ||
//										n is ConstructorDeclarationSyntax);

//			foreach (var node in nodes)
//			{
//				var lineSpan = node.GetLocation().GetLineSpan();
//				int startLine = lineSpan.StartLinePosition.Line;
//				int endLine = lineSpan.EndLinePosition.Line;

//				// Usaremos TextSpan como struct simples: Start = Linha Inicial, Length = Linha Final
//				list.Add(new TextSpan(startLine, endLine));
//			}
//		}
//		catch
//		{
//			// Fallback silencioso se o código estiver quebrado
//		}
//		return list;
//	}

//	// Limpa linhas de contexto excessivas do final do bloco
//	private List<DiffLine> TrimExcessContext(List<DiffLine> lines)
//	{
//		// Se as ultimas linhas forem unchanged, garante que não excede o limite
//		// (Isso é opcional, mas ajuda na estética)
//		return lines;
//	}

//	private List<string> SplitLines(string text)
//	{
//		if (string.IsNullOrEmpty(text)) return new List<string>();
//		return text.Replace("\r\n", "\n").Split('\n').ToList();
//	}

//	// Algoritmo de Diff (Mesmo da resposta anterior)
//	private List<DiffLine> ComputeDiff(List<string> oldLines, List<string> newLines)
//	{
//		int[,] lcsMatrix = new int[oldLines.Count + 1, newLines.Count + 1];

//		for (int i = 1; i <= oldLines.Count; i++)
//		{
//			for (int j = 1; j <= newLines.Count; j++)
//			{
//				if (oldLines[i - 1] == newLines[j - 1])
//					lcsMatrix[i, j] = lcsMatrix[i - 1, j - 1] + 1;
//				else
//					lcsMatrix[i, j] = Math.Max(lcsMatrix[i - 1, j], lcsMatrix[i, j - 1]);
//			}
//		}

//		var diffLines = new List<DiffLine>();
//		int x = oldLines.Count;
//		int y = newLines.Count;

//		while (x > 0 || y > 0)
//		{
//			if (x > 0 && y > 0 && oldLines[x - 1] == newLines[y - 1])
//			{
//				diffLines.Add(new DiffLine
//				{
//					Text = "  " + oldLines[x - 1],
//					Type = DiffType.Unchanged,
//					OriginalLineNumber = x,
//					NewLineNumber = y
//				});
//				x--; y--;
//			}
//			else if (y > 0 && (x == 0 || lcsMatrix[x, y - 1] >= lcsMatrix[x - 1, y]))
//			{
//				diffLines.Add(new DiffLine
//				{
//					Text = "+ " + newLines[y - 1],
//					Type = DiffType.Added,
//					NewLineNumber = y
//				});
//				y--;
//			}
//			else if (x > 0 && (y == 0 || lcsMatrix[x, y - 1] < lcsMatrix[x - 1, y]))
//			{
//				diffLines.Add(new DiffLine
//				{
//					Text = "- " + oldLines[x - 1],
//					Type = DiffType.Deleted,
//					OriginalLineNumber = x
//				});
//				x--;
//			}
//		}
//		diffLines.Reverse();
//		return diffLines;
//	}
//}