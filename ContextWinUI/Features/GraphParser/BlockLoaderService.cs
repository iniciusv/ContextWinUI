using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.GraphParser;
using ContextWinUI.Features.GraphParser.Models;
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

		public BlockLoaderService(
			ICodeBlockParserService parserService,
			IGitService gitService,
			IProjectSessionManager sessionManager)
		{
			_parserService = parserService;
			_gitService = gitService;
			_sessionManager = sessionManager;
		}

		public async Task<List<CodeBlockItem>> LoadAndProcessFileAsync(string filePath)
		{
			// 1. Carrega e parseia o conteúdo ATUAL do DISCO
			string diskContent = string.Empty;
			if (File.Exists(filePath))
			{
				diskContent = await File.ReadAllTextAsync(filePath);
			}
			// Lista principal de blocos (versão atual)
			var currentBlocks = await _parserService.ParseFileAsync(filePath, diskContent);

			// 2. Verifica Git e tenta pegar a versão HEAD
			string? rootPath = _sessionManager.CurrentProjectPath;
			string? gitContent = null;

			if (!string.IsNullOrEmpty(rootPath) && _gitService.IsGitRepository(rootPath))
			{
				gitContent = await _gitService.GetFileContentFromHeadAsync(rootPath, filePath);
			}

			// Se não tem conteúdo no Git ou é igual, retorna o que temos
			if (string.IsNullOrEmpty(gitContent) || gitContent == diskContent)
			{
				return currentBlocks;
			}

			// 3. Parseia a versão do Git (Snapshot antigo)
			var gitBlocks = await _parserService.ParseFileAsync(filePath, gitContent);

			// 4. MERGE: Processar Modificados e Deletados

			// Lista para rastrear quais blocos do Git foram encontrados no disco
			var matchedGitBlockIds = new HashSet<string>();

			// A) Percorre os blocos do DISCO para encontrar correspondências no Git
			foreach (var diskBlock in currentBlocks)
			{
				// Tenta achar o bloco correspondente no Git pela assinatura (Nome + Tipo)
				// Usamos FirstOrDefault pois a posição pode ter mudado
				var matchingGitBlock = gitBlocks.FirstOrDefault(gb =>
					gb.Name == diskBlock.Name &&
					gb.TypeDescription == diskBlock.TypeDescription &&
					// Opcional: checar DepthLevel para garantir que não é um método com mesmo nome em classe interna diferente
					gb.DepthLevel == diskBlock.DepthLevel
				);

				if (matchingGitBlock != null)
				{
					matchedGitBlockIds.Add(matchingGitBlock.Id); // Marca como encontrado (mesmo que Id seja novo, usamos a ref do obj)

					// Se o conteúdo é diferente, configuramos o histórico
					if (matchingGitBlock.Content != diskBlock.Content)
					{
						SetupBlockHistory(diskBlock, matchingGitBlock.Content, diskBlock.Content);
					}
				}
				else
				{
					SetupNewBlockHistory(diskBlock);
				}
			}



			// B) Identificar Blocos DELETADOS (Existem no Git, mas não foram achados no loop acima)
			// Filtramos apenas blocos "relevantes" (Métodos, Classes, Propriedades) para evitar ruído com Trivia/Espaços
			var deletedBlocks = gitBlocks
				.Where(gb => !matchedGitBlockIds.Contains(gb.Id) && IsSignificantBlock(gb))
				.ToList();

			foreach (var deletedBlock in deletedBlocks)
			{
				// Precisamos "ressuscitar" este bloco para mostrar na lista
				// Ele terá:
				// Versão 0 (Original): Conteúdo do Git
				// Versão 1 (Atual): VAZIO ou NULL (representando a deleção)

				// Clona o bloco do Git para não afetar referências
				var ghostBlock = deletedBlock.Clone();

				// Configura o histórico de deleção
				ghostBlock.Versions.Clear();

				// Versão Git (O que existia)
				ghostBlock.Versions.Add(new CodeBlockVersion
				{
					Content = deletedBlock.Content,
					Description = "Original (Git)",
					IsOriginal = true,
					Timestamp = System.DateTime.MinValue
				});

				// Versão Disco (Deletado)
				ghostBlock.Versions.Add(new CodeBlockVersion
				{
					Content = string.Empty, // Conteúdo vazio representa deleção
					Description = "Deletado",
					IsOriginal = false,
					Timestamp = System.DateTime.Now
				});

				// Define o estado atual como deletado
				ghostBlock.Content = string.Empty;
				ghostBlock.CurrentVersionIndex = 1;
				ghostBlock.Name += " (Deletado)"; // Opcional: Marcador visual no nome

				// Adiciona à lista final. 
				// DESAFIO: Onde inserir? Como não sabemos a posição exata nova, 
				// tentamos colocar próximo de onde estava (baseado no StartPosition original do Git),
				// ou apenas no final se for muito complexo calcular.
				InsertBlockSorted(currentBlocks, ghostBlock);
			}

			return currentBlocks;
		}



		private void SetupNewBlockHistory(CodeBlockItem block)
		{
			// Salva o conteúdo atual (que é o novo método)
			string currentContent = block.Content;

			block.Versions.Clear();

			// 1. Versão Original (Git) -> VAZIA
			// Isso garante que a coluna da esquerda mostre um espaço vazio/gap
			block.Versions.Add(new CodeBlockVersion
			{
				Content = string.Empty,
				Description = "Inexistente (Git)",
				IsOriginal = true, // O DiffManager vai pegar este aqui como referência
				Timestamp = System.DateTime.MinValue
			});

			// 2. Versão Atual (Disco) -> CONTEÚDO NOVO
			block.Versions.Add(new CodeBlockVersion
			{
				Content = currentContent,
				Description = "Novo (Adicionado)",
				IsOriginal = false,
				Timestamp = System.DateTime.Now
			});

			// Aponta para a versão atual e notifica a UI
			block.RestoreVersion(1);

			// Opcional: Adicionar um indicador visual no nome
			// block.Name += " (Novo)"; 
		}
		private void SetupBlockHistory(CodeBlockItem block, string gitContent, string diskContent)
		{
			// IMPORTANTE: Limpar as versões iniciais criadas pelo Parser
			block.Versions.Clear();

			// 1. Versão Original (Git - HEAD)
			block.Versions.Add(new CodeBlockVersion
			{
				Content = gitContent,
				Description = "HEAD (Git)",
				IsOriginal = true, // Isso permite que o DiffManager ache essa versão
				Timestamp = System.DateTime.MinValue
			});

			// 2. Versão Atual (Disco - Working Copy)
			block.Versions.Add(new CodeBlockVersion
			{
				Content = diskContent,
				Description = "Working Copy",
				IsOriginal = false,
				Timestamp = System.DateTime.Now
			});

			// Aponta para a versão do disco para edição
			block.RestoreVersion(1);

		}

		private bool IsSignificantBlock(CodeBlockItem block)
		{
			// Ignora Trivia, Gaps, Usings e Namespaces soltos na detecção de "Deletados"
			// para não poluir a visualização com chaves de fechamento perdidas.
			return block.SegmentType == SegmentType.Method ||
				   block.SegmentType == SegmentType.Property ||
				   block.SegmentType == SegmentType.Class ||
				   block.SegmentType == SegmentType.Constructor ||
				   block.SegmentType == SegmentType.Enum ||
				   block.SegmentType == SegmentType.Interface;
		}

		private void InsertBlockSorted(List<CodeBlockItem> list, CodeBlockItem item)
		{
			// Tenta inserir baseado na posição original absoluta para manter a ordem de leitura
			int index = list.FindIndex(b => b.AbsoluteStartPosition > item.AbsoluteStartPosition);
			if (index == -1)
				list.Add(item);
			else
				list.Insert(index, item);
		}
	}
}