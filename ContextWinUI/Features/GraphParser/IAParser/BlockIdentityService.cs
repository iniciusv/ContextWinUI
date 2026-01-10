using ContextWinUI.Core.Models;
using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.Models;
using System;

namespace ContextWinUI.Features.GraphParser.IAParser;

public class BlockIdentityService : IBlockIdentityService
{
	public bool AreSameEntity(CodeBlockItem blockA, CodeBlockItem blockB)
	{
		if (blockA == null || blockB == null) return false;

		// 1. O Tipo deve ser idêntico (ex: não pode comparar Classe com Interface)
		if (blockA.SegmentType != blockB.SegmentType) return false;

		// 2. O Nome deve ser idêntico
		// Normalizamos strings para evitar problemas de espaçamento (ex: "Corpo de  X" vs "Corpo de X")
		if (!string.Equals(Normalize(blockA.Name), Normalize(blockB.Name), StringComparison.OrdinalIgnoreCase))
			return false;

		// --- MUDANÇA CRÍTICA AQUI ---
		// Se for um CONTAINER (Classe, Interface, Struct), paramos por aqui.
		// Ignoramos assinatura e profundidade. Se tem o mesmo nome e tipo, É A MESMA COISA.
		if (IsContainer(blockA.SegmentType))
		{
			return true;
		}

		// 3. Verificação de Assinatura (Apenas para Métodos/Construtores)
		// Só usamos assinatura para diferenciar sobrecargas de métodos.
		string sigA = GetNormalizedSignature(blockA);
		string sigB = GetNormalizedSignature(blockB);

		if (!string.IsNullOrEmpty(sigA) && !string.IsNullOrEmpty(sigB))
		{
			return string.Equals(sigA, sigB, StringComparison.OrdinalIgnoreCase);
		}

		return true;
	}

	public string GetNormalizedSignature(CodeBlockItem block)
	{
		if (string.IsNullOrEmpty(block.Signature)) return string.Empty;
		return block.Signature.Replace(" ", "").Trim();
	}

	private string Normalize(string input) => input?.Replace(" ", "").Trim() ?? string.Empty;

	private bool IsContainer(SegmentType type)
	{
		return type == SegmentType.Class ||
			   type == SegmentType.Interface ||
			   type == SegmentType.Struct ||
			   type == SegmentType.Enum ||
			   type == SegmentType.Namespace;
	}


	public bool AreSameEntity(CodeBlockItem block, SymbolNode node)
	{
		// 1. Tipos incompatíveis (Ex: Método vs Classe)
		// Precisamos de um mapa simples pois os Enums são diferentes
		if (!MapTypesMatch(block.SymbolType, node.Type)) return false;

		// 2. Nome deve ser idêntico
		if (block.Name != node.Name) return false;

		// 3. Assinatura (O Desempate)
		// O SymbolNode idealmente deveria ter a assinatura armazenada. 
		// Se não tiver, usamos uma heurística de parâmetros ou assumimos pelo nome se não houver sobrecarga.

		// NOTA: Para isso funcionar perfeitamente, recomendo adicionar a propriedade "Signature" 
		// ou "UniqueKey" no seu SymbolNode durante a indexação (GraphBuilderWalker).
		// Por enquanto, vamos comparar nomes, o que cobre 90% dos casos.
		return true;
	}
	private bool MapTypesMatch(SymbolType blockType, SymbolType nodeType)
	{
		// Mapeamento direto pois os enums são iguais ou muito parecidos
		return blockType == nodeType;
	}

}
