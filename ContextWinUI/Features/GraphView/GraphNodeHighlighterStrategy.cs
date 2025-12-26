// ARQUIVO: GraphNodeHighlighterStrategy.cs
using ContextWinUI.Core.Models;
using ContextWinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

public class GraphNodeHighlighterStrategy : IHighlighterStrategy
{
	private readonly DependencyGraph _graph;
	private readonly string _filePath;

	public GraphNodeHighlighterStrategy(DependencyGraph graph, string filePath)
	{
		_graph = graph;
		_filePath = filePath;
	}

	public void ApplyHighlighting(RichTextBlock richTextBlock, string content, string contextParam)
	{
		richTextBlock.Blocks.Clear();
		if (string.IsNullOrEmpty(content) || _graph == null) return;

		var paragraph = new Paragraph();

		// 1. Busca os nós relevantes para este arquivo
		var searchKey = System.IO.Path.GetFullPath(_filePath).ToLowerInvariant();
		List<SymbolNode> nodes = new();

		if (_graph.FileIndex.TryGetValue(searchKey, out var fileNodes))
		{
			lock (fileNodes)
			{
				nodes = fileNodes.ToList();
			}
		}

		// 2. Cria um mapa de caracteres para determinar a cor de cada posição
		// O array armazena o SymbolType associado àquela posição.
		// Inicializa com -1 (sem nó).
		int[] charMap = new int[content.Length];
		for (int i = 0; i < charMap.Length; i++) charMap[i] = -1;

		// Ordenamos por Tamanho Decrescente (Length), assim nós menores (ex: métodos dentro de classes)
		// sobrescrevem os maiores (classes), garantindo que o "inner scope" tenha prioridade visual.
		var sortedNodes = nodes.OrderByDescending(n => n.Length).ToList();

		foreach (var node in sortedNodes)
		{
			int start = node.StartPosition;
			int end = node.StartPosition + node.Length;

			// Proteção contra range fora do conteúdo atual (caso o arquivo tenha mudado sem reindexar)
			if (start < 0) start = 0;
			if (end > content.Length) end = content.Length;

			for (int i = start; i < end; i++)
			{
				charMap[i] = (int)node.Type;
			}
		}

		// 3. Constroi os Runs baseados na mudança de tipo no mapa
		int currentIndex = 0;
		while (currentIndex < content.Length)
		{
			int type = charMap[currentIndex];
			int runStart = currentIndex;

			// Avança até mudar o tipo
			while (currentIndex < content.Length && charMap[currentIndex] == type)
			{
				currentIndex++;
			}

			string textSegment = content.Substring(runStart, currentIndex - runStart);
			var run = new Run { Text = textSegment };

			// Aplica cor se houver um tipo associado (-1 é texto comum)
			if (type != -1)
			{
				run.Foreground = new SolidColorBrush(GetColorForSymbolType((SymbolType)type));
			}
			else
			{
				// Cor padrão do tema (será aplicada pelo controle pai ou tema)
				// Deixamos null ou definimos uma cor base suave se necessário.
				var defaultColor = ContextWinUI.Helpers.ThemeHelper.IsDarkTheme() ? Colors.LightGray : Colors.Black;
				run.Foreground = new SolidColorBrush(defaultColor);
			}

			paragraph.Inlines.Add(run);
		}

		richTextBlock.Blocks.Add(paragraph);
	}


	private Color GetColorForSymbolType(SymbolType type)
	{
		bool isDark = ContextWinUI.Helpers.ThemeHelper.IsDarkTheme();

		return type switch
		{
			// Níveis Superiores (Global)
			SymbolType.Class => Color.FromArgb(255, 78, 201, 176),       // Teal
			SymbolType.Interface => Color.FromArgb(255, 181, 206, 168),  // Light Green
			SymbolType.Method => Color.FromArgb(50, 220, 220, 170),      // Yellow (Com Alpha baixo para ser background se sobrepor)

			// Níveis Granulares (Local)
			SymbolType.LocalVariable => isDark ? Color.FromArgb(255, 156, 220, 254) : Colors.Blue, // Light Blue
			SymbolType.Parameter => isDark ? Color.FromArgb(255, 156, 220, 254) : Colors.DarkBlue,

			// Fluxo de Controle (Roxo/Magenta)
			SymbolType.ControlFlow => isDark ? Color.FromArgb(255, 197, 134, 192) : Colors.Purple,

			// Statements Gerais
			SymbolType.Statement => isDark ? Colors.LightGray : Colors.DarkGray,

			_ => isDark ? Colors.White : Colors.Black
		};
	}
}