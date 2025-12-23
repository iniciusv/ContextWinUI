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
	/// Preenche um nó de ARQUIVO com seus métodos e classes contidos.
	/// Operação: Lookup O(N) no Grafo (onde N é total de símbolos do projeto, filtrado por path).
	/// </summary>
	public async Task EnrichFileNodeAsync(FileSystemItem item, string projectPath)
	{
		// 1. Garante que o grafo existe
		var graph = await _indexService.GetOrIndexProjectAsync(projectPath);
		item.Children.Clear();

		// 2. NORMALIZAÇÃO DE CAMINHO (O Segredo da Relação)
		// Converte tudo para minúsculo e caminho absoluto para garantir o Match
		var targetPath = Path.GetFullPath(item.FullPath).ToLowerInvariant();

		// 3. Busca símbolos usando a chave normalizada
		var fileSymbols = graph.Nodes.Values
			.Where(n => Path.GetFullPath(n.FilePath).ToLowerInvariant() == targetPath)
			.OrderBy(n => n.StartPosition)
			.ToList();

		// Se não achou nada, pode ser que o arquivo não seja C# ou foi ignorado
		if (!fileSymbols.Any()) return;

		// --- Criação dos Grupos Visuais ---

		// Métodos
		var methods = fileSymbols.Where(s => s.Type == SymbolType.Method || s.Type == SymbolType.Constructor).ToList();
		if (methods.Any())
		{
			var methodsGroup = _itemFactory.CreateWrapper($"{item.FullPath}::methods", FileSystemItemType.LogicalGroup, "\uEA86");
			methodsGroup.SharedState.Name = "Métodos";

			foreach (var node in methods)
			{
				var methodItem = _itemFactory.CreateWrapper($"{node.FilePath}::{node.Id}", FileSystemItemType.Method, "\uF158");
				methodItem.SharedState.Name = node.Name;
				methodItem.MethodSignature = node.Id; // IMPORTANTE: Guarda o ID do grafo para usar depois
				methodsGroup.Children.Add(methodItem);
			}
			item.Children.Add(methodsGroup);

			// Expande o arquivo automaticamente se encontrou métodos
			item.IsExpanded = true;
		}
	}

	/// <summary>
	/// Preenche um nó de MÉTODO com suas dependências recursivas.
	/// Operação: Algoritmo de busca em Grafo (BFS/DFS) em memória.
	/// </summary>
	public async Task EnrichMethodFlowAsync(FileSystemItem item, string projectPath)
	{
		var graph = await _indexService.GetOrIndexProjectAsync(projectPath);
		var startNodeId = item.MethodSignature; // Isso deve conter o ID do Roslyn agora

		if (string.IsNullOrEmpty(startNodeId) || !graph.Nodes.TryGetValue(startNodeId, out var startNode))
			return;

		item.Children.Clear();

		// 1. Obter dependências diretas para categorização visual imediata
		var directLinks = startNode.OutgoingLinks;

		// Grupo: Chamadas de Fluxo (Lógica)
		var logicLinks = directLinks.Where(l => l.Type == LinkType.Calls || l.Type == LinkType.Accesses).ToList();
		if (logicLinks.Any())
		{
			var flowGroup = _itemFactory.CreateWrapper($"{item.FullPath}::flow", FileSystemItemType.LogicalGroup, "\uE80D");
			flowGroup.SharedState.Name = "Lógica (Chamadas & Acessos)";

			foreach (var link in logicLinks)
			{
				if (graph.Nodes.TryGetValue(link.TargetId, out var targetNode))
				{
					// Define ícone baseado no tipo (Método vs Propriedade)
					string icon = targetNode.Type == SymbolType.Method ? "\uF158" : "\uE946";
					var child = CreateItemFromNode(targetNode, FileSystemItemType.Method, icon);

					// IMPORTANTE: Marca como "Checked" para vir selecionado por padrão na exportação
					child.IsChecked = true;

					flowGroup.Children.Add(child);
				}
			}
			item.Children.Add(flowGroup);
		}

		// Grupo: Dependências de Tipo (Complex Types / Entities)
		var typeLinks = directLinks.Where(l => l.Type == LinkType.UsesType).ToList();
		if (typeLinks.Any())
		{
			var depsGroup = _itemFactory.CreateWrapper($"{item.FullPath}::deps", FileSystemItemType.LogicalGroup, "\uE71D");
			depsGroup.SharedState.Name = "Usa Tipos (Classes/Entities)";

			foreach (var link in typeLinks)
			{
				if (graph.Nodes.TryGetValue(link.TargetId, out var targetNode))
				{
					var child = CreateItemFromNode(targetNode, FileSystemItemType.Dependency, "\uE972");
					child.IsChecked = true;
					depsGroup.Children.Add(child);
				}
			}
			item.Children.Add(depsGroup);
		}
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