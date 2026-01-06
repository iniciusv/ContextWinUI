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

	/// <summary>
	/// Recupera a indentação original da primeira linha do bloco olhando para o arquivo original.
	/// Ajusta o Conteúdo e o AbsoluteStartPosition.
	/// </summary>
	private void RecoverBlockIndentation(CodeBlockItem block, string fullFileContent)
	{
		// Validações de segurança
		if (string.IsNullOrEmpty(block.Content) ||
			char.IsWhiteSpace(block.Content[0]) ||
			block.AbsoluteStartPosition <= 0 ||
			block.AbsoluteStartPosition > fullFileContent.Length)
		{
			return;
		}

		int currentPos = block.AbsoluteStartPosition - 1;
		int whitespaceCount = 0;

		// "Walk backwards": Varre para trás a partir do início do bloco até achar o começo da linha
		while (currentPos >= 0)
		{
			char c = fullFileContent[currentPos];

			// Se achou quebra de linha, paramos (chegamos no início da linha)
			if (c == '\n' || c == '\r') break;

			// Se achou algo que não é espaço (ex: código na mesma linha), 
			// aborta a correção pois não é apenas indentação.
			if (!char.IsWhiteSpace(c))
			{
				return;
			}

			whitespaceCount++;
			currentPos--;
		}

		// Se detectou indentação perdida, aplica a correção
		if (whitespaceCount > 0)
		{
			// A. Reconstrói a string de indentação baseada no original
			string indentation = fullFileContent.Substring(block.AbsoluteStartPosition - whitespaceCount, whitespaceCount);

			// B. Anexa a indentação ao conteúdo
			string newContent = indentation + block.Content;
			block.Content = newContent;

			// C. CRÍTICO: Recua o AbsoluteStartPosition.
			// Se adicionamos 'N' espaços no início, o ponto de partida visual do bloco
			// deve recuar 'N' caracteres para alinhar com o índice absoluto do arquivo.
			block.AbsoluteStartPosition -= whitespaceCount;

			// D. Atualiza o sistema de versionamento do bloco
			UpdateBlockVersions(block, newContent);
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
			// Inicializa o histórico se estiver vazio
			block.InitializeVersions(newContent);
		}
	}
}