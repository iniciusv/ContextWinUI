// ARQUIVO: LayeredHighlighterStrategy.cs
using ContextWinUI.Core.Models;
using ContextWinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

public class LayeredHighlighterStrategy : IHighlighterStrategy
{
	private readonly List<SymbolNode> _scopes;
	private readonly List<SymbolNode> _tokens;
	private readonly bool _showScopes;
	private readonly bool _showTokens;

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

		// AGORA MAPEAMOS O NÓ, NÃO SÓ A COR
		SymbolNode?[] bgNodeMap = new SymbolNode?[length];
		SymbolNode?[] fgNodeMap = new SymbolNode?[length];

		// 1. Preenche Mapa de Escopos (Se habilitado)
		if (_showScopes)
		{
			foreach (var scope in _scopes.OrderByDescending(s => s.Length))
			{
				FillMap(bgNodeMap, scope.StartPosition, scope.Length, scope, length);
			}
		}

		// 2. Preenche Mapa de Tokens (Se habilitado)
		if (_showTokens)
		{
			foreach (var token in _tokens)
			{
				FillMap(fgNodeMap, token.StartPosition, token.Length, token, length);
			}
		}

		// 3. Renderização
		int currentIdx = 0;

		while (currentIdx < length)
		{
			int lineEnd = content.IndexOf('\n', currentIdx);
			if (lineEnd == -1) lineEnd = length;

			var paragraph = new Paragraph();
			int visualLineEnd = lineEnd;
			if (visualLineEnd > currentIdx && content[visualLineEnd - 1] == '\r') visualLineEnd--;

			int i = currentIdx;
			while (i < visualLineEnd)
			{
				int start = i;
				var currentBgNode = bgNodeMap[i];
				var currentFgNode = fgNodeMap[i];

				// Agrupa caracteres iguais
				while (i < visualLineEnd &&
					   bgNodeMap[i] == currentBgNode &&
					   fgNodeMap[i] == currentFgNode)
				{
					i++;
				}

				string textSegment = content.Substring(start, i - start);

				// Determina Cores
				var fgColor = currentFgNode != null ? GetTokenColor(currentFgNode.Type)
					: (ContextWinUI.Helpers.ThemeHelper.IsDarkTheme() ? Colors.White : Colors.Black);

				var bgColor = currentBgNode != null ? GetScopeColor(currentBgNode.Type) : Colors.Transparent;

				// CRIAÇÃO DO ELEMENTO VISUAL

				// Se tem Background OU se queremos Tooltip no Token, usamos Container
				// Se for texto simples sem nada especial, usamos Run (performance)
				bool needsContainer = currentBgNode != null || currentFgNode != null;

				if (needsContainer)
				{
					var textBlock = new TextBlock
					{
						Text = textSegment,
						Foreground = new SolidColorBrush(fgColor),
						FontFamily = new FontFamily("Consolas, Courier New, Monospace"),
						FontSize = 14
					};

					// CORREÇÃO DOS ESPAÇOS: Se for espaço, preserva largura
					if (string.IsNullOrWhiteSpace(textSegment))
					{
						// TextBlock normalmente colapsa espaços vazios em containers.
						// Uma opção é usar Margin, mas isso é impreciso.
						// Outra é substituir por Non-Breaking Space, mas atrapalha o Copiar/Colar.
						// A melhor opção no WinUI é garantir que o TextBlock não colapse.
						// Vamos deixar o TextBlock padrão renderizar o espaço.
					}

					var border = new Border
					{
						Background = new SolidColorBrush(bgColor),
						Padding = new Thickness(0),
						Child = textBlock
					};

					// TOOLTIP (Painel Flutuante)
					// Mostra info do Token (mais específico) ou do Escopo
					var infoNode = currentFgNode ?? currentBgNode;
					if (infoNode != null)
					{
						string tooltipText = $"Tipo: {infoNode.Type}\nNome: {infoNode.Name}";
						if (currentBgNode != null && currentFgNode != null)
						{
							tooltipText = $"Token: {currentFgNode.Name} ({currentFgNode.Type})\nNo Escopo: {currentBgNode.Name} ({currentBgNode.Type})";
						}
						ToolTipService.SetToolTip(border, tooltipText);
					}

					paragraph.Inlines.Add(new InlineUIContainer { Child = border });
				}
				else
				{
					// Renderização leve para espaços fora de escopos ou texto comum
					paragraph.Inlines.Add(new Run
					{
						Text = textSegment,
						Foreground = new SolidColorBrush(fgColor)
					});
				}
			}

			richTextBlock.Blocks.Add(paragraph);
			currentIdx = lineEnd + 1;
		}
	}

	private void FillMap(SymbolNode?[] map, int start, int len, SymbolNode node, int maxLen)
	{
		int end = start + len;
		if (start < 0) start = 0;
		if (end > maxLen) end = maxLen;
		for (int k = start; k < end; k++) map[k] = node;
	}

	// Cores (Mantidas as mesmas)
	private Color GetScopeColor(SymbolType type)
	{
		byte alpha = 40;
		return type switch
		{
			SymbolType.Class => Color.FromArgb(alpha, 0, 120, 215),
			SymbolType.Method => Color.FromArgb(alpha, 255, 215, 0),
			SymbolType.ControlFlow => Color.FromArgb(alpha, 200, 50, 200),
			_ => Colors.Transparent
		};
	}

	private Color GetTokenColor(SymbolType type)
	{
		bool isDark = ContextWinUI.Helpers.ThemeHelper.IsDarkTheme();
		return type switch
		{
			SymbolType.Keyword => isDark ? Color.FromArgb(255, 86, 156, 214) : Colors.Blue,
			SymbolType.LocalVariable => isDark ? Color.FromArgb(255, 156, 220, 254) : Colors.LightBlue,
			SymbolType.Parameter => isDark ? Color.FromArgb(255, 156, 220, 254) : Colors.DarkBlue,
			SymbolType.StringLiteral => isDark ? Color.FromArgb(255, 206, 145, 120) : Colors.Brown,
			SymbolType.NumericLiteral => isDark ? Color.FromArgb(255, 181, 206, 168) : Colors.Green,
			_ => isDark ? Colors.White : Colors.Black
		};
	}
}