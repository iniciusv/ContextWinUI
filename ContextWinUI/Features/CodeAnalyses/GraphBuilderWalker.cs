using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;


namespace ContextWinUI.Features.CodeAnalyses;

using ContextWinUI.Core.Models;
using System.IO;

public class GraphBuilderWalker : CSharpSyntaxWalker
{
	private readonly DependencyGraph _graph;
	private readonly SemanticModel _semanticModel;
	private readonly string _filePath;
	private readonly string _normalizedPath;
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
		_normalizedPath = Path.GetFullPath(filePath);
	}

	// 1. Captura Declaração de Métodos
	public override void VisitClassDeclaration(ClassDeclarationSyntax node)
	{
		var symbol = _semanticModel.GetDeclaredSymbol(node);
		if (symbol != null)
		{
			var classNode = CreateNode(symbol, node.Span);
			_graph.AddNode(classNode);

			var previousContext = _contextNode;
			_contextNode = classNode; // Define contexto para capturar links da classe

			if (symbol.BaseType != null && symbol.BaseType.SpecialType == SpecialType.None)
			{
				// Tenta pegar a localização do nome da classe base no código
				var baseTypeSyntax = node.BaseList?.Types.FirstOrDefault();
				var span = baseTypeSyntax?.Span ?? node.Identifier.Span;

				AddLink(GetId(symbol.BaseType), LinkType.Inherits, span);
			}

			foreach (var iface in symbol.Interfaces)
			{
				var ifaceId = GetId(iface);
				// Aqui seria ideal achar o SyntaxNode da interface específica no BaseList para ter o Span exato
				// Por simplificação, usaremos o Span da classe ou Identifier se não acharmos
				AddLink(ifaceId, LinkType.Implements, node.Identifier.Span);

				_graph.InterfaceImplementations.AddOrUpdate(
					ifaceId,
					new List<string> { classNode.Id },
					(k, v) => { lock (v) { v.Add(classNode.Id); return v; } });
			}

			base.VisitClassDeclaration(node); // Visita filhos
			_contextNode = previousContext;   // Restaura contexto
		}
	}

	// 2. Atualizar VisitInvocationExpression (Chamadas de Método)
	public override void VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		if (_contextNode == null) return;

		var symbolInfo = _semanticModel.GetSymbolInfo(node);
		var symbol = symbolInfo.Symbol as IMethodSymbol;

		if (symbol != null)
		{
			var definition = symbol.OriginalDefinition;
			// Captura o Span da expressão inteira ou apenas do identificador do método
			var locationSpan = node.Expression.Span;
			AddLink(GetId(definition), LinkType.Calls, locationSpan);
		}

		base.VisitInvocationExpression(node);
	}

	// 3. Atualizar VisitIdentifierName (Acessos a Propriedades/Campos e Uso de Tipos)
	public override void VisitIdentifierName(IdentifierNameSyntax node)
	{
		if (_contextNode == null) return;

		var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
		if (symbol == null) return;

		// Captura a posição exata do identificador no código
		var span = node.Span;

		if (symbol is IPropertySymbol || symbol is IFieldSymbol)
		{
			AddLink(GetId(symbol), LinkType.Accesses, span);

			var typeSymbol = (symbol as IPropertySymbol)?.Type ?? (symbol as IFieldSymbol)?.Type;
			// Para dependência de tipo implícito no acesso, podemos usar o mesmo span ou ignorar
			// AddDependency(typeSymbol, LinkType.UsesType, span); 
		}

		if (symbol is INamedTypeSymbol typeUsed && !IsSystemType(typeUsed))
		{
			AddLink(GetId(typeUsed), LinkType.UsesType, span);
		}

		base.VisitIdentifierName(node);
	}

	// 4. Atualizar VisitMethodDeclaration (Parâmetros)
	public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
	{
		var symbol = _semanticModel.GetDeclaredSymbol(node);
		if (symbol == null) return;

		var newNode = CreateNode(symbol, node.Span);
		_graph.AddNode(newNode);

		var previousContext = _contextNode;
		_contextNode = newNode;

		// Itera sobre os parâmetros para achar dependências de tipo
		foreach (var parameterSyntax in node.ParameterList.Parameters)
		{
			var paramSymbol = _semanticModel.GetDeclaredSymbol(parameterSyntax);
			if (paramSymbol?.Type != null)
			{
				// Usa o Span do TIPO do parâmetro, não o parâmetro inteiro
				var typeSpan = parameterSyntax.Type?.Span ?? parameterSyntax.Span;
				AddDependency(paramSymbol.Type, LinkType.UsesType, typeSpan);
			}
		}

		base.VisitMethodDeclaration(node);
		_contextNode = previousContext;
	}

	// 5. Métodos Auxiliares Atualizados
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

	// A MUDANÇA CRUCIAL AQUI: Receber TextSpan
	private void AddLink(string targetId, LinkType type, Microsoft.CodeAnalysis.Text.TextSpan span)
	{
		if (string.IsNullOrEmpty(targetId)) return;
		if (targetId == _contextNode!.Id) return; // Evita auto-referência

		// Agora criamos o Link COM a posição exata
		_contextNode.OutgoingLinks.Add(new SymbolLink(targetId, type, span.Start, span.Length));
	}
	private SymbolNode CreateNode(ISymbol symbol, Microsoft.CodeAnalysis.Text.TextSpan span)
	{
		return new SymbolNode
		{
			Id = GetId(symbol),
			Name = symbol.Name,
			Type = MapType(symbol.Kind),
			FilePath = _normalizedPath,
			StartPosition = span.Start,
			Length = span.Length
		};
	}

	private string GetId(ISymbol symbol)
	{
		if (symbol == null) return string.Empty;
		return symbol.OriginalDefinition.ToDisplayString(_idFormat);
	}

	private bool IsSystemType(ITypeSymbol type)
	{
		if (type.SpecialType != SpecialType.None) return true;

		// Fallback para namespace
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