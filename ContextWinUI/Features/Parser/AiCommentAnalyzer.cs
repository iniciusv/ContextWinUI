using ContextWinUI.Models;
using System.Text.RegularExpressions;

namespace ContextWinUI.Services;

public class AiCommentAnalyzer
{
	// Padrões de Regex para identificar intenções
	private static readonly Regex SnippetMarkerRegex = new Regex(
		@"^\s*\/\/\s*\.{3}|^\s*\/\/\s*etc|^\s*\/\/\s*restante",
		RegexOptions.Multiline | RegexOptions.IgnoreCase);

	private static readonly Regex DeleteMarkerRegex = new Regex(
		@"\/\/\s*(?:DELETAR|DELETE|REMOVER|APAGAR|REMOVED)",
		RegexOptions.IgnoreCase);

	private static readonly Regex CorrectionMarkerRegex = new Regex(
		@"\/\/\s*(?:CORREÇÃO|FIX|CORRIGIDO|BUGFIX)",
		RegexOptions.IgnoreCase);

	private static readonly Regex AddMarkerRegex = new Regex(
		@"\/\/\s*(?:ADICIONAR|ADD|NOVO|NEW)",
		RegexOptions.IgnoreCase);

	private static readonly Regex TodoMarkerRegex = new Regex(
		@"\/\/\s*(?:TODO|FAZER|IMPLEMENTAR)",
		RegexOptions.IgnoreCase);

	public void AnalyzeAndEnrich(ProposedFileChange change)
	{
		if (string.IsNullOrEmpty(change.NewContent)) return;

		var content = change.NewContent;

		// 1. Detecção de Snippet (Omissão de Código)
		// Crítico: Se isso for true, NÃO PODEMOS substituir o arquivo inteiro, temos que fazer Merge.
		if (SnippetMarkerRegex.IsMatch(content))
		{
			change.IsSnippet = true;
			change.ValidationWarnings.Add("Código parcial detectado (// ...). Será realizado um Merge, não uma substituição.");
		}

		// 2. Detecção de Exclusão Explícita
		if (DeleteMarkerRegex.IsMatch(content))
		{
			change.HasDestructiveComments = true;
			change.ValidationWarnings.Add("Contém instruções de exclusão (// DELETAR). Verifique se o código foi removido corretamente.");
		}

		// 3. Detecção de Correção
		if (CorrectionMarkerRegex.IsMatch(content))
		{
			change.Status = "Correção de Bug";
		}
		else if (AddMarkerRegex.IsMatch(content))
		{
			change.Status = "Nova Funcionalidade";
		}

		// 4. Detecção de Incompletude (Alerta ao Usuário)
		if (TodoMarkerRegex.IsMatch(content))
		{
			change.ValidationWarnings.Add("Atenção: A IA deixou marcadores // TODO no código.");
		}
	}
}