using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.GraphParser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser;

public class BlockLoaderService : IBlockLoaderService
{
	private readonly IFileSystemService _fileSystemService;
	private readonly ICodeBlockParserService _parserService;

	public BlockLoaderService(
		IFileSystemService fileSystemService,
		ICodeBlockParserService parserService)
	{
		_fileSystemService = fileSystemService;
		_parserService = parserService;
	}

	public async Task<List<CodeBlockItem>> LoadAndProcessFileAsync(string filePath)
	{
		// 1. Leitura bruta do arquivo
		var fileContent = await _fileSystemService.ReadFileContentAsync(filePath);

		if (string.IsNullOrEmpty(fileContent))
			return new List<CodeBlockItem>();

		// 2. Parsing (identificação dos blocos lógicos)
		// Nota: O parser geralmente remove a indentação inicial da primeira linha.
		var parsedItems = await _parserService.ParseFileAsync(filePath, fileContent);

		// 3. Pós-processamento: Correção de Indentação e Offsets
		foreach (var item in parsedItems)
		{
			RecoverBlockIndentation(item, fileContent);
		}

		return parsedItems;
	}

	private void RecoverBlockIndentation(CodeBlockItem block, string fullFileContent)
	{
		if (string.IsNullOrEmpty(block.Content) ||
			char.IsWhiteSpace(block.Content[0]) ||
			block.AbsoluteStartPosition <= 0 ||
			block.AbsoluteStartPosition > fullFileContent.Length)
		{
			return;
		}

		int currentPos = block.AbsoluteStartPosition - 1;
		int whitespaceCount = 0;

		while (currentPos >= 0)
		{
			char c = fullFileContent[currentPos];
			if (c == '\n' || c == '\r') break;
			if (!char.IsWhiteSpace(c))
			{
				return;
			}
			whitespaceCount++;
			currentPos--;
		}

		if (whitespaceCount > 0)
		{
			string indentation = fullFileContent.Substring(block.AbsoluteStartPosition - whitespaceCount, whitespaceCount);
			string newContent = indentation + block.Content;

			UpdateBlockVersions(block, newContent);

			block.Content = newContent;
			block.AbsoluteStartPosition -= whitespaceCount;
		}
	}

	private void UpdateBlockVersions(CodeBlockItem block, string newContent)
	{
		var originalVer = block.Versions.FirstOrDefault(v => v.IsOriginal);

		if (originalVer != null)
		{
			// Atualiza a versão original para não parecer que houve edição
			originalVer.Content = newContent;
		}
		else
		{
			block.InitializeVersions(newContent);
		}
	}
}