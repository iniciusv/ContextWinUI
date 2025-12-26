using ContextWinUI.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

// Ajuste o namespace conforme a estrutura do seu projeto
namespace ContextWinUI.Features.GraphView
{
	/// <summary>
	/// Navega pela árvore sintática (Roslyn) para construir uma hierarquia lógica de escopos.
	/// Diferente de um walker comum, este mantém estado de Pai/Filho.
	/// </summary>
	public class ScopeGraphBuilderWalker : CSharpSyntaxWalker
	{
		// Lista plana para facilitar o acesso linear na hora de pintar (Highlight)
		public List<SymbolNode> AllNodes { get; } = new();

		// Lista apenas dos nós raiz (sem pai), caso queira navegar na árvore partindo do topo
		public List<SymbolNode> RootNodes { get; } = new();

		private readonly string _filePath;

		// A PILHA: O topo é sempre o escopo onde estamos "dentro" neste momento da navegação.
		private readonly Stack<SymbolNode> _parentStack = new();

		public ScopeGraphBuilderWalker(string filePath)
		{
			_filePath = filePath;
		}

		// --- LÓGICA CORE DE HIERARQUIA ---

		private SymbolNode EnterScope(string name, SymbolType type, Microsoft.CodeAnalysis.Text.TextSpan span)
		{
			var node = new SymbolNode
			{
				Name = name,
				Type = type,
				FilePath = _filePath,
				StartPosition = span.Start,
				Length = span.Length
			};

			// 1. Vincula ao Pai (Se houver alguém na pilha)
			if (_parentStack.Count > 0)
			{
				var parent = _parentStack.Peek();
				node.Parent = parent;       // O Filho conhece o Pai
				parent.Children.Add(node);  // O Pai conhece o Filho
			}
			else
			{
				// Se a pilha está vazia, é um nó raiz (ex: Classe externa ou Namespace)
				RootNodes.Add(node);
			}

			// 2. Adiciona à lista plana global (usada pela estratégia de highlight)
			AllNodes.Add(node);

			// 3. Empilha este nó. Tudo o que for visitado a partir de agora será filho dele.
			_parentStack.Push(node);

			return node;
		}

		private void ExitScope()
		{
			// Ao terminar de visitar os filhos de um nó, removemos ele da pilha
			// para que o próximo elemento pertença ao pai anterior.
			if (_parentStack.Count > 0)
				_parentStack.Pop();
		}

		// --- VISITANTES DE ESTRUTURA ---

		public override void VisitClassDeclaration(ClassDeclarationSyntax node)
		{
			EnterScope(node.Identifier.Text, SymbolType.Class, node.Span);
			base.VisitClassDeclaration(node); // Visita o conteúdo da classe
			ExitScope();
		}

		public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
		{
			EnterScope(node.Identifier.Text, SymbolType.Interface, node.Span);
			base.VisitInterfaceDeclaration(node);
			ExitScope();
		}

		public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
		{
			EnterScope(node.Identifier.Text, SymbolType.Method, node.Span);
			base.VisitMethodDeclaration(node); // Visita parâmetros e corpo
			ExitScope();
		}

		public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
		{
			EnterScope(node.Identifier.Text, SymbolType.Constructor, node.Span);
			base.VisitConstructorDeclaration(node);
			ExitScope();
		}

		public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
		{
			// Propriedades também podem ter escopo (getters/setters com lógica)
			EnterScope(node.Identifier.Text, SymbolType.Property, node.Span);
			base.VisitPropertyDeclaration(node);
			ExitScope();
		}

		// --- VISITANTES DE FLUXO DE CONTROLE ---
		// Aqui capturamos blocos lógicos como IF, FOR, WHILE para dar cor ao fundo

		public override void VisitIfStatement(IfStatementSyntax node)
		{
			EnterScope("if", SymbolType.ControlFlow, node.Span);
			base.VisitIfStatement(node);
			ExitScope();
		}

		public override void VisitElseClause(ElseClauseSyntax node)
		{
			EnterScope("else", SymbolType.ControlFlow, node.Span);
			base.VisitElseClause(node);
			ExitScope();
		}

		public override void VisitForStatement(ForStatementSyntax node)
		{
			EnterScope("for", SymbolType.ControlFlow, node.Span);
			base.VisitForStatement(node);
			ExitScope();
		}

		public override void VisitForEachStatement(ForEachStatementSyntax node)
		{
			EnterScope("foreach", SymbolType.ControlFlow, node.Span);
			base.VisitForEachStatement(node);
			ExitScope();
		}

		public override void VisitWhileStatement(WhileStatementSyntax node)
		{
			EnterScope("while", SymbolType.ControlFlow, node.Span);
			base.VisitWhileStatement(node);
			ExitScope();
		}

		public override void VisitSwitchStatement(SwitchStatementSyntax node)
		{
			EnterScope("switch", SymbolType.ControlFlow, node.Span);
			base.VisitSwitchStatement(node);
			ExitScope();
		}

		public override void VisitTryStatement(TryStatementSyntax node)
		{
			EnterScope("try", SymbolType.ControlFlow, node.Span);
			base.VisitTryStatement(node);
			ExitScope();
		}

		public override void VisitCatchClause(CatchClauseSyntax node)
		{
			EnterScope("catch", SymbolType.ControlFlow, node.Span);
			base.VisitCatchClause(node);
			ExitScope();
		}

		public override void VisitFinallyClause(FinallyClauseSyntax node)
		{
			EnterScope("finally", SymbolType.ControlFlow, node.Span);
			base.VisitFinallyClause(node);
			ExitScope();
		}
	}
}