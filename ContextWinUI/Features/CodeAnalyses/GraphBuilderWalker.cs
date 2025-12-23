using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;


namespace ContextWinUI.Features.CodeAnalyses;

using ContextWinUI.Core.Models;


public class GraphBuilderWalker : CSharpSyntaxWalker
{
	private readonly DependencyGraph _graph;
	private readonly SemanticModel _semanticModel;
	private readonly string _filePath;

	private SymbolNode? _contextNode; 

	public GraphBuilderWalker(DependencyGraph graph, SemanticModel semanticModel, string filePath)
	{
		_graph = graph;
		_semanticModel = semanticModel;
		_filePath = filePath;
	}

	// 1. Captura Declaração de Métodos
	public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
	{
		var symbol = _semanticModel.GetDeclaredSymbol(node);
		if (symbol == null) return;

		var newNode = CreateNode(symbol, node.Span);
		_graph.AddNode(newNode);

		// Define contexto e visita o corpo
		var previousContext = _contextNode;
		_contextNode = newNode;

		// Analisa parâmetros para detectar dependências de Tipos Complexos
		foreach (var param in symbol.Parameters)
		{
			AddDependency(param.Type, LinkType.UsesType);
		}

		base.VisitMethodDeclaration(node);
		_contextNode = previousContext;
	}

	// 2. Captura Declaração de Classes (para propriedades/campos)
	public override void VisitClassDeclaration(ClassDeclarationSyntax node)
	{
		var symbol = _semanticModel.GetDeclaredSymbol(node);
		if (symbol != null)
		{
			var classNode = CreateNode(symbol, node.Span);
			_graph.AddNode(classNode);

			// Detecta Herança e Interfaces
			if (symbol.BaseType != null && symbol.BaseType.SpecialType == SpecialType.None)
			{
				// Link de Herança
				classNode.OutgoingLinks.Add(new SymbolLink(GetId(symbol.BaseType), LinkType.Inherits));
			}

			foreach (var iface in symbol.Interfaces)
			{
				// Indexação reversa para Interfaces
				var ifaceId = GetId(iface);
				classNode.OutgoingLinks.Add(new SymbolLink(ifaceId, LinkType.Implements));

				_graph.InterfaceImplementations.AddOrUpdate(
					ifaceId,
					new List<string> { classNode.Id },
					(k, v) => { lock (v) { v.Add(classNode.Id); return v; } });
			}
		}
		base.VisitClassDeclaration(node);
	}

	// 3. Captura Chamadas e Acessos (O núcleo da recursão)
	public override void VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		if (_contextNode == null) return;

		var symbolInfo = _semanticModel.GetSymbolInfo(node);
		var symbol = symbolInfo.Symbol as IMethodSymbol;

		if (symbol != null)
		{
			// TRUQUE PARA GENERICS: Se chamar Method<T>(), queremos o link para Method<T> definition
			var definition = symbol.OriginalDefinition;
			AddLink(GetId(definition), LinkType.Calls);
		}

		base.VisitInvocationExpression(node);
	}

	public override void VisitIdentifierName(IdentifierNameSyntax node)
	{
		if (_contextNode == null) return;

		var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
		if (symbol == null) return;

		// Detecta uso de Propriedades e Campos
		if (symbol is IPropertySymbol || symbol is IFieldSymbol)
		{
			// Ignora se for membro da própria classe sendo analisada (opcional, depende do nível de ruído desejado)
			AddLink(GetId(symbol), LinkType.Accesses);

			// Adiciona dependência ao TIPO da propriedade (ex: public User CurrentUser { get; })
			// Isso captura "Classes Complexas"
			var typeSymbol = (symbol as IPropertySymbol)?.Type ?? (symbol as IFieldSymbol)?.Type;
			AddDependency(typeSymbol, LinkType.UsesType);
		}

		// Detecta instanciação de objetos: new Customer()
		if (symbol is INamedTypeSymbol typeUsed && !IsSystemType(typeUsed))
		{
			AddLink(GetId(typeUsed), LinkType.UsesType);
		}

		base.VisitIdentifierName(node);
	}

	// --- Helpers ---

	private void AddDependency(ITypeSymbol? type, LinkType linkType)
	{
		if (type == null || IsSystemType(type)) return;

		// Se for Lista<Customer>, queremos Customer
		if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
		{
			foreach (var arg in namedType.TypeArguments) AddDependency(arg, linkType);
		}

		AddLink(GetId(type), linkType);
	}

	private void AddLink(string targetId, LinkType type)
	{
		if (string.IsNullOrEmpty(targetId)) return;
		// Evita auto-referência
		if (targetId == _contextNode.Id) return;

		_contextNode.OutgoingLinks.Add(new SymbolLink(targetId, type));
	}

	private SymbolNode CreateNode(ISymbol symbol, Microsoft.CodeAnalysis.Text.TextSpan span)
	{
		return new SymbolNode
		{
			Id = GetId(symbol),
			Name = symbol.Name,
			Type = MapType(symbol.Kind),
			FilePath = _filePath,
			StartPosition = span.Start,
			Length = span.Length
		};
	}

	private string GetId(ISymbol symbol) => symbol.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

	private bool IsSystemType(ITypeSymbol type) => type.ContainingNamespace?.ToString().StartsWith("System") ?? false;

	private SymbolType MapType(SymbolKind kind) => kind switch
	{
		SymbolKind.NamedType => SymbolType.Class,
		SymbolKind.Method => SymbolType.Method,
		SymbolKind.Property => SymbolType.Property,
		SymbolKind.Field => SymbolType.Field,
		_ => SymbolType.Class
	};
}