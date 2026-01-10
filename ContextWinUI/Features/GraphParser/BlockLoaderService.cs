using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.GraphParser;
using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services
{
	public class BlockLoaderService : IBlockLoaderService
	{
		private readonly ICodeBlockParserService _parserService;
		private readonly IGitService _gitService;
		private readonly IProjectSessionManager _sessionManager;
		private readonly IBlockIdentityService _identityService; // <--- Nova dependência

		public BlockLoaderService(
			ICodeBlockParserService parserService,
			IGitService gitService,
			IProjectSessionManager sessionManager,
			IBlockIdentityService identityService) // <--- Injeção aqui
		{
			_parserService = parserService;
			_gitService = gitService;
			_sessionManager = sessionManager;
			_identityService = identityService;
		}

		public async Task<List<CodeBlockItem>> LoadAndProcessFileAsync(string filePath)
		{
			// 1. Carregar do Disco
			string diskContent = string.Empty;
			if (File.Exists(filePath))
			{
				diskContent = await File.ReadAllTextAsync(filePath);
			}

			var currentBlocks = await _parserService.ParseFileAsync(filePath, diskContent);

			// 2. Preparar carregamento do Git
			string? rootPath = _sessionManager.CurrentProjectPath;
			string? gitContent = null;

			if (!string.IsNullOrEmpty(rootPath) && _gitService.IsGitRepository(rootPath))
			{
				gitContent = await _gitService.GetFileContentFromHeadAsync(rootPath, filePath);
			}

			// Otimização: Se não tem git ou conteúdo é idêntico, retorna logo
			if (string.IsNullOrEmpty(gitContent) || gitContent == diskContent)
			{
				return currentBlocks;
			}

			// 3. Carregar Blocos do Git
			var gitBlocks = await _parserService.ParseFileAsync(filePath, gitContent);
			var matchedGitBlockIds = new HashSet<string>();

			// 4. Loop de Correlação (O Coração da Mudança)
			foreach (var diskBlock in currentBlocks)
			{
				// USA O SERVIÇO DEDICADO PARA COMPARAR IDENTIDADE
				// Isso resolve o problema de sobrecargas (Overloads) verificando a Assinatura
				var matchingGitBlock = gitBlocks.FirstOrDefault(gb =>
					_identityService.AreSameEntity(diskBlock, gb));

				if (matchingGitBlock != null)
				{
					matchedGitBlockIds.Add(matchingGitBlock.Id); // Marca ID do git como "encontrado"

					// Se o conteúdo textual mudou, configura o histórico de versões
					if (matchingGitBlock.Content != diskBlock.Content)
					{
						SetupBlockHistory(diskBlock, matchingGitBlock.Content, diskBlock.Content);
					}
					// Nota: Se o conteúdo for igual, o bloco mantém apenas a versão "Original" criada pelo parser
				}
				else
				{
					// Se não achou correspondência no Git, é um bloco recém-criado
					SetupNewBlockHistory(diskBlock);
				}
			}

			// 5. Processar Blocos Deletados (O que estava no Git mas não está mais no Disco)
			var deletedBlocks = gitBlocks
				.Where(gb => !matchedGitBlockIds.Contains(gb.Id) && IsSignificantBlock(gb))
				.ToList();

			foreach (var deletedBlock in deletedBlocks)
			{
				// Cria um "Ghost Block" para mostrar visualmente que algo foi apagado
				var ghostBlock = deletedBlock.Clone();
				ghostBlock.Versions.Clear();

				// Versão 1: O que existia no Git
				ghostBlock.Versions.Add(new CodeBlockVersion
				{
					Content = deletedBlock.Content,
					Description = "Original (Git)",
					IsOriginal = true,
					Timestamp = System.DateTime.MinValue
				});

				// Versão 2: Vazio (Estado Atual)
				ghostBlock.Versions.Add(new CodeBlockVersion
				{
					Content = string.Empty,
					Description = "Deletado",
					IsOriginal = false,
					Timestamp = System.DateTime.Now
				});

				ghostBlock.Content = string.Empty;
				ghostBlock.CurrentVersionIndex = 1;
				ghostBlock.Name += " (Deletado)";

				InsertBlockSorted(currentBlocks, ghostBlock);
			}

			return currentBlocks;
		}

		private void SetupNewBlockHistory(CodeBlockItem block)
		{
			string currentContent = block.Content;
			block.Versions.Clear();

			// Versão Base: Vazia (não existia)
			block.Versions.Add(new CodeBlockVersion
			{
				Content = string.Empty,
				Description = "Inexistente (Git)",
				IsOriginal = true,
				Timestamp = System.DateTime.MinValue
			});

			// Versão Atual: Conteúdo Novo
			block.Versions.Add(new CodeBlockVersion
			{
				Content = currentContent,
				Description = "Novo (Adicionado)",
				IsOriginal = false,
				Timestamp = System.DateTime.Now
			});

			block.RestoreVersion(1);
		}

		private void SetupBlockHistory(CodeBlockItem block, string gitContent, string diskContent)
		{
			block.Versions.Clear();

			// Versão Base: Git HEAD
			block.Versions.Add(new CodeBlockVersion
			{
				Content = gitContent,
				Description = "HEAD (Git)",
				IsOriginal = true,
				Timestamp = System.DateTime.MinValue
			});

			// Versão Atual: Working Copy
			block.Versions.Add(new CodeBlockVersion
			{
				Content = diskContent,
				Description = "Working Copy",
				IsOriginal = false,
				Timestamp = System.DateTime.Now
			});

			block.RestoreVersion(1);
		}

		private bool IsSignificantBlock(CodeBlockItem block)
		{
			// Define o que vale a pena mostrar como "Deletado". 
			// Geralmente ignoramos Trivia, Gaps ou Usings para não poluir a UI.
			return block.SegmentType == SegmentType.Method ||
				   block.SegmentType == SegmentType.Property ||
				   block.SegmentType == SegmentType.Class ||
				   block.SegmentType == SegmentType.Constructor ||
				   block.SegmentType == SegmentType.Enum ||
				   block.SegmentType == SegmentType.Interface;
		}

		private void InsertBlockSorted(List<CodeBlockItem> list, CodeBlockItem item)
		{
			// Insere o bloco deletado na posição visual correta baseada na posição absoluta original
			int index = list.FindIndex(b => b.AbsoluteStartPosition > item.AbsoluteStartPosition);
			if (index == -1)
				list.Add(item);
			else
				list.Insert(index, item);
		}
	}
}