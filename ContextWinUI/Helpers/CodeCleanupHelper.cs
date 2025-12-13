using System.Text.RegularExpressions;

namespace ContextWinUI.Helpers;

public static class CodeCleanupHelper
{
	public static string ProcessCode(string content, string fileExtension, bool removeUsings, bool removeComments)
	{
		// 1. Verificação de segurança (Caminho de retorno 1)
		if (string.IsNullOrEmpty(content))
		{
			return string.Empty;
		}

		string processed = content;

		// 2. Remover Comentários
		if (removeComments)
		{
			// Remove comentários de bloco /* ... */
			processed = Regex.Replace(processed, @"/\*[\s\S]*?\*/", "", RegexOptions.Multiline);

			// Remove comentários de linha // ...
			// Regex para linhas inteiras de comentário
			processed = Regex.Replace(processed, @"^\s*//.*$", "", RegexOptions.Multiline);
			// Regex para comentários no final da linha de código
			processed = Regex.Replace(processed, @"//.*$", "", RegexOptions.Multiline);
		}

		// 3. Remover Usings/Imports
		if (removeUsings)
		{
			// C# (using System;)
			processed = Regex.Replace(processed, @"^\s*using\s+[\w\.]+.*;\r?\n", "", RegexOptions.Multiline);

			// TypeScript/JS/Java/Python (import ..., package ...)
			processed = Regex.Replace(processed, @"^\s*import\s+.*;\r?\n", "", RegexOptions.Multiline);
			processed = Regex.Replace(processed, @"^\s*package\s+.*;\r?\n", "", RegexOptions.Multiline);
		}

		// 4. Limpeza de linhas em branco excessivas (resultado das remoções)
		// Substitui 3 ou mais quebras de linha consecutivas por apenas 2
		processed = Regex.Replace(processed, @"(\r?\n){3,}", "\n\n");

		// 5. Retorno Final (Caminho de retorno 2 - OBRIGATÓRIO)
		return processed;
	}
}