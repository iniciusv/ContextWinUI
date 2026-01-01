using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Helpers; // Assumindo onde está CodeCleanupHelper
using ContextWinUI.Core.Models;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.CodeAnalyses;

public class DependencyAnalysisOrchestrator : IDependencyAnalysisOrchestrator
{
	private readonly ISemanticIndexService _indexService;
	private readonly DependencyTrackerService _trackerService;
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly IFileSystemService _fileSystemService;

	public DependencyAnalysisOrchestrator(
		ISemanticIndexService indexService,
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
	/// Preenche um nó de ARQUIVO com seus métodos e classes encontrados no Grafo.
	/// </summary>
	public async Task EnrichFileNodeAsync(FileSystemItem item, string projectPath)
	{
		// Se já tiver filhos, não faz nada (evita reprocessamento)
		if (item.Children.Any()) return;

		var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()
						 ?? App.MainWindow.DispatcherQueue;

		// ---------------------------------------------------------
		// ETAPA 1: FAST TRACK (Análise Sintática Pura - Visualização Rápida)
		// ---------------------------------------------------------
		var tempChildren = new List<FileSystemItem>();
		try
		{
			string fileContent = await _fileSystemService.ReadFileContentAsync(item.FullPath);
			var tree = CSharpSyntaxTree.ParseText(fileContent);
			var root = await tree.GetRootAsync();

			var methodsSyntax = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
			var classesSyntax = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

			if (methodsSyntax.Any())
			{
				var methodsGroup = _itemFactory.CreateWrapper($"{item.FullPath}::methods_temp", FileSystemItemType.LogicalGroup, "\uEA86");
				methodsGroup.SharedState.Name = "Métodos (Carregando...)";

				foreach (var m in methodsSyntax)
				{
					var methodName = m.Identifier.Text;
					var tempItem = _itemFactory.CreateWrapper($"{item.FullPath}::temp::{methodName}", FileSystemItemType.Method, "\uF158");
					tempItem.SharedState.Name = methodName;
					methodsGroup.Children.Add(tempItem);
				}
				tempChildren.Add(methodsGroup);
			}

			if (classesSyntax.Any())
			{
				var typesGroup = _itemFactory.CreateWrapper($"{item.FullPath}::types_temp", FileSystemItemType.LogicalGroup, "\uE943");
				typesGroup.SharedState.Name = "Tipos (Carregando...)";

				foreach (var c in classesSyntax)
				{
					var className = c.Identifier.Text;
					var tempItem = _itemFactory.CreateWrapper($"{item.FullPath}::temp::{className}", FileSystemItemType.Class, "\uE943");
					tempItem.SharedState.Name = className;
					typesGroup.Children.Add(tempItem);
				}
				tempChildren.Add(typesGroup);
			}

			dispatcher.TryEnqueue(() =>
			{
				// Verifica race condition
				if (!item.Children.Any())
				{
					foreach (var child in tempChildren) item.Children.Add(child);
					if (tempChildren.Any()) item.IsExpanded = true;
					item.NotifyViewUpdate(nameof(item.DeepAnalyzeVisibility));
					item.NotifyViewUpdate(nameof(item.MethodFlowVisibility));
				}
			});
		}
		catch
		{
			// Ignora falhas no Fast Track e segue para análise profunda
		}

		// ---------------------------------------------------------
		// ETAPA 2: ANÁLISE PROFUNDA (Grafo Semântico Otimizado)
		// ---------------------------------------------------------

		var graph = await _indexService.GetOrIndexProjectAsync(projectPath);

		// CORREÇÃO: Converter Path String -> FileId Int
		int fileId = graph.GetOrAddFileId(Path.GetFullPath(item.FullPath));

		// Busca nós usando ID Inteiro no FileIndex
		if (!graph.FileIndex.TryGetValue(fileId, out var fileSymbols))
		{
			// Se não houver símbolos, limpa os temporários
			dispatcher.TryEnqueue(() => item.Children.Clear());
			return;
		}

		var methodNodes = new List<SymbolNode>();
		var typeNodes = new List<SymbolNode>();

		// Cópia thread-safe da lista
		lock (fileSymbols)
		{
			foreach (var s in fileSymbols)
			{
				if (s.Type == SymbolType.Method || s.Type == SymbolType.Constructor)
					methodNodes.Add(s);
				else if (s.Type == SymbolType.Class || s.Type == SymbolType.Interface)
					typeNodes.Add(s);
			}
		}

		// Ordenação visual
		methodNodes.Sort((a, b) => a.StartPosition.CompareTo(b.StartPosition));
		typeNodes.Sort((a, b) => a.StartPosition.CompareTo(b.StartPosition));

		var finalChildren = new List<FileSystemItem>();

		// Caminho textual para IDs visuais da TreeView
		string filePathStr = item.FullPath;

		// 2a. Construção dos Métodos
		if (methodNodes.Count > 0)
		{
			var methodsGroup = _itemFactory.CreateWrapper($"{filePathStr}::methods", FileSystemItemType.LogicalGroup, "\uEA86");
			methodsGroup.SharedState.Name = "Métodos";

			foreach (var node in methodNodes)
			{
				// CORREÇÃO: node.Id é int. Usamos ToString() para a MethodSignature (que a UI espera ser string)
				var methodItem = _itemFactory.CreateWrapper($"{filePathStr}::{node.Id}", FileSystemItemType.Method, "\uF158");
				methodItem.SharedState.Name = node.Name;
				methodItem.MethodSignature = node.Id.ToString();
				methodsGroup.Children.Add(methodItem);
			}
			finalChildren.Add(methodsGroup);
		}

		// 2b. Construção dos Tipos
		if (typeNodes.Count > 0)
		{
			var typesGroup = _itemFactory.CreateWrapper($"{filePathStr}::types", FileSystemItemType.LogicalGroup, "\uE943");
			typesGroup.SharedState.Name = "Estrutura e Tipos Usados";

			foreach (var node in typeNodes)
			{
				var typeItem = _itemFactory.CreateWrapper($"{filePathStr}::{node.Id}", FileSystemItemType.Class, "\uE943");
				typeItem.SharedState.Name = node.Name + " (Definição)";
				typeItem.MethodSignature = node.Id.ToString();
				typesGroup.Children.Add(typeItem);

				// Dependências do Tipo (Lookup no Grafo)
				if (graph.Nodes.TryGetValue(node.Id, out var classNodeInGraph))
				{
					// Agrupa dependências únicas
					var dependencies = classNodeInGraph.OutgoingLinks
						.Where(l => l.Type == LinkType.UsesType || l.Type == LinkType.Inherits || l.Type == LinkType.Implements)
						.GroupBy(l => l.TargetId)
						.Select(g => g.First());

					foreach (var link in dependencies)
					{
						if (graph.Nodes.TryGetValue(link.TargetId, out var targetNode))
						{
							// CORREÇÃO: Recuperar o caminho do arquivo do nó destino via ID
							string targetPath = graph.GetFilePath(targetNode.FileId);

							var depItem = _itemFactory.CreateWrapper($"{targetPath}::{targetNode.Id}", FileSystemItemType.Dependency, "\uE972");
							depItem.SharedState.Name = targetNode.Name;
							depItem.MethodSignature = targetNode.Id.ToString();
							typesGroup.Children.Add(depItem);
						}
					}
				}
			}
			finalChildren.Add(typesGroup);
		}

		// ---------------------------------------------------------
		// ETAPA 3: SWAP (Atualização da UI)
		// ---------------------------------------------------------
		dispatcher.TryEnqueue(() =>
		{
			item.Children.Clear();
			foreach (var child in finalChildren) item.Children.Add(child);

			if (finalChildren.Any()) item.IsExpanded = true;
		});
	}

	/// <summary>
	/// Preenche um nó de MÉTODO com o fluxo de chamadas (Call Graph).
	/// </summary>
	public async Task EnrichMethodFlowAsync(FileSystemItem item, string projectPath)
	{
		if (item.Children.Any()) return;

		await Task.Yield(); // Libera UI

		var graph = await _indexService.GetOrIndexProjectAsync(projectPath);

		// CORREÇÃO: Parse seguro de String (UI) para Int (Grafo)
		if (string.IsNullOrEmpty(item.MethodSignature) || !int.TryParse(item.MethodSignature, out int methodId))
			return;

		if (!graph.Nodes.TryGetValue(methodId, out var startNode))
			return;

		// Agrupamento por TargetId (int)
		var uniqueLinks = startNode.OutgoingLinks
			.GroupBy(link => link.TargetId)
			.Select(group => group.First())
			.ToList();

		if (!uniqueLinks.Any()) return;

		var flowItems = new List<FileSystemItem>();
		var dependencyItems = new List<FileSystemItem>();

		foreach (var link in uniqueLinks)
		{
			if (graph.Nodes.TryGetValue(link.TargetId, out var targetNode))
			{
				if (link.Type == LinkType.Calls || link.Type == LinkType.Accesses)
				{
					string icon = targetNode.Type == SymbolType.Method ? "\uF158" : "\uE946";
					// Usa helper corrigido que acessa o Grafo para resolver caminhos
					var child = CreateItemFromNode(targetNode, FileSystemItemType.Method, icon, graph);
					child.IsChecked = true;
					flowItems.Add(child);
				}
				else if (link.Type == LinkType.UsesType || link.Type == LinkType.Inherits || link.Type == LinkType.Implements)
				{
					var child = CreateItemFromNode(targetNode, FileSystemItemType.Dependency, "\uE972", graph);
					child.IsChecked = true;
					dependencyItems.Add(child);
				}
			}
		}

		Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
		{
			if (flowItems.Count > 0)
			{
				var flowGroup = _itemFactory.CreateWrapper($"{item.FullPath}::flow", FileSystemItemType.LogicalGroup, "\uE80D");
				flowGroup.SharedState.Name = "Chamadas & Acessos";
				foreach (var child in flowItems) flowGroup.Children.Add(child);
				item.Children.Add(flowGroup);
			}

			if (dependencyItems.Count > 0)
			{
				var depsGroup = _itemFactory.CreateWrapper($"{item.FullPath}::deps", FileSystemItemType.LogicalGroup, "\uE71D");
				depsGroup.SharedState.Name = "Tipos Usados";
				foreach (var child in dependencyItems) depsGroup.Children.Add(child);
				item.Children.Add(depsGroup);
			}

			if (item.Children.Any()) item.IsExpanded = true;
		});
	}

	/// <summary>
	/// Constrói o texto final para o LLM usando Slicing baseado em Grafo.
	/// </summary>
	public async Task<string> BuildContextStringAsync(IEnumerable<FileSystemItem> selectedItems, IProjectSessionManager sessionSettings)
	{
		var graph = _indexService.GetCurrentGraph();
		var sb = new StringBuilder();

		if (!string.IsNullOrWhiteSpace(sessionSettings.PrePrompt))
		{
			sb.AppendLine(sessionSettings.PrePrompt);
			sb.AppendLine("\n--- CONTEXTO DO CÓDIGO ---\n");
		}

		var itemsByFile = selectedItems
			.GroupBy(i => GetPhysicalPath(i.FullPath))
			.Where(g => !string.IsNullOrEmpty(g.Key) && File.Exists(g.Key));

		foreach (var group in itemsByFile)
		{
			string filePath = group.Key;
			// IO: Lê o arquivo físico uma única vez
			string fileContent = await _fileSystemService.ReadFileContentAsync(filePath);

			sb.AppendLine($"// ARQUIVO: {Path.GetFileName(filePath)}");

			bool isFullFileSelected = group.Any(i => i.Type == FileSystemItemType.File && i.IsChecked);

			if (isFullFileSelected)
			{
				// Estratégia A: Arquivo Completo
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
				// Estratégia B: Extração Parcial (Slicing)
				// CORREÇÃO: Recuperar IDs inteiros da MethodSignature
				var explicitNodeIds = new HashSet<int>();
				foreach (var i in group)
				{
					if (!string.IsNullOrEmpty(i.MethodSignature) && int.TryParse(i.MethodSignature, out int id))
					{
						explicitNodeIds.Add(id);
					}
				}

				if (explicitNodeIds.Count == 0) continue;

				sb.AppendLine("// (Conteúdo Parcial - Apenas métodos relevantes)");

				// Busca nós no grafo e ordena por posição
				var nodesToExport = explicitNodeIds
					.Select(id => graph.Nodes.TryGetValue(id, out var n) ? n : null)
					.Where(n => n != null)
					.OrderBy(n => n!.StartPosition)
					.ToList();

				foreach (var node in nodesToExport)
				{
					if (node == null) continue;

					// Verifica limites para evitar crash de substring
					if (node.StartPosition >= 0 && node.StartPosition + node.Length <= fileContent.Length)
					{
						string extract = fileContent.Substring(node.StartPosition, node.Length);

						if (sessionSettings.OmitComments)
							extract = CodeCleanupHelper.RemoveComments(extract);

						sb.AppendLine(extract);
						sb.AppendLine();
					}
				}
			}
			sb.AppendLine($"\n{new string('-', 40)}\n");
		}

		return sb.ToString();
	}

	// --- Helpers Privados ---

	private FileSystemItem CreateItemFromNode(SymbolNode node, FileSystemItemType type, string icon, DependencyGraph graph)
	{
		// CORREÇÃO CRÍTICA: Resolver caminho textual a partir do FileId do nó
		string filePath = graph.GetFilePath(node.FileId);

		// Cria chave composta visual: "c:\path\file.cs::12345"
		var item = _itemFactory.CreateWrapper($"{filePath}::{node.Id}", type, icon);

		item.SharedState.Name = node.Name;
		item.MethodSignature = node.Id.ToString(); // Converte int para string para persistir na UI

		return item;
	}

	private string GetPhysicalPath(string fullPath)
	{
		if (string.IsNullOrEmpty(fullPath)) return string.Empty;

		// Remove sufixos como "::methods", "::flow" ou "::12345"
		int separatorIndex = fullPath.IndexOf("::");
		return separatorIndex > 0 ? fullPath.Substring(0, separatorIndex) : fullPath;
	}
}