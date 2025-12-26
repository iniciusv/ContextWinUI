// ARQUIVO: RoslynSyntaxAnalysisService.cs
using ContextWinUI.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphView
{
	public class RoslynSyntaxAnalysisService : ISyntaxAnalysisService
	{
		public async Task<AnalysisResult> AnalyzeFileAsync(string code, string filePath)
		{
			if (string.IsNullOrWhiteSpace(code)) return AnalysisResult.Empty();

			return await Task.Run(() =>
			{
				var tree = CSharpSyntaxTree.ParseText(code);
				return ProcessSyntaxTree(tree, filePath);
			});
		}

		public async Task<AnalysisResult> AnalyzeSnippetAsync(string snippetCode)
		{
			if (string.IsNullOrWhiteSpace(snippetCode)) return AnalysisResult.Empty();

			return await Task.Run(() =>
			{
				// 1. Tenta parsear direto
				var tree = CSharpSyntaxTree.ParseText(snippetCode);
				var root = tree.GetRoot();

				// 2. Heurística de Envelopamento: Se tiver erros graves de estrutura (top-level statements inválidos em versões antigas ou contexto perdido)
				// Uma verificação simples: se não tem classe nem namespace, envelopamos.
				if (!snippetCode.Contains("class ") && !snippetCode.Contains("namespace ") && !snippetCode.Contains("struct "))
				{
					string wrappedCode = $"class Wrapper {{ void Method() {{ {snippetCode} }} }}";
					var wrappedTree = CSharpSyntaxTree.ParseText(wrappedCode);

					// Nota: Idealmente, ajustaríamos os offsets aqui, mas para visualização rápida, 
					// o parser resiliente do Roslyn no texto original costuma funcionar melhor 
					// para manter as posições corretas do texto do usuário.
					// Vamos tentar processar o original mesmo que incompleto.
				}

				return ProcessSyntaxTree(tree, "snippet.cs");
			});
		}

		private AnalysisResult ProcessSyntaxTree(SyntaxTree tree, string filePath)
		{
			var root = tree.GetRoot();

			// 1. Extrair Escopos (Background)
			var scopeWalker = new ScopeGraphBuilderWalker(filePath);
			scopeWalker.Visit(root);

			// 2. Extrair Tokens (Foreground)
			var tokens = new List<SymbolNode>();
			foreach (var token in root.DescendantTokens())
			{
				var kind = token.Kind();
				SymbolType type = SymbolType.Statement;

				if (token.IsKeyword()) type = SymbolType.Keyword;
				else if (kind == SyntaxKind.IdentifierToken) type = SymbolType.LocalVariable;
				else if (kind == SyntaxKind.StringLiteralToken) type = SymbolType.StringLiteral;
				else if (kind == SyntaxKind.NumericLiteralToken) type = SymbolType.NumericLiteral;
				else if (kind == SyntaxKind.Parameter) type = SymbolType.Parameter;

				tokens.Add(new SymbolNode
				{
					StartPosition = token.SpanStart,
					Length = token.Span.Length,
					Type = type,
					Name = token.Text,
					FilePath = filePath
					// Parent é resolvido visualmente pela estratégia ou Tooltip
				});
			}

			return new AnalysisResult(scopeWalker.AllNodes, tokens);
		}
	}
}