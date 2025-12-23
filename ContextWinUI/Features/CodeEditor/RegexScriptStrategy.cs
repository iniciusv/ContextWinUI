//using ContextWinUI.Core.Contracts;
//using ContextWinUI.Services;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//namespace ContextWinUI.Features.CodeAnalyses;

//public class RegexScriptStrategy : ILanguageStrategy
//{
//	private readonly Dictionary<string, string> _projectTypeMap;

//	public RegexScriptStrategy(Dictionary<string, string> projectTypeMap)
//	{
//		_projectTypeMap = projectTypeMap;
//	}

//	public bool CanHandle(string extension)
//	{
//		var ext = extension.ToLower();
//		return ext == ".js" || ext == ".jsx" || ext == ".ts" || ext == ".tsx" || ext == ".vue";
//	}

//	public async Task<RoslynAnalyzerService.FileAnalysisResult> AnalyzeAsync(string filePath)
//	{
//		var result = new RoslynAnalyzerService.FileAnalysisResult();
//		try
//		{
//			var code = await File.ReadAllTextAsync(filePath);

//			// 1. Extrair Funções (Simplificado via Regex)

//			// function myFunc()
//			var functionMatches = Regex.Matches(code, @"function\s+(\w+)\s*\(");
//			foreach (Match m in functionMatches) result.Methods.Add(m.Groups[1].Value + "()");

//			// const myFunc = () => 
//			var arrowMatches = Regex.Matches(code, @"(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s*)?(?:\(|[^=]*=>)");
//			foreach (Match m in arrowMatches) result.Methods.Add(m.Groups[1].Value + " (arrow)");

//			// Métodos de classe/objeto: myMethod() {
//			var methodMatches = Regex.Matches(code, @"(\w+)\s*\([^\)]*\)\s*\{");
//			var ignoredMethods = new[] { "if", "switch", "while", "for", "catch", "constructor" };
//			foreach (Match m in methodMatches)
//			{
//				var name = m.Groups[1].Value;
//				if (!ignoredMethods.Contains(name)) result.Methods.Add(name + "()");
//			}

//			// 2. Extrair Dependências (Imports, Requires e Vue Components)
//			var dependencies = new HashSet<string>();

//			// import X from 'path'
//			var importMatches = Regex.Matches(code, @"(?:import|from)\s+['""]([^'""]+)['""]");
//			// require('path')
//			var requireMatches = Regex.Matches(code, @"require\s*\(\s*['""]([^'""]+)['""]\s*\)");

//			var allImports = importMatches.Select(m => m.Groups[1].Value)
//										  .Concat(requireMatches.Select(m => m.Groups[1].Value));

//			foreach (var importPath in allImports)
//			{
//				// Tenta resolver o nome do arquivo (ex: ./Components/Header -> Header)
//				var possibleName = Path.GetFileName(importPath);
//				if (possibleName.Contains(".")) possibleName = Path.GetFileNameWithoutExtension(possibleName);

//				if (_projectTypeMap.TryGetValue(possibleName, out var depPath) && depPath != filePath)
//				{
//					dependencies.Add(depPath);
//				}
//			}

//			// Vue Components: components: { Button, Header }
//			var vueComponents = Regex.Matches(code, @"components\s*:\s*\{([^}]+)\}");
//			foreach (Match m in vueComponents)
//			{
//				var comps = m.Groups[1].Value.Split(',');
//				foreach (var c in comps)
//				{
//					var cleanName = c.Trim();
//					if (_projectTypeMap.TryGetValue(cleanName, out var depPath))
//						dependencies.Add(depPath);
//				}
//			}

//			result.Dependencies = dependencies.ToList();
//		}
//		catch { }

//		return result;
//	}
//}