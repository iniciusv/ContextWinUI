using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.CodeAnalyses;

public class DependencyAnalysisOrchestrator : IDependencyAnalysisOrchestrator
{
	private readonly SemanticIndexService _indexService;
	private readonly DependencyTrackerService _trackerService;
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly IFileSystemService _fileSystemService;

	public DependencyAnalysisOrchestrator(
		SemanticIndexService indexService,
		DependencyTrackerService trackerService,
		IFileSystemItemFactory itemFactory,
		IFileSystemService fileSystemService)
	{
		_indexService = indexService;
		_trackerService = trackerService;
		_itemFactory = itemFactory;
		_fileSystemService = fileSystemService;
	}


	/// <summary>
	/// Preenche um nó de MÉTODO com suas dependências recursivas.
	/// Operação: Algoritmo de busca em Grafo (BFS/DFS) em memória.
	/// </summary>
	public async Task EnrichFileNodeAsync(FileSystemItem item, string projectPath)
	{
		// Cede controle inicial
		await Task.Yield();

		var graph = await _indexService.GetOrIndexProjectAsync(projectPath);

		// Normalização da chave de busca (deve bater com a do GraphBuilder)
		var searchKey = Path.GetFullPath(item.FullPath).ToLowerInvariant();

		// [OTIMIZAÇÃO 1] Lookup O(1) Instantâneo
		if (!graph.FileIndex.TryGetValue(searchKey, out var fileSymbols))
		{
			return; // Arquivo não tem símbolos mapeados ou caminho diferente
		}

		// [OTIMIZAÇÃO 2] Preparar dados fora da UI Thread
		// Cria as listas em memória pura antes de criar os wrappers visuais
		var methodNodes = new List<SymbolNode>();
		var typeNodes = new List<SymbolNode>();

		// Iteração rápida em memória
		lock (fileSymbols) // Lista pode estar sendo escrita se indexação for paralela
		{
			foreach (var s in fileSymbols)
			{
				if (s.Type == SymbolType.Method || s.Type == SymbolType.Constructor)
					methodNodes.Add(s);
				else if (s.Type == SymbolType.Class || s.Type == SymbolType.Interface)
					typeNodes.Add(s);
			}
		}

		// Ordenação rápida
		methodNodes.Sort((a, b) => a.StartPosition.CompareTo(b.StartPosition));

		// [OTIMIZAÇÃO 3] Batch Update na UI
		// Usamos Dispatcher apenas uma vez por grupo se possível, ou construímos os itens antes

		// Criar os Wrappers (ainda desconectados da UI)
		var newChildren = new List<FileSystemItem>();

		if (methodNodes.Count > 0)
		{
			var methodsGroup = _itemFactory.CreateWrapper($"{item.FullPath}::methods", FileSystemItemType.LogicalGroup, "\uEA86");
			methodsGroup.SharedState.Name = "Métodos";

			foreach (var node in methodNodes)
			{
				var methodItem = _itemFactory.CreateWrapper($"{node.FilePath}::{node.Id}", FileSystemItemType.Method, "\uF158");
				methodItem.SharedState.Name = node.Name;
				methodItem.MethodSignature = node.Id;
				methodsGroup.Children.Add(methodItem);
			}
			newChildren.Add(methodsGroup);
		}

		if (typeNodes.Count > 0)
		{
			var typesGroup = _itemFactory.CreateWrapper($"{item.FullPath}::types", FileSystemItemType.LogicalGroup, "\uE943");
			typesGroup.SharedState.Name = "Tipos";

			foreach (var node in typeNodes)
			{
				var typeItem = _itemFactory.CreateWrapper($"{node.FilePath}::{node.Id}", FileSystemItemType.Class, "\uE943");
				typeItem.SharedState.Name = node.Name;
				typesGroup.Children.Add(typeItem);
			}
			newChildren.Add(typesGroup);
		}

		// Atualização Visual Atômica
		// Se estivermos rodando via Task.Run (do ViewModel), precisamos do Dispatcher aqui.
		// Se o ViewModel já chamou dentro do Dispatcher, isso roda direto.

		// Dica: Use o DispatcherQueue do MainThread para garantir
		Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
		{
			item.Children.Clear();
			foreach (var child in newChildren)
			{
				item.Children.Add(child);
			}
			if (newChildren.Any()) item.IsExpanded = true;
		});
	}
	/// <summary>
	/// Preenche um nó de MÉTODO com suas dependências recursivas.
	/// Operação: Algoritmo de busca em Grafo (BFS/DFS) em memória.
	/// </summary>
	public async Task EnrichMethodFlowAsync(FileSystemItem item, string projectPath)
	{
		// [OTIMIZAÇÃO 1] Cache Visual
		// Se já expandiu e tem filhos, não faz nada. 
		// Isso evita reprocessar se o usuário fecha e abre o nó.
		if (item.Children.Any()) return;

		await Task.Yield(); // Libera UI thread

		var graph = await _indexService.GetOrIndexProjectAsync(projectPath);
		var methodId = item.MethodSignature;

		// Lookup O(1)
		if (string.IsNullOrEmpty(methodId) || !graph.Nodes.TryGetValue(methodId, out var startNode))
			return;

		// [OTIMIZAÇÃO 2] Acesso Direto (Sem Recursão)
		// Em vez de chamar _trackerService.GetDeepDependencies (que percorre o mundo todo),
		// olhamos apenas para os links que saem DESTE nó.
		var directLinks = startNode.OutgoingLinks;

		if (!directLinks.Any()) return;

		// Prepara listas em memória (fora da UI Thread)
		var flowItems = new List<FileSystemItem>();
		var dependencyItems = new List<FileSystemItem>();

		foreach (var link in directLinks)
		{
			// Resolve o nó alvo (O(1))
			if (graph.Nodes.TryGetValue(link.TargetId, out var targetNode))
			{
				if (link.Type == LinkType.Calls || link.Type == LinkType.Accesses)
				{
					string icon = targetNode.Type == SymbolType.Method ? "\uF158" : "\uE946";
					var child = CreateItemFromNode(targetNode, FileSystemItemType.Method, icon);

					// Marca dependências diretas automaticamente para facilitar a vida do usuário
					child.IsChecked = true;
					flowItems.Add(child);
				}
				else if (link.Type == LinkType.UsesType)
				{
					var child = CreateItemFromNode(targetNode, FileSystemItemType.Dependency, "\uE972");
					child.IsChecked = true;
					dependencyItems.Add(child);
				}
			}
		}

		// [OTIMIZAÇÃO 3] Batch Update na UI
		// Só voltamos para a Thread principal para desenhar
		Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
		{
			// Grupo Lógico: Fluxo
			if (flowItems.Count > 0)
			{
				var flowGroup = _itemFactory.CreateWrapper($"{item.FullPath}::flow", FileSystemItemType.LogicalGroup, "\uE80D");
				flowGroup.SharedState.Name = "Chamadas & Acessos";
				foreach (var child in flowItems) flowGroup.Children.Add(child);
				item.Children.Add(flowGroup);
			}

			// Grupo Lógico: Tipos
			if (dependencyItems.Count > 0)
			{
				var depsGroup = _itemFactory.CreateWrapper($"{item.FullPath}::deps", FileSystemItemType.LogicalGroup, "\uE71D");
				depsGroup.SharedState.Name = "Tipos Usados";
				foreach (var child in dependencyItems) depsGroup.Children.Add(child);
				item.Children.Add(depsGroup);
			}

			// Expande o item se houve algo adicionado
			if (item.Children.Any()) item.IsExpanded = true;
		});
	}

	/// <summary>
	/// Constrói o texto final para o LLM.
	/// Operação: Lê arquivos do disco (IO) mas usa coordenadas do Grafo para cortar o texto (Slicing) em vez de Parsing.
	/// </summary>
	public async Task<string> BuildContextStringAsync(IEnumerable<FileSystemItem> selectedItems, IProjectSessionManager sessionSettings)
	{
		var graph = _indexService.GetCurrentGraph();
		var sb = new StringBuilder();

		// 1. Adiciona Pre-Prompt
		if (!string.IsNullOrWhiteSpace(sessionSettings.PrePrompt))
		{
			sb.AppendLine(sessionSettings.PrePrompt);
			sb.AppendLine();
			sb.AppendLine("--- CONTEXTO DO CÓDIGO ---");
			sb.AppendLine();
		}

		// 2. Agrupa itens por arquivo físico para minimizar IO
		var itemsByFile = selectedItems
			.GroupBy(i => GetPhysicalPath(i.FullPath))
			.Where(g => !string.IsNullOrEmpty(g.Key) && File.Exists(g.Key));

		foreach (var group in itemsByFile)
		{
			string filePath = group.Key;
			string fileContent = await _fileSystemService.ReadFileContentAsync(filePath); // Lê arquivo inteiro uma vez

			sb.AppendLine($"// ARQUIVO: {Path.GetFileName(filePath)}");

			// Verifica se selecionou o arquivo inteiro ou partes dele
			bool isFullFileSelected = group.Any(i => i.Type == FileSystemItemType.File && i.IsChecked);

			if (isFullFileSelected)
			{
				// Estratégia A: Arquivo Completo (aplica limpezas regex básicas se configurado)
				string processedContent = CodeCleanupHelper.ProcessCode(
					fileContent,
					Path.GetExtension(filePath),
					sessionSettings.OmitUsings,
					sessionSettings.OmitNamespaces,
					sessionSettings.OmitComments,
					sessionSettings.OmitEmptyLines);

				sb.AppendLine(processedContent);
			}
			else
			{
				// Estratégia B: Extração Cirúrgica baseada no Grafo
				var explicitNodeIds = group
					.Where(i => !string.IsNullOrEmpty(i.MethodSignature))
					.Select(i => i.MethodSignature)
					.ToHashSet();

				if (explicitNodeIds.Count == 0) continue;

				// Expandir para dependências se necessário (opcional: aqui estamos exportando apenas o que foi marcado visualmente)
				// Se quiser exportar a árvore recursiva automaticamente, chame _trackerService.GetDeepDependencies aqui.

				sb.AppendLine("// (Conteúdo Parcial - Apenas métodos relevantes)");

				// Ordenar por posição no arquivo para o texto sair na ordem correta
				var nodesToExport = explicitNodeIds
					.Select(id => graph.Nodes.TryGetValue(id, out var n) ? n : null)
					.Where(n => n != null)
					.OrderBy(n => n!.StartPosition)
					.ToList();

				foreach (var node in nodesToExport)
				{
					if (node == null) continue;

					// Extração ultra-rápida via Substring usando coordenadas do grafo
					if (node.StartPosition + node.Length <= fileContent.Length)
					{
						string extract = fileContent.Substring(node.StartPosition, node.Length);

						// Opcional: Aplicar limpeza básica no trecho extraído
						if (sessionSettings.OmitComments)
							extract = CodeCleanupHelper.RemoveComments(extract);

						sb.AppendLine(extract);
						sb.AppendLine();
					}
				}
			}
			sb.AppendLine();
			sb.AppendLine(new string('-', 40));
			sb.AppendLine();
		}

		return sb.ToString();
	}

	// --- Helpers Privados ---

	private FileSystemItem CreateItemFromNode(SymbolNode node, FileSystemItemType type, string icon)
	{
		// O ID único do grafo vai no MethodSignature para rastreio futuro
		var item = _itemFactory.CreateWrapper($"{node.FilePath}::{node.Id}", type, icon);
		item.SharedState.Name = node.Name; // Nome amigável (ex: "CalcularImposto")
		item.MethodSignature = node.Id;    // ID Real (ex: "Namespace.Class.CalcularImposto(decimal)")
		return item;
	}

	private string GetPhysicalPath(string fullPath)
	{
		if (string.IsNullOrEmpty(fullPath)) return string.Empty;
		int separatorIndex = fullPath.IndexOf("::");
		return separatorIndex > 0 ? fullPath.Substring(0, separatorIndex) : fullPath;
	}

}