using ContextWinUI.Core.Contracts;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Shared;

public class ContentGenerationService : IContentGenerationService
{
	private readonly IFileSystemService _fileSystemService;
	private readonly IRoslynAnalyzerService _roslynAnalyzer;

	public ContentGenerationService(IFileSystemService fileSystemService, IRoslynAnalyzerService roslynAnalyzer)
	{
		_fileSystemService = fileSystemService;
		_roslynAnalyzer = roslynAnalyzer;
	}

	public async Task<string> GenerateContentAsync(IEnumerable<FileSystemItem> items, IProjectSessionManager settings)
	{
		var sb = new StringBuilder();
		var selectedFiles = items.ToList();

		// 1. Pré-Prompt
		if (!string.IsNullOrWhiteSpace(settings.PrePrompt))
		{
			sb.AppendLine(settings.PrePrompt);
			sb.AppendLine();
			sb.AppendLine();
		}

		// 2. Estrutura de Pastas
		if (settings.IncludeStructure)
		{
			sb.AppendLine("/* --- ESTRUTURA DO PROJETO --- */");
			// Nota: StructureGeneratorHelper.GenerateTree geralmente espera a raiz ou lista plana. 
			// Se ele esperar raiz hierárquica e 'items' for plana, o resultado pode ser estranho, 
			// mas mantendo a lógica original:
			sb.AppendLine(StructureGeneratorHelper.GenerateTree(selectedFiles, settings.StructureOnlyFolders));
			sb.AppendLine();
			sb.AppendLine();
		}

		// 3. Conteúdo dos Arquivos
		sb.AppendLine("/* --- CONTEÚDO DOS ARQUIVOS --- */");
		sb.AppendLine();
		sb.AppendLine();

		// Agrupar itens por arquivo físico
		var fileGroups = selectedFiles.GroupBy(GetPhysicalPath);

		foreach (var group in fileGroups)
		{
			string filePath = group.Key;
			if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) continue;

			sb.AppendLine($"// ==================== {filePath} ====================");
			sb.AppendLine();

			var methodItems = group.Where(i => i.Type == FileSystemItemType.Method).ToList();
			string content;

			if (methodItems.Any()) // Seleção parcial (apenas métodos específicos)
			{
				var signatures = methodItems.Select(x => x.MethodSignature).Cast<string>();
				content = await _roslynAnalyzer.FilterClassContentAsync(
					filePath, signatures,
					settings.OmitUsings, settings.OmitNamespaces, settings.OmitComments, settings.OmitEmptyLines);
			}
			else // Arquivo inteiro
			{
				var rawContent = await _fileSystemService.ReadFileContentAsync(filePath);
				content = CodeCleanupHelper.ProcessCode(
					rawContent, Path.GetExtension(filePath),
					settings.OmitUsings, settings.OmitNamespaces, settings.OmitComments, settings.OmitEmptyLines);
			}

			sb.AppendLine(content);
			sb.AppendLine();
			sb.AppendLine();
		}

		return sb.ToString();
	}

	private string GetPhysicalPath(FileSystemItem item)
	{
		if (item.Type == FileSystemItemType.Method || item.Type == FileSystemItemType.LogicalGroup)
		{
			var parts = item.FullPath.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
			return parts.Length > 0 ? parts[0] : item.FullPath;
		}
		return item.FullPath;
	}
}