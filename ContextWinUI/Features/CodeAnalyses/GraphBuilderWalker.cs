using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;


namespace ContextWinUI.Features.CodeAnalyses;

using ContextWinUI.Core.Models;
using System.IO;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ContextWinUI.Core.Models; // Onde estão SymbolNode e SymbolLink
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class GraphBuilderWalker : CSharpSyntaxWalker
{
	private readonly DependencyGraph _graph;
	private readonly SemanticModel _semanticModel;

	// OTIMIZAÇÃO #1: Armazenamos int em vez da string do caminho
	private readonly int _fileId;

	private SymbolNode? _contextNode;

	private static readonly SymbolDisplayFormat _idFormat = new SymbolDisplayFormat(
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters,
		parameterOptions: SymbolDisplayParameterOptions.IncludeType,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

	public GraphBuilderWalker(DependencyGraph graph, SemanticModel semanticModel, string filePath)
	{
		_graph = graph;
		_semanticModel = semanticModel;

		// OTIMIZAÇÃO #1: Resolvemos o ID do arquivo uma única vez na inicialização
		var normalizedPath = Path.GetFullPath(filePath);
		_fileId = _graph.GetOrAddFileId(normalizedPath);
	}

	// --- Métodos de Visita ---

	public override void VisitClassDeclaration(ClassDeclarationSyntax node)
	{
		var symbol = _semanticModel.GetDeclaredSymbol(node);
		if (symbol != null)
		{
			var classNode = CreateNode(symbol, node.Span);
			_graph.AddNode(classNode);

			var previousContext = _contextNode;
			_contextNode = classNode;

			// Herança (Base Type)
			if (symbol.BaseType != null && symbol.BaseType.SpecialType == SpecialType.None)
			{
				var baseTypeSyntax = node.BaseList?.Types.FirstOrDefault();
				var span = baseTypeSyntax?.Span ?? node.Identifier.Span;

				// OTIMIZAÇÃO #4: GetId retorna int agora
				AddLink(GetId(symbol.BaseType), LinkType.Inherits, span);
			}

			// Interfaces
			foreach (var iface in symbol.Interfaces)
			{
				int ifaceId = GetId(iface); // ID Inteiro
				AddLink(ifaceId, LinkType.Implements, node.Identifier.Span);

				// OTIMIZAÇÃO: A chave do dicionário InterfaceImplementations continua string (nome da interface)
				// mas a lista contém IDs inteiros dos implementadores.
				string ifaceKey = iface.ToDisplayString(_idFormat);

				_graph.InterfaceImplementations.AddOrUpdate(
					ifaceKey,
					new List<int> { classNode.Id }, // Lista de int
					(k, v) => { lock (v) { v.Add(classNode.Id); return v; } });
			}

			base.VisitClassDeclaration(node);
			_contextNode = previousContext;
		}
	}

	public override void VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		if (_contextNode == null) return;

		var symbolInfo = _semanticModel.GetSymbolInfo(node);
		var symbol = symbolInfo.Symbol as IMethodSymbol;

		if (symbol != null)
		{
			var definition = symbol.OriginalDefinition;
			var locationSpan = node.Expression.Span;
			AddLink(GetId(definition), LinkType.Calls, locationSpan);
		}

		base.VisitInvocationExpression(node);
	}

	public override void VisitIdentifierName(IdentifierNameSyntax node)
	{
		if (_contextNode == null) return;

		var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
		if (symbol == null) return;

		var span = node.Span;

		if (symbol is IPropertySymbol || symbol is IFieldSymbol)
		{
			AddLink(GetId(symbol), LinkType.Accesses, span);

			// Opcional: Dependência do tipo da propriedade/campo
			// var typeSymbol = (symbol as IPropertySymbol)?.Type ?? (symbol as IFieldSymbol)?.Type;
			// AddDependency(typeSymbol, LinkType.UsesType, span); 
		}

		if (symbol is INamedTypeSymbol typeUsed && !IsSystemType(typeUsed))
		{
			AddLink(GetId(typeUsed), LinkType.UsesType, span);
		}

		base.VisitIdentifierName(node);
	}

	public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
	{
		var symbol = _semanticModel.GetDeclaredSymbol(node);
		if (symbol == null) return;

		var newNode = CreateNode(symbol, node.Span);
		_graph.AddNode(newNode);

		var previousContext = _contextNode;
		_contextNode = newNode;

		foreach (var parameterSyntax in node.ParameterList.Parameters)
		{
			var paramSymbol = _semanticModel.GetDeclaredSymbol(parameterSyntax);
			if (paramSymbol?.Type != null)
			{
				var typeSpan = parameterSyntax.Type?.Span ?? parameterSyntax.Span;
				AddDependency(paramSymbol.Type, LinkType.UsesType, typeSpan);
			}
		}

		base.VisitMethodDeclaration(node);
		_contextNode = previousContext;
	}

	// --- Helpers Otimizados ---

	private void AddDependency(ITypeSymbol? type, LinkType linkType, Microsoft.CodeAnalysis.Text.TextSpan span)
	{
		if (type == null || IsSystemType(type)) return;

		if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
		{
			foreach (var arg in namedType.TypeArguments)
				AddDependency(arg, linkType, span);
		}

		AddLink(GetId(type), linkType, span);
	}

	// OTIMIZAÇÃO #4: Recebe int targetId
	private void AddLink(int targetId, LinkType type, Microsoft.CodeAnalysis.Text.TextSpan span)
	{
		if (targetId == 0) return;
		if (targetId == _contextNode!.Id) return; // Evita auto-referência

		// Cria a struct SymbolLink (alocação na stack/inline no HashSet)
		_contextNode.OutgoingLinks.Add(new SymbolLink(targetId, type, span.Start, span.Length));
	}

	private SymbolNode CreateNode(ISymbol symbol, Microsoft.CodeAnalysis.Text.TextSpan span)
	{
		// OTIMIZAÇÃO #1 e #4: 
		// - Id é int
		// - FileId é int (previamente calculado no construtor)
		return new SymbolNode(GetId(symbol), _fileId)
		{
			Name = symbol.Name,
			Type = MapType(symbol.Kind),
			// FilePath = ... (REMOVIDO, agora usamos FileId)
			StartPosition = span.Start,
			Length = span.Length
		};
	}

	// OTIMIZAÇÃO #4: Retorna int mapeado no Grafo
	private int GetId(ISymbol symbol)
	{
		if (symbol == null) return 0;

		// Gera a string única do Roslyn (custoso, mas necessário para a chave)
		string uniqueString = symbol.OriginalDefinition.ToDisplayString(_idFormat);

		// Pede ao grafo o ID Inteiro correspondente a essa string
		return _graph.GetOrAddNodeId(uniqueString);
	}

	private bool IsSystemType(ITypeSymbol type)
	{
		if (type.SpecialType != SpecialType.None) return true;
		return type.ContainingNamespace?.Name == "System" ||
			   (type.ContainingNamespace?.ToDisplayString().StartsWith("System") ?? false);
	}

	private SymbolType MapType(SymbolKind kind) => kind switch
	{
		SymbolKind.NamedType => SymbolType.Class,
		SymbolKind.Method => SymbolType.Method,
		SymbolKind.Property => SymbolType.Property,
		SymbolKind.Field => SymbolType.Field,
		_ => SymbolType.Class
	};
}