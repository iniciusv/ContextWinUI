using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.UI.Dispatching;
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
		// Se já tiver filhos, não faz nada (evita reprocessamento desnecessário)
		if (item.Children.Any()) return;

		var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()	 ?? App.MainWindow.DispatcherQueue;


		// ---------------------------------------------------------
		// ETAPA 1: FAST TRACK (Análise Sintática Pura)
		// Objetivo: Dar feedback visual imediato ao usuário enquanto a compilação roda.
		// ---------------------------------------------------------
		string fileContent = string.Empty;
		try
		{
			// Lê o conteúdo do disco rapidamente
			fileContent = await _fileSystemService.ReadFileContentAsync(item.FullPath);

			// Parse rápido (sem Semantic Model, apenas texto -> árvore)
			var tree = CSharpSyntaxTree.ParseText(fileContent);
			var root = await tree.GetRootAsync();

			var methodsSyntax = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
			var classesSyntax = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();

			var tempChildren = new List<FileSystemItem>();

			// Criação visual dos métodos (sem ID de linkagem ainda)
			if (methodsSyntax.Any())
			{
				var methodsGroup = _itemFactory.CreateWrapper($"{item.FullPath}::methods_temp", FileSystemItemType.LogicalGroup, "\uEA86");
				methodsGroup.SharedState.Name = "Métodos (Carregando...)"; // Feedback visual

				foreach (var m in methodsSyntax)
				{
					var methodName = m.Identifier.Text;
					// Usamos um ID temporário. Nota: O ícone pode ser cinza ou diferente se quiser indicar "loading"
					var tempItem = _itemFactory.CreateWrapper($"{item.FullPath}::temp::{methodName}", FileSystemItemType.Method, "\uF158");
					tempItem.SharedState.Name = methodName;
					methodsGroup.Children.Add(tempItem);
				}
				tempChildren.Add(methodsGroup);
			}

			// Criação visual das classes/tipos
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

			// Atualiza a UI imediatamente com os dados preliminares
			dispatcher.TryEnqueue(() =>
			{
				// Verifica se no meio tempo a análise completa não terminou (race condition rara)
				if (!item.Children.Any())
				{
					foreach (var child in tempChildren) item.Children.Add(child);
					if (tempChildren.Any()) item.IsExpanded = true;

					// IMPORTANTE: Notificar que as propriedades de visibilidade mudaram
					item.NotifyViewUpdate(nameof(item.DeepAnalyzeVisibility));
					item.NotifyViewUpdate(nameof(item.MethodFlowVisibility));
				}
			});
		}
		catch (Exception)
		{
			// Se falhar o parse rápido, ignoramos e deixamos o fluxo seguir para a análise completa
		}

		// ---------------------------------------------------------
		// ETAPA 2: ANÁLISE PROFUNDA (Semântica)
		// Objetivo: Obter os IDs corretos do Roslyn para permitir navegação de dependência.
		// ---------------------------------------------------------

		// Isso pode demorar se for a primeira execução (compilação do projeto)
		var graph = await _indexService.GetOrIndexProjectAsync(projectPath);

		var searchKey = Path.GetFullPath(item.FullPath).ToLowerInvariant();
		if (!graph.FileIndex.TryGetValue(searchKey, out var fileSymbols))
		{
			// Se não encontrou símbolos no grafo, removemos os itens temporários de "Loading" e encerramos
			dispatcher.TryEnqueue(() => item.Children.Clear());
			return;
		}

		var methodNodes = new List<SymbolNode>();
		var typeNodes = new List<SymbolNode>();

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

		// Ordena pela posição no arquivo para ficar igual à leitura do código
		methodNodes.Sort((a, b) => a.StartPosition.CompareTo(b.StartPosition));
		typeNodes.Sort((a, b) => a.StartPosition.CompareTo(b.StartPosition));

		var finalChildren = new List<FileSystemItem>();

		// Recria o grupo de métodos, agora com dados reais e linkáveis
		if (methodNodes.Count > 0)
		{
			var methodsGroup = _itemFactory.CreateWrapper($"{item.FullPath}::methods", FileSystemItemType.LogicalGroup, "\uEA86");
			methodsGroup.SharedState.Name = "Métodos"; // Nome definitivo

			foreach (var node in methodNodes)
			{
				// O ID agora é o node.Id (Assinatura única do Roslyn), permitindo o MethodFlow funcionar
				var methodItem = _itemFactory.CreateWrapper($"{node.FilePath}::{node.Id}", FileSystemItemType.Method, "\uF158");
				methodItem.SharedState.Name = node.Name;
				methodItem.MethodSignature = node.Id;
				methodsGroup.Children.Add(methodItem);
			}
			finalChildren.Add(methodsGroup);
		}

		// Recria o grupo de tipos
		if (typeNodes.Count > 0)
		{
			var typesGroup = _itemFactory.CreateWrapper($"{item.FullPath}::types", FileSystemItemType.LogicalGroup, "\uE943");
			typesGroup.SharedState.Name = "Estrutura e Tipos Usados";

			foreach (var node in typeNodes)
			{
				// 1. Adiciona a própria classe (a declaração)
				var typeItem = _itemFactory.CreateWrapper($"{node.FilePath}::{node.Id}", FileSystemItemType.Class, "\uE943");
				typeItem.SharedState.Name = node.Name + " (Definição)";
				typeItem.MethodSignature = node.Id;
				typesGroup.Children.Add(typeItem);

				// 2. Busca as dependências desta classe no Grafo
				if (graph.Nodes.TryGetValue(node.Id, out var classNodeInGraph))
				{
					// Filtra links de tipos usados, herança ou interfaces
					var dependencies = classNodeInGraph.OutgoingLinks
						.Where(l => l.Type == LinkType.UsesType || l.Type == LinkType.Inherits || l.Type == LinkType.Implements)
						.GroupBy(l => l.TargetId) // Evita duplicados
						.Select(g => g.First());

					foreach (var link in dependencies)
					{
						if (graph.Nodes.TryGetValue(link.TargetId, out var targetNode))
						{
							// Adiciona o tipo usado como um filho ou item da lista
							var depItem = _itemFactory.CreateWrapper($"{targetNode.FilePath}::{targetNode.Id}", FileSystemItemType.Dependency, "\uE972");
							depItem.SharedState.Name = targetNode.Name; // Ex: "IProjectSessionManager"
							depItem.MethodSignature = targetNode.Id;

							// Você pode adicionar direto no grupo ou dentro do item da classe
							typesGroup.Children.Add(depItem);
						}
					}
				}
			}
			finalChildren.Add(typesGroup);
		}

		// ---------------------------------------------------------
		// ETAPA 3: SWAP (Troca)
		// Objetivo: Substituir os itens temporários pelos itens finais na UI thread.
		// ---------------------------------------------------------
		dispatcher.TryEnqueue(() =>
		{
			// Limpa os itens de "Carregando..."
			item.Children.Clear();

			// Adiciona os itens finais enriquecidos
			foreach (var child in finalChildren)
			{
				item.Children.Add(child);
			}

			if (finalChildren.Any()) item.IsExpanded = true;
		});
	}
	/// <summary>
	/// Preenche um nó de MÉTODO com suas dependências recursivas.
	/// Operação: Algoritmo de busca em Grafo (BFS/DFS) em memória.
	/// </summary>
	// ARQUIVO: DependencyAnalysisOrchestrator.cs

	public async Task EnrichMethodFlowAsync(FileSystemItem item, string projectPath)
	{
		// Se já tiver filhos carregados, não faz nada (evita recarregar)
		if (item.Children.Any()) return;

		// Cede a vez para a UI não travar antes de processar
		await Task.Yield();

		// Obtém o grafo (já em cache se disponível)
		var graph = await _indexService.GetOrIndexProjectAsync(projectPath);

		// Identifica o nó correspondente ao item clicado
		var methodId = item.MethodSignature;
		if (string.IsNullOrEmpty(methodId) || !graph.Nodes.TryGetValue(methodId, out var startNode))
			return;

		// ----------------------------------------------------------------------------------
		// REFATORAÇÃO AQUI:
		// Como agora o SymbolLink guarda posições (Start/Length), podemos ter múltiplos links
		// apontando para o mesmo TargetId (ex: duas chamadas ao mesmo método).
		// Para a Árvore Visual, queremos apenas itens únicos, então agrupamos pelo TargetId.
		// ----------------------------------------------------------------------------------
		var uniqueLinks = startNode.OutgoingLinks
			.GroupBy(link => link.TargetId)
			.Select(group => group.First()) // Pega um representante do grupo
			.ToList();

		if (!uniqueLinks.Any()) return;

		var flowItems = new List<FileSystemItem>();       // Chamadas de métodos/acessos
		var dependencyItems = new List<FileSystemItem>(); // Tipos utilizados

		foreach (var link in uniqueLinks)
		{
			// Tenta achar o nó destino no grafo
			if (graph.Nodes.TryGetValue(link.TargetId, out var targetNode))
			{
				// Categoria 1: Fluxo de Execução (Chamadas e Acessos)
				if (link.Type == LinkType.Calls || link.Type == LinkType.Accesses)
				{
					// Ícone diferente se for Método (cubo roxo) ou Propriedade (chave inglesa)
					string icon = targetNode.Type == SymbolType.Method ? "\uF158" : "\uE946";

					var child = CreateItemFromNode(targetNode, FileSystemItemType.Method, icon);
					child.IsChecked = true; // Marca por padrão para facilitar seleção de contexto
					flowItems.Add(child);
				}
				// Categoria 2: Dependências de Tipos (UsesType, Inherits, etc)
				else if (link.Type == LinkType.UsesType || link.Type == LinkType.Inherits || link.Type == LinkType.Implements)
				{
					var child = CreateItemFromNode(targetNode, FileSystemItemType.Dependency, "\uE972");
					child.IsChecked = true;
					dependencyItems.Add(child);
				}
			}
		}

		// Atualiza a UI na Thread Principal
		Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
		{
			// Adiciona grupo "Chamadas & Acessos" se houver itens
			if (flowItems.Count > 0)
			{
				var flowGroup = _itemFactory.CreateWrapper($"{item.FullPath}::flow", FileSystemItemType.LogicalGroup, "\uE80D");
				flowGroup.SharedState.Name = "Chamadas & Acessos";

				foreach (var child in flowItems)
					flowGroup.Children.Add(child);

				item.Children.Add(flowGroup);
			}

			// Adiciona grupo "Tipos Usados" se houver itens
			if (dependencyItems.Count > 0)
			{
				var depsGroup = _itemFactory.CreateWrapper($"{item.FullPath}::deps", FileSystemItemType.LogicalGroup, "\uE71D");
				depsGroup.SharedState.Name = "Tipos Usados";

				foreach (var child in dependencyItems)
					depsGroup.Children.Add(child);

				item.Children.Add(depsGroup);
			}

			// Expande o item pai para mostrar os resultados
			if (item.Children.Any())
				item.IsExpanded = true;
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