// ==================== C:\Users\vinic\source\repos\ContextWinUI\ContextWinUI\Services\RoslynAnalyzerService.cs ====================

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class RoslynAnalyzerService
{
	// Mapa global do projeto: NomeDoTipo (SimpleName) -> CaminhoDoArquivo
	private Dictionary<string, string> _projectTypeMap = new();

	public class FileAnalysisResult
	{
		public List<string> Methods { get; set; } = new();
		public List<string> Dependencies { get; set; } = new();
	}

	/// <summary>
	/// Varre todo o diretório para mapear onde cada classe/interface está definida.
	/// Essencial para saber para qual arquivo navegar ao clicar em uma dependência.
	/// </summary>
	public async Task IndexProjectAsync(string rootPath)
	{
		_projectTypeMap.Clear();

		var csFiles = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories)
			.Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains(".g.cs"));

		var tasks = csFiles.Select(async file =>
		{
			try
			{
				var code = await File.ReadAllTextAsync(file);
				var tree = CSharpSyntaxTree.ParseText(code);
				var root = await tree.GetRootAsync();

				// Pega declarações de classes, interfaces, structs, records, enums
				var typeDeclarations = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();
				var typesInFile = typeDeclarations.Select(t => t.Identifier.Text).ToList();

				return (FilePath: file, Types: typesInFile);
			}
			catch
			{
				return (FilePath: file, Types: new List<string>());
			}
		});

		var results = await Task.WhenAll(tasks);

		foreach (var result in results)
		{
			foreach (var typeName in result.Types)
			{
				_projectTypeMap[typeName] = result.FilePath;
			}
		}
	}

	/// <summary>
	/// Analisa um arquivo para montar a árvore visual inicial (Métodos e Dependências Gerais).
	/// </summary>
	public async Task<FileAnalysisResult> AnalyzeFileStructureAsync(string filePath)
	{
		var result = new FileAnalysisResult();

		try
		{
			var code = await File.ReadAllTextAsync(filePath);
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = await tree.GetRootAsync();

			// 1. Extrair Assinaturas de Métodos
			var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
			foreach (var method in methods)
			{
				var paramsList = method.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var");
				var signature = $"{method.Identifier.Text}({string.Join(", ", paramsList)})";
				result.Methods.Add(signature);
			}

			// 2. Extrair Identificadores do corpo (Dependências de uso)
			var identifiers = root.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Where(id => IsValidDependency(id))
				.Select(id => id.Identifier.Text)
				.Distinct();

			// 3. Extrair Herança e Interfaces (CORREÇÃO DE GENÉRICOS)
			// Antes pegávamos apenas o tipo base. Agora descemos na árvore do tipo base.
			// Ex: class A : Base<IInterfaceA, List<IInterfaceB>>
			var baseTypesList = new List<string>();
			var baseDecls = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>();

			foreach (var baseDecl in baseDecls)
			{
				if (baseDecl.BaseList != null)
				{
					foreach (var baseType in baseDecl.BaseList.Types)
					{
						// DescendantNodesAndSelf pega tudo dentro da declaração de herança.
						// Isso inclui "BaseWorkerActor", "ProductTrainerConfig", "IProductTrainerWorkerActor", etc.
						var typesInBase = baseType.DescendantNodesAndSelf()
												  .OfType<IdentifierNameSyntax>()
												  .Select(id => id.Identifier.Text);
						baseTypesList.AddRange(typesInBase);
					}
				}
			}

			// 4. Cruzar dados e remover duplicatas
			var allPotentialDeps = identifiers.Concat(baseTypesList).Distinct();
			var dependencies = new HashSet<string>();

			foreach (var id in allPotentialDeps)
			{
				// Verifica se existe no projeto e não é o próprio arquivo
				if (_projectTypeMap.TryGetValue(id, out var depPath) && depPath != filePath)
				{
					dependencies.Add(depPath);
				}
			}
			result.Dependencies = dependencies.ToList();
		}
		catch { /* Ignorar erros de parse */ }

		return result;
	}

	/// <summary>
	/// Analisa o corpo de um método específico para encontrar chamadas a outros métodos.
	/// Resolve o tipo da variável para mostrar "ITipo.Metodo" ao invés de "variavel.Metodo".
	/// </summary>
	public async Task<List<string>> GetMethodCallsAsync(string filePath, string methodSignature)
	{
		var calls = new List<string>();
		try
		{
			var code = await File.ReadAllTextAsync(filePath);
			var tree = CSharpSyntaxTree.ParseText(code);
			var root = await tree.GetRootAsync();

			// 1. Mapear Variáveis da Classe -> Tipos (Propriedades e Campos)
			var classVariableMap = BuildClassVariableMap(root);

			// Tenta achar o método pelo nome simples (ignorando params por enquanto para robustez)
			var methodName = methodSignature.Split('(')[0].Trim();

			var methodNode = root.DescendantNodes()
								 .OfType<MethodDeclarationSyntax>()
								 .FirstOrDefault(m => m.Identifier.Text == methodName);

			if (methodNode != null && methodNode.Body != null)
			{
				// Pega todas as invocações dentro do corpo
				var invocations = methodNode.Body.DescendantNodes().OfType<InvocationExpressionSyntax>();

				foreach (var invocation in invocations)
				{
					string callName = "";

					// Caso A: Chamada simples (ex: MetodoPrivado())
					if (invocation.Expression is IdentifierNameSyntax simpleName)
					{
						callName = simpleName.Identifier.Text;
					}
					// Caso B: Acesso a membro (ex: _service.DoWork())
					else if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
					{
						var objectName = memberAccess.Expression.ToString(); // ex: "_service"
						var methodCalled = memberAccess.Name.Identifier.Text; // ex: "DoWork"

						// Tenta resolver "_service" para "IMyService"
						if (classVariableMap.TryGetValue(objectName, out var typeName))
						{
							// Retorna "IMyService.DoWork"
							callName = $"{typeName}.{methodCalled}";
						}
						else
						{
							// Retorna "_service.DoWork" (Fallback)
							callName = $"{objectName}.{methodCalled}";
						}
					}

					if (!string.IsNullOrEmpty(callName))
					{
						calls.Add(callName);
					}
				}
			}
		}
		catch { }

		return calls.Distinct().ToList();
	}

	// --- MÉTODOS AUXILIARES ---

	/// <summary>
	/// Cria um mapa de (NomeDaVariavel -> TipoDaVariavel) escaneando Propriedades e Campos.
	/// Essencial para Injeção de Dependência.
	/// </summary>
	private Dictionary<string, string> BuildClassVariableMap(SyntaxNode root)
	{
		var map = new Dictionary<string, string>();

		// 1. Mapear Propriedades: public IService Service { get; }
		var props = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
		foreach (var prop in props)
		{
			map[prop.Identifier.Text] = ExtractSimpleName(prop.Type.ToString());
		}

		// 2. Mapear Campos: private readonly IService _service;
		var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
		foreach (var field in fields)
		{
			var typeName = ExtractSimpleName(field.Declaration.Type.ToString());
			foreach (var variable in field.Declaration.Variables)
			{
				map[variable.Identifier.Text] = typeName;
			}
		}

		return map;
	}

	/// <summary>
	/// Verifica se um identificador é código válido ou apenas importação/declaração.
	/// </summary>
	private bool IsValidDependency(IdentifierNameSyntax id)
	{
		// Ignora se estiver dentro de um 'using ...;'
		if (id.FirstAncestorOrSelf<UsingDirectiveSyntax>() != null)
			return false;

		// Ignora se for parte do nome do namespace atual (ex: namespace Meu.Projeto.Services)
		var namespaceDecl = id.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
		if (namespaceDecl != null && namespaceDecl.Name.ToString().Contains(id.Identifier.Text))
			return false;

		return true;
	}

	/// <summary>
	/// Remove generics para facilitar a busca no mapa.
	/// Ex: "BaseActor<Config>" -> "BaseActor"
	/// </summary>
	private string ExtractSimpleName(string typeName)
	{
		if (typeName.Contains("<"))
		{
			return typeName.Substring(0, typeName.IndexOf("<"));
		}
		return typeName;
	}
}