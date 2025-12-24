using ContextWinUI.Core.Models;
using ContextWinUI.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class RoslynSemanticDiffService
{
	private readonly TextDiffService _textDiffService = new();

	public async Task<List<SemanticChangeBlock>> ComputeSemanticDiffAsync(string oldCode, string newCode, DependencyGraph? originalGraph)
	{
		return await Task.Run(() =>
		{
			var blocks = new List<SemanticChangeBlock>();

			// Garante que o código não seja nulo
			oldCode = oldCode ?? string.Empty;
			newCode = newCode ?? string.Empty;

			var oldTree = CSharpSyntaxTree.ParseText(oldCode);
			var newTree = CSharpSyntaxTree.ParseText(newCode);
			var oldRoot = oldTree.GetRoot();
			var newRoot = newTree.GetRoot();

			var newMembers = GetMembers(newRoot);
			var oldMembers = GetMembers(oldRoot);

			// Se não encontrou membros (ex: arquivo vazio ou estrutura não reconhecida),
			// fazemos um diff textual simples do arquivo inteiro como um único bloco.
			if (!newMembers.Any() && !oldMembers.Any())
			{
				var fullDiff = _textDiffService.ComputeDiff(oldCode, newCode);
				blocks.Add(new SemanticChangeBlock("Arquivo Completo", "\uE7C3", DiffType.Placeholder, fullDiff));
				return blocks;
			}

			// 1. Processar Novos e Modificados
			foreach (var newMember in newMembers)
			{
				var newId = GetIdentifier(newMember);

				// Busca no antigo pelo NOME. 
				// Se o nome mudou, infelizmente conta como Delete + Add, o que é correto semanticamente.
				var oldMemberMatch = oldMembers.FirstOrDefault(om => GetIdentifier(om) == newId);

				if (oldMemberMatch != null)
				{
					oldMembers.Remove(oldMemberMatch); // Marca como processado

					if (!AreSemanticallyEquivalent(oldMemberMatch, newMember))
					{
						// MODIFICADO (Laranja)
						var lines = _textDiffService.ComputeDiff(oldMemberMatch.ToFullString(), newMember.ToFullString());
						blocks.Add(CreateBlock(lines, DiffType.Placeholder));
					}
					else
					{
						// IDÊNTICO (Cinza/Transparente)
						// IMPORTANTE: Adicionamos mesmo se igual, para o usuário ver o contexto do arquivo.
						// O diff aqui será apenas linhas "Unchanged".
						var lines = _textDiffService.ComputeDiff(oldMemberMatch.ToFullString(), newMember.ToFullString());
						var block = CreateBlock(lines, DiffType.Unchanged);
						block.IsExpanded = false; // Opcional: Colapsar código não alterado por padrão
						blocks.Add(block);
					}
				}
				else
				{
					// ADICIONADO (Verde)
					var lines = _textDiffService.ComputeDiff("", newMember.ToFullString());
					blocks.Add(CreateBlock(lines, DiffType.Added));
				}
			}

			// 2. Processar Deletados (O que sobrou na lista antiga)
			foreach (var deletedMember in oldMembers)
			{
				// DELETADO (Vermelho)
				var lines = _textDiffService.ComputeDiff(deletedMember.ToFullString(), "");
				blocks.Add(CreateBlock(lines, DiffType.Deleted));
			}

			return blocks;
		});
	}

	private List<MemberDeclarationSyntax> GetMembers(SyntaxNode root)
	{
		// Pega membros de classes e namespaces recursivamente
		return root.DescendantNodes()
				   .OfType<MemberDeclarationSyntax>()
				   .Where(m => m is MethodDeclarationSyntax ||
							   m is PropertyDeclarationSyntax ||
							   m is ConstructorDeclarationSyntax ||
							   m is FieldDeclarationSyntax ||
							   m is EventFieldDeclarationSyntax ||
							   m is DelegateDeclarationSyntax ||
							   m is EnumDeclarationSyntax ||
							   m is ClassDeclarationSyntax && IsTopLevelClass(m)) // Se quiser tratar classes aninhadas ou top level
				   .ToList();
	}

	// Evita pegar a classe "wrapper" inteira como um único bloco, queremos o conteúdo dela
	private bool IsTopLevelClass(MemberDeclarationSyntax m)
	{
		// Se for uma classe dentro de namespace, ok. Se for classe aninhada, talvez queiramos tratar diferente.
		// Por simplificação, vamos ignorar classes inteiras no diff de membros e focar nos métodos internos,
		// a não ser que seja um Enum ou Delegate.
		return false;
	}

	private string GetIdentifier(MemberDeclarationSyntax member)
	{
		try
		{
			if (member is MethodDeclarationSyntax m) return m.Identifier.Text + m.ParameterList.ToString(); // Inclui params para diferenciar overloads
			if (member is PropertyDeclarationSyntax p) return p.Identifier.Text;
			if (member is ConstructorDeclarationSyntax c) return c.Identifier.Text + c.ParameterList.ToString();
			if (member is FieldDeclarationSyntax f) return f.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "";
			if (member is EventFieldDeclarationSyntax e) return e.Declaration.Variables.FirstOrDefault()?.Identifier.Text ?? "";
			if (member is DelegateDeclarationSyntax d) return d.Identifier.Text;
			if (member is EnumDeclarationSyntax en) return en.Identifier.Text;
		}
		catch { return ""; }
		return "";
	}

	private SemanticChangeBlock CreateBlock(List<DiffLine> lines, DiffType typeOverride)
	{
		var finalType = typeOverride;

		// Refinamento de tipo se for Placeholder
		if (finalType == DiffType.Placeholder)
		{
			bool hasAdds = lines.Any(x => x.Type == DiffType.Added);
			bool hasDels = lines.Any(x => x.Type == DiffType.Deleted);

			if (hasAdds && !hasDels) finalType = DiffType.Added;
			else if (hasDels && !hasAdds) finalType = DiffType.Deleted;
			else if (!hasAdds && !hasDels) finalType = DiffType.Unchanged;
			// Se tiver os dois, mantemos Placeholder (que vira Laranja/Misto na UI)
		}

		return new SemanticChangeBlock(string.Empty, string.Empty, finalType, lines);
	}

	private bool AreSemanticallyEquivalent(SyntaxNode oldNode, SyntaxNode newNode)
	{
		// Remove espaços em branco
		return oldNode.ToFullString().Trim() == newNode.ToFullString().Trim();
	}
}