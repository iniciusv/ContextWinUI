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
	/// Constrói o texto final para o LLM usando Slicing baseado em Grafo.
	/// </summary>
	// ARQUIVO: DependencyAnalysisOrchestrator.cs


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
			.Where(g => !string.IsNullOrEmpty(g.Key));

		foreach (var group in itemsByFile)
		{
			string filePath = group.Key;
			string fileContent = string.Empty;

			// --- CORREÇÃO AQUI ---
			// Não usamos _cachedCompilation diretamente. Pedimos ao serviço.
			var memoryContent = await _indexService.GetSourceContentAsync(filePath);

			if (memoryContent != null)
			{
				fileContent = memoryContent;
			}
			else
			{
				// Fallback para disco se não achar na memória
				if (File.Exists(filePath))
				{
					fileContent = await _fileSystemService.ReadFileContentAsync(filePath);
				}
				else
				{
					continue;
				}
			}
			// ---------------------

			bool isFullFileSelected = group.Any(i => i.Type == FileSystemItemType.File && i.IsChecked);

			if (isFullFileSelected)
			{
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
				var explicitNodeIds = new HashSet<int>();
				foreach (var i in group)
				{
					if (!string.IsNullOrEmpty(i.MethodSignature) && int.TryParse(i.MethodSignature, out int id))
					{
						explicitNodeIds.Add(id);
					}
				}

				if (explicitNodeIds.Count == 0) continue;

				sb.AppendLine($"// ARQUIVO: {Path.GetFileName(filePath)} (Trechos selecionados)");

				var nodesToExport = explicitNodeIds
					.Select(id => graph.Nodes.TryGetValue(id, out var n) ? n : null)
					.Where(n => n != null)
					.OrderBy(n => n!.StartPosition)
					.ToList();

				foreach (var node in nodesToExport)
				{
					if (node == null) continue;

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