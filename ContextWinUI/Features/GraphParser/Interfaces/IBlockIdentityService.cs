using ContextWinUI.Core.Models;
using ContextWinUI.Features.GraphParser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.Interfaces;

public interface IBlockIdentityService
{
	/// <summary>
	/// Determina se dois blocos representam a mesma entidade lógica (ignora alterações de conteúdo).
	/// </summary>
	bool AreSameEntity(CodeBlockItem blockA, CodeBlockItem blockB);
	bool AreSameEntity(CodeBlockItem block, SymbolNode node);

	/// <summary>
	/// Gera o hash ou string de assinatura normalizada.
	/// </summary>
	string GetNormalizedSignature(CodeBlockItem block);
}
