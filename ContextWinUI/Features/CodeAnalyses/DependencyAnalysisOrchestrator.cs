
using ContextWinUI.Core.Contracts;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.CodeAnalyses;

public class DependencyAnalysisOrchestrator : IDependencyAnalysisOrchestrator
{
	private readonly IRoslynAnalyzerService _roslynAnalyzer;
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly IFileSystemService _fileSystemService;

	public DependencyAnalysisOrchestrator(
		IRoslynAnalyzerService roslynAnalyzer,
		IFileSystemItemFactory itemFactory,
		IFileSystemService fileSystemService)
	{
		_roslynAnalyzer = roslynAnalyzer;
		_itemFactory = itemFactory;
		_fileSystemService = fileSystemService;
	}

	// Lógica do botão "+" (Análise Profunda)
	public async Task EnrichFileNodeAsync(FileSystemItem item, string projectPath)
	{
		if (string.IsNullOrEmpty(projectPath)) return;

		await _roslynAnalyzer.IndexProjectAsync(projectPath);
		item.Children.Clear();
		var analysis = await _roslynAnalyzer.AnalyzeFileStructureAsync(item.FullPath);
		if (analysis.Methods.Any())
		{
			var methodsGroup = _itemFactory.CreateWrapper($"{item.FullPath}::methods", FileSystemItemType.LogicalGroup, "\uEA86");
			methodsGroup.SharedState.Name = "Métodos";

			foreach (var method in analysis.Methods)
			{
				var methodItem = _itemFactory.CreateWrapper($"{item.FullPath}::{method}", FileSystemItemType.Method, "\uF158");
				methodItem.SharedState.Name = method;
				methodItem.MethodSignature = method;
				methodsGroup.Children.Add(methodItem);
			}
			item.Children.Add(methodsGroup);
		}

		if (analysis.Dependencies.Any())
		{
			var depsGroup = _itemFactory.CreateWrapper($"{item.FullPath}::deps", FileSystemItemType.LogicalGroup, "\uE71D");
			depsGroup.SharedState.Name = "Dependências (Classes Usadas)";

			foreach (var depPath in analysis.Dependencies)
			{
				var depItem = _itemFactory.CreateWrapper(depPath, FileSystemItemType.Dependency, "\uE972");
				depsGroup.Children.Add(depItem);
			}
			item.Children.Add(depsGroup);
		}
	}

	public async Task EnrichMethodFlowAsync(FileSystemItem item, string projectPath)
	{
		string realPath = GetPhysicalPath(item);
		string signature = item.MethodSignature;

		if (string.IsNullOrEmpty(realPath) || string.IsNullOrEmpty(signature)) return;

		if (!string.IsNullOrEmpty(projectPath))
			await _roslynAnalyzer.IndexProjectAsync(projectPath);

		var result = await _roslynAnalyzer.AnalyzeMethodBodyAsync(realPath, signature);

		item.Children.Clear();

		if (result.InternalMethodCalls.Any())
		{
			var callsGroup = _itemFactory.CreateWrapper($"{realPath}::flow_calls", FileSystemItemType.LogicalGroup, "\uE80D");
			callsGroup.SharedState.Name = "Chamadas (Internas)";

			foreach (var call in result.InternalMethodCalls)
			{
				var callItem = _itemFactory.CreateWrapper($"{realPath}::{call}", FileSystemItemType.Method, "\uF158");
				callItem.SharedState.Name = call + "(...)";
				callItem.MethodSignature = call;
				callItem.IsChecked = true;
				callsGroup.Children.Add(callItem);
			}
			item.Children.Add(callsGroup);
		}

		if (result.ExternalDependencies.Any())
		{
			var depsGroup = _itemFactory.CreateWrapper($"{realPath}::flow_deps", FileSystemItemType.LogicalGroup, "\uE71D");
			depsGroup.SharedState.Name = "Usa (Classes/Tipos)";

			foreach (var kvp in result.ExternalDependencies)
			{
				var depItem = _itemFactory.CreateWrapper(kvp.Value, FileSystemItemType.Dependency, "\uE972");
				depItem.IsChecked = true;
				depsGroup.Children.Add(depItem);
			}
			item.Children.Add(depsGroup);
		}
	}

	public async Task<string> BuildContextStringAsync(IEnumerable<FileSystemItem> selectedItems, IProjectSessionManager sessionSettings)
	{
		var sb = new StringBuilder();
		sb.AppendLine("/* CONTEXTO SELECIONADO */");
		sb.AppendLine();

		var fileGroups = selectedItems.GroupBy(item => GetPhysicalPath(item)).ToList();

		foreach (var group in fileGroups)
		{
			string filePath = group.Key;
			if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) continue;

			sb.AppendLine($"// ==================== {Path.GetFileName(filePath)} ====================");

			var selectedMethodSignatures = group
				.Where(item => item.Type == FileSystemItemType.Method)
				.Select(item => item.MethodSignature)
				.Where(s => !string.IsNullOrEmpty(s))
				.Cast<string>()
				.ToList();

			bool isFullFileRequested = group.Any(item =>
				item.Type == FileSystemItemType.File ||
				item.Type == FileSystemItemType.Dependency);

			if (selectedMethodSignatures.Any() && !isFullFileRequested)
			{
				sb.AppendLine("// (Conteúdo filtrado: Estrutura + Métodos selecionados)");
				var content = await _roslynAnalyzer.FilterClassContentAsync(
					filePath,
					selectedMethodSignatures,
					sessionSettings.OmitUsings,
					sessionSettings.OmitNamespaces,
					sessionSettings.OmitComments,
					sessionSettings.OmitEmptyLines
				);
				sb.AppendLine(content);
			}
			else
			{
				var rawContent = await _fileSystemService.ReadFileContentAsync(filePath);
				var cleanContent = CodeCleanupHelper.ProcessCode(
					rawContent,
					Path.GetExtension(filePath),
					sessionSettings.OmitUsings,
					sessionSettings.OmitNamespaces,
					sessionSettings.OmitComments,
					sessionSettings.OmitEmptyLines);
				sb.AppendLine(cleanContent);
			}

			sb.AppendLine();
			sb.AppendLine();
		}

		return sb.ToString();
	}

	private string GetPhysicalPath(FileSystemItem item)
	{
		if (string.IsNullOrEmpty(item.FullPath)) return string.Empty;
		int separatorIndex = item.FullPath.IndexOf("::");
		return separatorIndex > 0 ? item.FullPath.Substring(0, separatorIndex) : item.FullPath;
	}
}