// ARQUIVO: GranularGraphBuilderWalker.cs
using ContextWinUI.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

public class GranularGraphBuilderWalker : CSharpSyntaxWalker
{
	// Lista local de nós apenas para este arquivo
	public List<SymbolNode> LocalNodes { get; } = new();
	private readonly string _filePath;

	public GranularGraphBuilderWalker(string filePath)
	{
		_filePath = filePath;
	}

	// Captura declarações de variáveis (var x = 1;)
	public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
	{
		CreateNode(node.Identifier.Text, SymbolType.LocalVariable, node.Span);
		base.VisitVariableDeclarator(node);
	}

	// Captura Parâmetros de métodos
	public override void VisitParameter(ParameterSyntax node)
	{
		CreateNode(node.Identifier.Text, SymbolType.Parameter, node.Span);
		base.VisitParameter(node);
	}

	// Captura Fluxo de Controle (If, For, While, Switch)
	public override void VisitIfStatement(IfStatementSyntax node)
	{
		// Pegamos apenas a palavra "if" e a condição para o highlight, 
		// ou o bloco inteiro se quiser pintar o fundo.
		// Vamos pegar o span do 'if' até o fechar parenteses da condição para ficar visualmente limpo.
		var length = node.Statement.SpanStart - node.SpanStart;
		CreateNode("if", SymbolType.ControlFlow, new Microsoft.CodeAnalysis.Text.TextSpan(node.SpanStart, length));
		base.VisitIfStatement(node);
	}

	public override void VisitForEachStatement(ForEachStatementSyntax node)
	{
		var length = node.Statement.SpanStart - node.SpanStart;
		CreateNode("foreach", SymbolType.ControlFlow, new Microsoft.CodeAnalysis.Text.TextSpan(node.SpanStart, length));
		base.VisitForEachStatement(node);
	}

	public override void VisitReturnStatement(ReturnStatementSyntax node)
	{
		CreateNode("return", SymbolType.ControlFlow, node.Span);
		base.VisitReturnStatement(node);
	}

	// Métodos e Classes também são capturados para manter a hierarquia visual
	public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
	{
		CreateNode(node.Identifier.Text, SymbolType.Method, node.Span);
		base.VisitMethodDeclaration(node);
	}

	public override void VisitClassDeclaration(ClassDeclarationSyntax node)
	{
		CreateNode(node.Identifier.Text, SymbolType.Class, node.Span);
		base.VisitClassDeclaration(node);
	}

	private void CreateNode(string name, SymbolType type, Microsoft.CodeAnalysis.Text.TextSpan span)
	{
		LocalNodes.Add(new SymbolNode
		{
			Id = System.Guid.NewGuid().ToString(), // ID temporário, não precisa ser linkável globalmente
			Name = name,
			Type = type,
			FilePath = _filePath,
			StartPosition = span.Start,
			Length = span.Length
		});
	}
}