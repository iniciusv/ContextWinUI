using ContextWinUI.Core.Models;
using ContextWinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.UI;

namespace ContextWinUI.Features.GraphView
{
	public class LayeredHighlighterStrategy : IHighlighterStrategy
	{
		private readonly List<SymbolNode> _scopes;
		private readonly List<SymbolNode> _tokens;
		private readonly bool _showScopes;
		private readonly bool _showTokens;

		// Fonte padronizada para garantir alinhamento entre Runs e TextBlocks
		private const string FontFamilyName = "Consolas, Courier New, Monospace";
		private const double FontSizeValue = 14;

		public LayeredHighlighterStrategy(List<SymbolNode> scopes, List<SymbolNode> tokens, bool showScopes, bool showTokens)
		{
			_scopes = scopes;
			_tokens = tokens;
			_showScopes = showScopes;
			_showTokens = showTokens;
		}

		public void ApplyHighlighting(RichTextBlock richTextBlock, string content, string contextParam)
		{
			richTextBlock.Blocks.Clear();
			if (string.IsNullOrEmpty(content)) return;

			int length = content.Length;

			// 1. Preparação dos Mapas (Mesma lógica anterior)
			SymbolNode?[] bgNodeMap = new SymbolNode?[length];
			SymbolNode?[] fgNodeMap = new SymbolNode?[length];

			if (_showScopes)
			{
				// Ordena por tamanho para que escopos menores fiquem "por cima" no mapa
				foreach (var scope in _scopes.OrderByDescending(s => s.Length))
					FillMap(bgNodeMap, scope.StartPosition, scope.Length, scope, length);
			}

			if (_showTokens)
			{
				foreach (var token in _tokens)
					FillMap(fgNodeMap, token.StartPosition, token.Length, token, length);
			}

			// 2. Renderização Linha a Linha
			int currentIdx = 0;

			while (currentIdx < length)
			{
				// Encontrar fim da linha visual
				int lineEnd = content.IndexOf('\n', currentIdx);
				if (lineEnd == -1) lineEnd = length;

				var paragraph = new Paragraph();
				int visualLineEnd = lineEnd;
				// Ignora o \r se existir
				if (visualLineEnd > currentIdx && content[visualLineEnd - 1] == '\r') visualLineEnd--;

				int i = currentIdx;
				while (i < visualLineEnd)
				{
					int start = i;
					var currentBgNode = bgNodeMap[i];
					var currentFgNode = fgNodeMap[i];

					// Agrupa caracteres enquanto o estilo (Background e Foreground) for o mesmo
					while (i < visualLineEnd && bgNodeMap[i] == currentBgNode && fgNodeMap[i] == currentFgNode)
					{
						i++;
					}

					string textSegment = content.Substring(start, i - start);

					// --- CORREÇÃO DE ESPAÇOS ---
					if (string.IsNullOrWhiteSpace(textSegment))
					{
						// Espaços são sempre Runs simples para manter o fluxo
						paragraph.Inlines.Add(new Run { Text = textSegment, FontSize = FontSizeValue });
						continue;
					}

					// Define as cores
					var fgColor = currentFgNode != null
						? GetTokenColor(currentFgNode.Type)
						: (ContextWinUI.Helpers.ThemeHelper.IsDarkTheme() ? Colors.White : Colors.Black);

					var bgColor = currentBgNode != null
						? GetScopeColor(currentBgNode.Type)
						: Colors.Transparent;

					bool hasBackground = currentBgNode != null;

					// --- LÓGICA DE RENDERIZAÇÃO OTIMIZADA ---

					if (hasBackground)
					{
						// CASO A: Tem Fundo (Escopo) -> Usa Border + TextBlock (InlineUIContainer)

						var textBlock = new TextBlock
						{
							Text = textSegment,
							Foreground = new SolidColorBrush(fgColor),
							FontFamily = new FontFamily(FontFamilyName),
							FontSize = FontSizeValue,

							// --- ADICIONE ISTO ---
							LineHeight = 20, // O mesmo valor do XAML
							LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
							// ---------------------

							Margin = new Thickness(0),
							Padding = new Thickness(0),
							VerticalAlignment = VerticalAlignment.Center
						};

						var border = new Border
						{
							Background = new SolidColorBrush(bgColor),
							Child = textBlock,
							Padding = new Thickness(0),
							Margin = new Thickness(0),
							VerticalAlignment = VerticalAlignment.Center,
							CornerRadius = new CornerRadius(2) // Opcional: Leve arredondamento no escopo
						};

						// Adiciona Tooltip com a hierarquia
						ApplyTooltip(border, currentFgNode, currentBgNode);

						paragraph.Inlines.Add(new InlineUIContainer { Child = border });
					}
					else
					{
						// CASO B: Apenas Texto/Sintaxe -> Usa Run (Nativo e Leve)
						// Isso garante que a altura da linha seja baseada na fonte, idêntica ao modo "sem highlight"
						var run = new Run
						{
							Text = textSegment,
							Foreground = new SolidColorBrush(fgColor),
							FontSize = FontSizeValue
						};

						// Nota: Tooltips não funcionam diretamente em Runs no WinUI 3 facilmente,
						// mas como não tem escopo (fundo), geralmente a tooltip é menos crítica aqui.

						paragraph.Inlines.Add(run);
					}
				}

				richTextBlock.Blocks.Add(paragraph);
				currentIdx = lineEnd + 1;
			}
		}

		private void ApplyTooltip(DependencyObject element, SymbolNode? fgNode, SymbolNode? bgNode)
		{
			var activeNode = fgNode ?? bgNode; // Prefere mostrar info do token, senão do escopo
			if (activeNode != null || bgNode != null)
			{
				string tooltipText = BuildTooltipText(activeNode, fgNode, bgNode);
				ToolTipService.SetToolTip(element, tooltipText);
			}
		}

		private string BuildTooltipText(SymbolNode? primaryNode, SymbolNode? fgNode, SymbolNode? bgNode)
		{
			var sb = new StringBuilder();

			if (fgNode != null) sb.AppendLine($"Sintaxe: {fgNode.Type}");
			if (bgNode != null) sb.AppendLine($"Escopo: {bgNode.Name} ({bgNode.Type})");

			var currentNode = bgNode ?? fgNode?.Parent;
			if (currentNode != null)
			{
				sb.AppendLine("----------------");
				sb.AppendLine("Hierarquia:");
				var stack = new Stack<string>();

				// Reconstrói a pilha de pais
				while (currentNode != null)
				{
					stack.Push($"{currentNode.Type}: {currentNode.Name}");
					currentNode = currentNode.Parent;
				}

				string indent = "";
				while (stack.Count > 0)
				{
					sb.AppendLine($"{indent}↳ {stack.Pop()}");
					indent += "  ";
				}
			}
			return sb.ToString().Trim();
		}

		private void FillMap(SymbolNode?[] map, int start, int len, SymbolNode node, int maxLen)
		{
			int end = start + len;
			if (start < 0) start = 0;
			if (end > maxLen) end = maxLen;
			for (int k = start; k < end; k++) map[k] = node;
		}

		private Color GetScopeColor(SymbolType type)
		{
			// Transparência baixa para permitir leitura do texto
			byte alpha = 40;
			return type switch
			{
				SymbolType.Class => Color.FromArgb(alpha, 0, 120, 215),        // Azul
				SymbolType.Method => Color.FromArgb(alpha, 255, 215, 0),       // Ouro
				SymbolType.ControlFlow => Color.FromArgb(alpha, 148, 0, 211),  // Roxo
				SymbolType.Interface => Color.FromArgb(alpha, 46, 204, 113),   // Verde
				//SymbolType.Try => Color.FromArgb(alpha, 231, 76, 60),          // Vermelho
				_ => Colors.Transparent
			};
		}

		private Color GetTokenColor(SymbolType type)
		{
			bool isDark = ContextWinUI.Helpers.ThemeHelper.IsDarkTheme();

			// Cores inspiradas no VS Code
			return type switch
			{
				SymbolType.Keyword => isDark ? Color.FromArgb(255, 86, 156, 214) : Colors.Blue,
				SymbolType.LocalVariable => isDark ? Color.FromArgb(255, 156, 220, 254) : Color.FromArgb(255, 31, 55, 127),
				SymbolType.StringLiteral => isDark ? Color.FromArgb(255, 206, 145, 120) : Color.FromArgb(255, 163, 21, 21),
				SymbolType.NumericLiteral => isDark ? Color.FromArgb(255, 181, 206, 168) : Color.FromArgb(255, 9, 136, 90),
				SymbolType.Parameter => isDark ? Color.FromArgb(255, 156, 220, 254) : Colors.DarkSlateGray,
				_ => isDark ? Colors.White : Colors.Black
			};
		}
	}
}