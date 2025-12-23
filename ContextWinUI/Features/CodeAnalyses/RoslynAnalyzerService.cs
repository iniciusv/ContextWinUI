//using ContextWinUI.Core.Contracts;
//using ContextWinUI.Core.Models;
//using ContextWinUI.Core.Services;
//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Threading.Tasks;

//namespace ContextWinUI.Features.CodeAnalyses;

//public class RoslynAnalyzerService : IRoslynAnalyzerService
//{
//	private Dictionary<string, string> _projectTypeMap = new();
//	private readonly List<ILanguageStrategy> _strategies;

//	public class FileAnalysisResult
//	{
//		public List<string> Methods { get; set; } = new();
//		public List<string> Dependencies { get; set; } = new();
//	}

//	public RoslynAnalyzerService()
//	{
//		_strategies = new List<ILanguageStrategy>
//		{
//			new RoslynCsharpStrategy(_projectTypeMap),
//			new RegexScriptStrategy(_projectTypeMap)
//		};
//	}

//	public async Task IndexProjectAsync(string rootPath)
//	{
//		_projectTypeMap.Clear();

//		if (Directory.Exists(rootPath))
//		{
//			var csFiles = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories)
//				.Where(f => !IsIgnoredFolder(f));

//			foreach (var file in csFiles)
//			{
//				try
//				{
//					var code = await File.ReadAllTextAsync(file);
//					var tree = CSharpSyntaxTree.ParseText(code);
//					var root = await tree.GetRootAsync();
//					var typeDeclarations = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();
//					foreach (var t in typeDeclarations)
//					{
//						_projectTypeMap[t.Identifier.Text] = file;
//					}
//				}
//				catch { }
//			}

//			var jsExtensions = new[] { "*.js", "*.jsx", "*.ts", "*.tsx", "*.vue" };
//			foreach (var ext in jsExtensions)
//			{
//				var jsFiles = Directory.GetFiles(rootPath, ext, SearchOption.AllDirectories)
//					.Where(f => !IsIgnoredFolder(f));

//				foreach (var file in jsFiles)
//				{
//					var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
//					if (!_projectTypeMap.ContainsKey(fileNameNoExt))
//					{
//						_projectTypeMap[fileNameNoExt] = file;
//					}
//				}
//			}
//		}
//	}

//	private bool IsIgnoredFolder(string path)
//	{
//		return path.Contains("\\bin\\") ||
//			   path.Contains("\\obj\\") ||
//			   path.Contains("\\node_modules\\") ||
//			   path.Contains(".g.cs") ||
//			   path.Contains(".min.js");
//	}

//	public async Task<FileAnalysisResult> AnalyzeFileStructureAsync(string filePath)
//	{
//		var ext = Path.GetExtension(filePath).ToLower();
//		var strategy = _strategies.FirstOrDefault(s => s.CanHandle(ext));

//		if (strategy != null)
//		{
//			return await strategy.AnalyzeAsync(filePath);
//		}

//		return new FileAnalysisResult();
//	}

//	public async Task<string> FilterClassContentAsync(
//		string filePath,
//		IEnumerable<string>? keptMethodSignatures,
//		bool removeUsings,
//		bool removeNamespaces,
//		bool removeComments,
//		bool removeEmptyLines)
//	{
//		if (!File.Exists(filePath)) return string.Empty;

//		try
//		{
//			var code = await File.ReadAllTextAsync(filePath);
//			var tree = CSharpSyntaxTree.ParseText(code);
//			var root = await tree.GetRootAsync();

//			// O Rewriter trata da estrutura sintática (remover métodos não selecionados, usings, comentários triviais)
//			var rewriter = new MethodFilterRewriter(keptMethodSignatures, removeUsings, removeComments);

//			var newRoot = rewriter.Visit(root);

//			if (newRoot != null)
//			{
//				var processedCode = newRoot.ToFullString();
//				// O Helper trata das limpezas textuais finais (Namespaces e Linhas vazias)
//				return Helpers.CodeCleanupHelper.ProcessCode(processedCode, ".cs", false, removeNamespaces, false, removeEmptyLines);
//			}
//		}
//		catch { }

//		return string.Empty;
//	}

//	public async Task<List<string>> GetMethodCallsAsync(string filePath, string methodSignature)
//	{
//		if (!filePath.EndsWith(".cs")) return new List<string>();

//		var calls = new List<string>();
//		try
//		{
//			var code = await File.ReadAllTextAsync(filePath);
//			var tree = CSharpSyntaxTree.ParseText(code);
//			var root = await tree.GetRootAsync();

//			var classVariableMap = BuildClassVariableMap(root);
//			var methodName = methodSignature.Split('(')[0].Trim();

//			var methodNode = root.DescendantNodes()
//									 .OfType<MethodDeclarationSyntax>()
//									 .FirstOrDefault(m => m.Identifier.Text == methodName);

//			if (methodNode != null && methodNode.Body != null)
//			{
//				var invocations = methodNode.Body.DescendantNodes().OfType<InvocationExpressionSyntax>();

//				foreach (var invocation in invocations)
//				{
//					string callName = "";

//					if (invocation.Expression is IdentifierNameSyntax simpleName)
//					{
//						callName = simpleName.Identifier.Text;
//					}
//					else if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
//					{
//						var objectName = memberAccess.Expression.ToString();
//						var methodCalled = memberAccess.Name.Identifier.Text;

//						if (classVariableMap.TryGetValue(objectName, out var typeName))
//						{
//							callName = $"{typeName}.{methodCalled}";
//						}
//						else
//						{
//							callName = $"{objectName}.{methodCalled}";
//						}
//					}

//					if (!string.IsNullOrEmpty(callName))
//					{
//						calls.Add(callName);
//					}
//				}
//			}
//		}
//		catch { }

//		return calls.Distinct().ToList();
//	}

//	private Dictionary<string, string> BuildClassVariableMap(SyntaxNode root)
//	{
//		var map = new Dictionary<string, string>();
//		var props = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
//		foreach (var prop in props)
//		{
//			map[prop.Identifier.Text] = ExtractSimpleName(prop.Type.ToString());
//		}
//		var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
//		foreach (var field in fields)
//		{
//			var typeName = ExtractSimpleName(field.Declaration.Type.ToString());
//			foreach (var variable in field.Declaration.Variables)
//			{
//				map[variable.Identifier.Text] = typeName;
//			}
//		}
//		return map;
//	}

//	private string ExtractSimpleName(string typeName)
//	{
//		if (typeName.Contains("<"))
//		{
//			return typeName.Substring(0, typeName.IndexOf("<"));
//		}
//		return typeName;
//	}

//	public async Task<MethodBodyResult> AnalyzeMethodBodyAsync(string filePath, string methodSignature)
//	{
//		// 1. Validar e Ler Arquivo
//		if (!File.Exists(filePath)) return new MethodBodyResult();
//		var code = await File.ReadAllTextAsync(filePath);

//		// 2. Parse do código
//		var tree = CSharpSyntaxTree.ParseText(code);
//		var root = await tree.GetRootAsync();

//		// Setup do compilador (necessário para o SemanticModel funcionar bem)
//		var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
//		var compilation = CSharpCompilation.Create("Analysis")
//			.AddReferences(mscorlib)
//			.AddSyntaxTrees(tree);

//		var semanticModel = compilation.GetSemanticModel(tree);

//		// =======================================================================
//		// 3. ESTRATÉGIA DE BUSCA ROBUSTA (A Correção)
//		// =======================================================================

//		// Extrai apenas o nome do método da assinatura (ex: "Salvar(string)" -> "Salvar")
//		var targetName = methodSignature.Contains("(")
//			? methodSignature.Substring(0, methodSignature.IndexOf("(")).Trim()
//			: methodSignature.Trim();

//		// Busca TODOS os métodos com esse nome
//		var candidates = root.DescendantNodes()
//			.OfType<MethodDeclarationSyntax>()
//			.Where(m => m.Identifier.Text == targetName)
//			.ToList();

//		MethodDeclarationSyntax? methodNode = null;

//		if (candidates.Count == 0)
//		{
//			// Debug: Nao achou nenhum metodo com esse nome. 
//			// Verifique se o nome passado em 'methodSignature' está correto.
//			return new MethodBodyResult();
//		}
//		else if (candidates.Count == 1)
//		{
//			// CENÁRIO IDEAL: Só existe um método com esse nome. Não precisamos comparar parâmetros chatos.
//			methodNode = candidates.First();
//		}
//		else
//		{
//			var normalizedTargetSig = methodSignature.Replace(" ", "");

//			methodNode = candidates.FirstOrDefault(m =>
//			{
//				var paramsList = m.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var");
//				var currentSig = $"{m.Identifier.Text}({string.Join(",", paramsList)})";

//				return currentSig.Replace(" ", "") == normalizedTargetSig;
//			});

//			// Fallback: Se a comparação de string falhar, pega o primeiro (melhor que nada)
//			if (methodNode == null) methodNode = candidates.First();
//		}
//		// =======================================================================

//		if (methodNode == null) return new MethodBodyResult();

//		// 4. Preparar Resultado
//		var result = new MethodBodyResult
//		{
//			InternalMethodCalls = new List<string>(),
//			AccessedProperties = new List<string>(), // <--- Inicializando a lista
//			ExternalDependencies = new Dictionary<string, string>()
//		};

//		// 5. Obter Símbolo da Classe
//		var containingClass = methodNode.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
//		if (containingClass == null) return result;

//		var classSymbol = semanticModel.GetDeclaredSymbol(containingClass);

//		// 6. Executar o Walker (Sua versão corrigida com VisitIdentifierName)
//		var walker = new MethodBodyWalker(semanticModel, result, classSymbol);

//		if (methodNode.Body != null)
//			walker.Visit(methodNode.Body);
//		else if (methodNode.ExpressionBody != null)
//			walker.Visit(methodNode.ExpressionBody);

//		return result;
//	}

//	public async Task<HashSet<string>> GetConnectedMethodsRecursivelyAsync(string filePath, string startMethodSignature)
//	{
//		var visited = new HashSet<string>();
//		var queue = new Queue<string>();

//		queue.Enqueue(startMethodSignature);

//		while (queue.Count > 0)
//		{
//			var currentMethod = queue.Dequeue();

//			if (visited.Contains(currentMethod)) continue;
//			visited.Add(currentMethod);

//			var bodyResult = await AnalyzeMethodBodyAsync(filePath, currentMethod);

//			foreach (var internalCall in bodyResult.InternalMethodCalls)
//			{
//				if (!visited.Contains(internalCall))
//				{
//					queue.Enqueue(internalCall);
//				}
//			}
//		}

//		return visited;
//	}


//	private class MethodBodyWalker : CSharpSyntaxWalker
//	{
//		private readonly SemanticModel _semanticModel;
//		private readonly MethodBodyResult _result;
//		private readonly INamedTypeSymbol _containingClassSymbol;

//		public MethodBodyWalker(SemanticModel semanticModel, MethodBodyResult result, INamedTypeSymbol containingClassSymbol)
//		{
//			_semanticModel = semanticModel;
//			_result = result;
//			_containingClassSymbol = containingClassSymbol;
//		}

//		// 1. Captura Chamadas de Métodos (Ex: OnStatusChanged(...))
//		public override void VisitInvocationExpression(InvocationExpressionSyntax node)
//		{
//			var symbolInfo = _semanticModel.GetSymbolInfo(node);
//			var symbol = symbolInfo.Symbol as IMethodSymbol;

//			if (symbol != null)
//			{
//				// Verifica se o método pertence à classe atual
//				if (SymbolEqualityComparer.Default.Equals(symbol.ContainingType, _containingClassSymbol))
//				{
//					// É uma chamada interna (Ex: OnStatusChanged)
//					if (!_result.InternalMethodCalls.Contains(symbol.Name))
//					{
//						_result.InternalMethodCalls.Add(symbol.Name);
//					}
//				}
//				else
//				{
//					// É uma dependência externa (Ex: Clipboard.SetContent)
//					AddExternalDependency(symbol.ContainingType);
//				}
//			}

//			// Continua visitando os filhos para achar argumentos que sejam propriedades
//			base.VisitInvocationExpression(node);
//		}

//		// 2. Captura Propriedades e Campos (Ex: SelectionVM, IsLoading, _sessionManager)
//		// Esta é a correção principal para o seu problema.
//		public override void VisitIdentifierName(IdentifierNameSyntax node)
//		{
//			var symbolInfo = _semanticModel.GetSymbolInfo(node);
//			var symbol = symbolInfo.Symbol;

//			if (symbol != null)
//			{
//				// Estamos interessados apenas em Propriedades (SelectionVM) ou Campos (_sessionManager)
//				bool isPropertyOrField = symbol is IPropertySymbol || symbol is IFieldSymbol;

//				if (isPropertyOrField)
//				{
//					// VERIFICAÇÃO CRUCIAL: O dono dessa propriedade é a classe que estamos analisando?
//					if (SymbolEqualityComparer.Default.Equals(symbol.ContainingType, _containingClassSymbol))
//					{
//						// Sim! É uma propriedade interna. Adiciona à lista.
//						// Isso vai pegar "SelectionVM" mesmo em "SelectionVM.SelectedItemsList"
//						if (!_result.AccessedProperties.Contains(symbol.Name))
//						{
//							_result.AccessedProperties.Add(symbol.Name);
//						}
//					}
//					else
//					{
//						// Se a propriedade pertence a outra classe (Ex: .SelectedItemsList pertence a FileSelectionViewModel)
//						// Registramos o tipo dono dessa propriedade como dependência externa
//						AddExternalDependency(symbol.ContainingType);
//					}
//				}
//			}

//			// Importante chamar a base para continuar a árvore, embora IdentifierName geralmente seja uma folha
//			base.VisitIdentifierName(node);
//		}

//		// Método auxiliar para evitar duplicidade na lógica de dependências externas
//		private void AddExternalDependency(INamedTypeSymbol typeSymbol)
//		{
//			if (typeSymbol == null) return;

//			// Ignora tipos do sistema básico se desejar limpar a visualização (opcional)
//			if (typeSymbol.ContainingNamespace.ToString().StartsWith("System")) return;

//			string typeName = typeSymbol.Name;
//			if (!_result.ExternalDependencies.ContainsKey(typeName))
//			{
//				_result.ExternalDependencies.Add(typeName, typeSymbol.ToDisplayString());
//			}
//		}
//	}
//}