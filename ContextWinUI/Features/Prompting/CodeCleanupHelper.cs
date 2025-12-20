using System.Text.RegularExpressions;

namespace ContextWinUI.Helpers;

public static class CodeCleanupHelper
{
	public static string ProcessCode(
		string content,
		string fileExtension,
		bool removeUsings,
		bool removeNamespaces,
		bool removeComments,
		bool removeEmptyLines)
	{

		if (string.IsNullOrEmpty(content))
		{
			return string.Empty;
		}

		string processed = content;

		if (removeComments)
		{
			// Blocos de comentário
			processed = Regex.Replace(processed, @"/\*[\s\S]*?\*/", "", RegexOptions.Multiline);

			// Comentários de linha única (iniciando no começo da linha ou após código)
			// Nota: Esta regex simples pode remover URLs em strings, cuidado em prod.
			// Para simplificação atual:
			processed = Regex.Replace(processed, @"//.*$", "", RegexOptions.Multiline);
		}

		if (removeUsings)
		{
			processed = Regex.Replace(processed, @"^\s*using\s+[\w\.]+.*;\r?\n", "", RegexOptions.Multiline);
			processed = Regex.Replace(processed, @"^\s*import\s+.*;\r?\n", "", RegexOptions.Multiline);
			processed = Regex.Replace(processed, @"^\s*package\s+.*;\r?\n", "", RegexOptions.Multiline);
		}

		if (removeNamespaces)
		{
			// Remove namespaces "File Scoped" (C# 10+)
			processed = Regex.Replace(processed, @"^\s*namespace\s+[\w\.]+\s*;\r?\n", "", RegexOptions.Multiline);

			// Remove declaração de namespace com bloco (ex: "namespace ContextWinUI {")
			// Remove apenas a linha de declaração, mantendo o conteúdo indentado.
			processed = Regex.Replace(processed, @"^\s*namespace\s+[\w\.]+(\s*\{)?\r?\n", "", RegexOptions.Multiline);
		}

		if (removeEmptyLines)
		{
			// Remove linhas totalmente vazias ou com espaços
			processed = Regex.Replace(processed, @"^\s*$(\r?\n)+", "", RegexOptions.Multiline);

			// Garante que não haja múltiplos newlines sobrando
			processed = Regex.Replace(processed, @"(\r?\n){2,}", "\n");
		}
		else
		{
			// Comportamento padrão: normaliza para no máximo 2 quebras de linha (parágrafos visuais)
			processed = Regex.Replace(processed, @"(\r?\n){3,}", "\n\n");
		}

		return processed.Trim();
	}
}