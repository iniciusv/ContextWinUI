using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser;

public interface IBlockEditorService
{
	/// <summary>
	/// Adiciona um novo bloco vazio logo após o bloco de referência.
	/// </summary>
	CodeBlockItem? AddSiblingBlock(IList<SegmentRowViewModel> rows, CodeBlockItem referenceBlock);

	/// <summary>
	/// Remove um bloco. Realiza "Soft Delete" (limpa conteúdo) se houver histórico original,
	/// ou "Hard Delete" (remove da lista) se for um bloco novo.
	/// </summary>
	/// <returns>Retorna true se o bloco foi removido da lista (hard delete).</returns>
	bool DeleteBlock(IList<SegmentRowViewModel> rows, CodeBlockItem block);

	/// <summary>
	/// Copia o conteúdo da origem para o destino e trata a exclusão da origem.
	/// </summary>
	void LinkBlocks(IList<SegmentRowViewModel> rows, CodeBlockItem source, CodeBlockItem target);

	/// <summary>
	/// Desvincula o bloco do seu histórico original, tratando-o como um novo bloco inserido.
	/// </summary>
	void PromoteEditToNewBlock(IList<SegmentRowViewModel> rows, CodeBlockItem block);

	/// <summary>
	/// Tenta reverter o bloco para sua versão original.
	/// </summary>
	bool RevertBlockToOriginal(CodeBlockItem block);
}