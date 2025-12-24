using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class RoslynSemanticDiffService
{
	/// <summary>
	/// Compara semanticamente dois códigos C#.
	/// Retorna apenas as linhas que contém alterações LÓGICAS ou INSERÇÕES.
	/// Ignora mudanças puramente estéticas (espaços, tabs, quebras de linha).
	/// </summary>
	public async Task<List<DiffLine>> ComputeSemanticDiffAsync(string oldCode, string newCode)
	{
		return await Task.Run(() =>
		{
			var diffLines = new List<DiffLine>();

			// 1. Parse das Árvores
			var oldTree = CSharpSyntaxTree.ParseText(oldCode);
			var newTree = CSharpSyntaxTree.ParseText(newCode);

			var oldRoot = oldTree.GetRoot();
			var newRoot = newTree.GetRoot();

			// 2. Mapear linhas do arquivo novo (para preencher o DiffLine)
			// Precisamos saber quantas linhas o novo arquivo tem para preencher os "Unchanged"
			var sourceText = newTree.GetText();
			int totalLines = sourceText.Lines.Count;

			// Inicializa tudo como Unchanged (padrão)
			var lineStatus = new DiffType[totalLines];

			// 3. Comparação de Membros (Smart Diff)
			var newMembers = newRoot.DescendantNodes().OfType<MemberDeclarationSyntax>();

			foreach (var newMember in newMembers)
			{
				// Ignora membros aninhados para não processar duas vezes (ex: processar Classe e depois Método dentro dela)
				// Focamos nos "folhas" lógicas: Métodos, Propriedades, Campos, Construtores.
				if (newMember is TypeDeclarationSyntax || newMember is NamespaceDeclarationSyntax) continue;

				// Tenta encontrar o correspondente no antigo
				var oldMember = FindMatchingMember(oldRoot, newMember);

				if (oldMember == null)
				{
					// NOVO: Não existe no antigo -> Marca linhas como ADDED
					MarkLines(lineStatus, newMember, sourceText, DiffType.Added);
				}
				else
				{
					// EXISTE: Verifica se são semanticamente equivalentes
					// AreEquivalent ignora Trivia (espaços e comentários) por padrão
					bool areEqual = SyntaxFactory.AreEquivalent(oldMember, newMember, topLevel: false);

					if (!areEqual)
					{
						// MODIFICADO: Lógica mudou -> Marca linhas como ADDED (Visualmente Verde)
						// (Podemos usar Modified se quisermos cor diferente, mas Added (Verde) costuma ser o padrão para "Isso é o que vai entrar")
						MarkLines(lineStatus, newMember, sourceText, DiffType.Added);
					}
				}
			}

			// 4. Converte o array de status para a lista de DiffLine esperada pela View
			var rawLines = newCode.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

			for (int i = 0; i < rawLines.Length; i++)
			{
				var type = (i < lineStatus.Length) ? lineStatus[i] : DiffType.Unchanged;

				// Se não foi marcado como Added/Modified, é Unchanged
				if (type == 0) type = DiffType.Unchanged;
				diffLines.Add(new DiffLine
				{
					Text = rawLines[i],
					Type = type,
					// O índice 'i' é baseado em zero, linhas geralmente são baseadas em 1.
					// Como o Semantic Diff mostra o código resultante (novo), usamos NewLineNumber.
					NewLineNumber = i + 1,
					OriginalLineNumber = null
				});
			}

			return diffLines;
		});
	}

	private void MarkLines(DiffType[] statusArray, SyntaxNode node, SourceText sourceText, DiffType type)
	{
		// Pega as linhas que esse nó ocupa no texto NOVO
		var span = node.Span; // Span sem trivia (sem comentários/espaços antes)
							  // Se quiser incluir comentários acima do método na marcação, use node.FullSpan

		var startLine = sourceText.Lines.GetLineFromPosition(span.Start).LineNumber;
		var endLine = sourceText.Lines.GetLineFromPosition(span.End).LineNumber;

		for (int i = startLine; i <= endLine; i++)
		{
			if (i < statusArray.Length)
				statusArray[i] = type;
		}
	}

	private MemberDeclarationSyntax? FindMatchingMember(SyntaxNode oldRoot, MemberDeclarationSyntax newMember)
	{
		// Tenta encontrar um membro no antigo que tenha a mesma "Assinatura"

		if (newMember is MethodDeclarationSyntax newMethod)
		{
			return oldRoot.DescendantNodes()
						  .OfType<MethodDeclarationSyntax>()
						  .FirstOrDefault(m => GenerateSignature(m) == GenerateSignature(newMethod));
		}

		if (newMember is ConstructorDeclarationSyntax newCtor)
		{
			// Construtores comparamos pelo nome da classe + parâmetros
			return oldRoot.DescendantNodes()
						  .OfType<ConstructorDeclarationSyntax>()
						  .FirstOrDefault(c => c.ParameterList.ToString() == newCtor.ParameterList.ToString());
		}

		if (newMember is PropertyDeclarationSyntax newProp)
		{
			return oldRoot.DescendantNodes()
						  .OfType<PropertyDeclarationSyntax>()
						  .FirstOrDefault(p => p.Identifier.Text == newProp.Identifier.Text);
		}

		if (newMember is FieldDeclarationSyntax newField)
		{
			// Campos podem ter múltiplas variáveis "int x, y;"
			// Simplificação: pega o primeiro nome
			var name = newField.Declaration.Variables.FirstOrDefault()?.Identifier.Text;
			return oldRoot.DescendantNodes()
						  .OfType<FieldDeclarationSyntax>()
						  .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.Text == name));
		}

		return null;
	}

	private string GenerateSignature(MethodDeclarationSyntax method)
	{
		// Cria uma string única para identificar o método (Nome + Tipos dos Parâmetros)
		// Ex: "Salvar(String, Int32)"
		var sb = new StringBuilder();
		sb.Append(method.Identifier.Text);
		sb.Append("(");
		sb.Append(string.Join(",", method.ParameterList.Parameters.Select(p => p.Type?.ToString())));
		sb.Append(")");
		return sb.ToString();
	}
}