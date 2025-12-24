//using ContextWinUI.Features.CodeAnalyses;
//using ContextWinUI.Models;
//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
//using System.IO;
//using System.Linq;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;

//namespace ContextWinUI.Services;

//public class SmartSnippetPatcher
//{
//	private readonly SemanticIndexService _indexService;
//	private static readonly Regex HeaderRegex = new Regex(
//		@"^(?:\/\/\s*ARQUIVO:|\/\/\s*FILE:|\*\*\s*ARQUIVO:|\*\*\s*FILE:)\s*(?<path>[\w\.\-\\\/\s]+?)(?:\s*$|\s*\n)",
//		RegexOptions.Multiline | RegexOptions.IgnoreCase);

//	public SmartSnippetPatcher(SemanticIndexService indexService)
//	{
//		_indexService = indexService;
//	}

//	public async Task<ProposedFileChange?> PatchAsync(string snippetCode, string projectRoot)
//	{
//		string? targetFilePath = DetectFileFromHeader(snippetCode, projectRoot);

//		// Se não achou pelo cabeçalho, tenta inferir pela estrutura
//		if (string.IsNullOrEmpty(targetFilePath))
//		{
//			// ALTERAÇÃO: Reutilizando a lógica centralizada no AiResponseParser
//			// Passamos o grafo atual do SemanticIndexService
//			var currentGraph = _indexService.GetCurrentGraph();
//			targetFilePath = AiResponseParser.TryFindFileByCodeStructure(snippetCode, currentGraph);
//		}

//		// Se falhar a inferência, aborta
//		if (string.IsNullOrEmpty(targetFilePath) || !File.Exists(targetFilePath)) return null;

//		// --- Daqui para baixo é a lógica de merge (AST Rewriting) ---

//		string wrappedCode = $"public class __WrapperTemp__ {{ \n{snippetCode}\n }}";
//		var wrappedTree = CSharpSyntaxTree.ParseText(wrappedCode);
//		var wrappedRoot = wrappedTree.GetRoot();

//		var wrapperClass = wrappedRoot.DescendantNodes()
//									  .OfType<ClassDeclarationSyntax>()
//									  .FirstOrDefault(c => c.Identifier.Text == "__WrapperTemp__");

//		if (wrapperClass == null) return null;

//		string originalCode = await File.ReadAllTextAsync(targetFilePath);
//		var originalTree = CSharpSyntaxTree.ParseText(originalCode);

//		var mainClassInOriginal = originalTree.GetRoot().DescendantNodes()
//											  .OfType<ClassDeclarationSyntax>()
//											  .FirstOrDefault();

//		if (mainClassInOriginal == null) return null;

//		// Usa o RobustMergerRewriter para mesclar os membros encontrados
//		var rewriter = new RobustMergerRewriter(mainClassInOriginal.Identifier.Text, wrapperClass.Members);
//		var newRoot = rewriter.Visit(originalTree.GetRoot());

//		newRoot = newRoot.NormalizeWhitespace();

//		return new ProposedFileChange
//		{
//			FilePath = targetFilePath,
//			OriginalContent = originalCode,
//			NewContent = newRoot.ToFullString(),
//			Status = "Smart Merge (Inserção/Edição)"
//		};
//	}

//	private string? DetectFileFromHeader(string snippet, string projectRoot)
//	{
//		var match = HeaderRegex.Match(snippet);
//		if (match.Success)
//		{
//			string relativePath = match.Groups["path"].Value.Trim();
//			relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

//			string fullPath = Path.Combine(projectRoot, relativePath);
//			if (File.Exists(fullPath)) return fullPath;

//			// Tentativa fuzzy se o caminho exato falhar
//			var files = Directory.GetFiles(projectRoot, Path.GetFileName(relativePath), SearchOption.AllDirectories);
//			return files.FirstOrDefault();
//		}
//		return null;
//	}
//}