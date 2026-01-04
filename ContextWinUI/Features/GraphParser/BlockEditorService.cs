using ContextWinUI.Core.Models;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser;

public class BlockEditorService : IBlockEditorService
{
	public CodeBlockItem? AddSiblingBlock(IList<SegmentRowViewModel> rows, CodeBlockItem referenceBlock)
	{
		if (referenceBlock == null) return null;

		var parentRow = rows.FirstOrDefault(r => r.Current == referenceBlock);
		if (parentRow == null) return null;

		int index = rows.IndexOf(parentRow);

		// Criação do novo bloco com indentação correta baseada no irmão
		var newBlock = new CodeBlockItem
		{
			Name = "NovaFuncionalidade",
			SegmentType = SegmentType.Method,
			SymbolType = SymbolType.Method,
			DepthLevel = referenceBlock.DepthLevel,
			FileExtension = referenceBlock.FileExtension,
			TypeDescription = "NOVO MÉTODO",
			// Cria um conteúdo vazio com a indentação correta
			Content = $"\n{new string('\t', referenceBlock.DepthLevel)}"
		};

		// Inicializa histórico como novo
		newBlock.InitializeVersions(string.Empty);

		// Insere na lista visual
		var newRow = new SegmentRowViewModel(newBlock);
		rows.Insert(index + 1, newRow);

		return newBlock;
	}

	public bool DeleteBlock(IList<SegmentRowViewModel> rows, CodeBlockItem block)
	{
		if (block == null) return false;

		var row = rows.FirstOrDefault(r => r.Current == block);
		if (row == null) return false;

		// Regra de Ouro: Se tem versão original, não removemos a linha, apenas limpamos o conteúdo.
		bool hasOriginalVersion = block.Versions.Any(v => v.IsOriginal);

		if (hasOriginalVersion)
		{
			// Soft Delete: Mantém o row para o Diff mostrar "Original vs Vazio"
			block.Content = string.Empty;
			return false; // Não foi removido da lista
		}
		else
		{
			// Hard Delete: Bloco rascunho sem histórico, pode sumir.
			rows.Remove(row);
			return true; // Foi removido da lista
		}
	}

	public void LinkBlocks(IList<SegmentRowViewModel> rows, CodeBlockItem source, CodeBlockItem target)
	{
		if (source == null || target == null || source == target) return;

		// 1. Merge de Conteúdo
		target.Content = source.Content;
		// Opcional: Copiar nome se fizer sentido
		// target.Name = source.Name; 

		// 2. Tratar o Bloco de Origem (Source)
		// Usamos a mesma lógica do DeleteBlock aqui
		DeleteBlock(rows, source);
	}

	public void PromoteEditToNewBlock(IList<SegmentRowViewModel> rows, CodeBlockItem block)
	{
		if (block == null) return;

		// Gera nova identidade
		block.Id = Guid.NewGuid().ToString();

		// Reinicia o histórico considerando o conteúdo ATUAL como o "Original" deste novo bloco
		block.InitializeVersions(block.Content);

		// Atualiza metadados visuais
		block.TypeDescription += " (Novo)";
		block.Name += " *";

		// Remove a referência visual (coluna da esquerda), pois agora é um bloco novo sem antepassado
		var row = rows.FirstOrDefault(r => r.Current == block);
		if (row != null)
		{
			row.Reference = null;
		}
	}

	public bool RevertBlockToOriginal(CodeBlockItem block)
	{
		if (block == null) return false;
		return block.RestoreOriginal();
	}
}