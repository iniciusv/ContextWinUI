// ARQUIVO: DiffHighlighterStrategy.cs
using ContextWinUI.Features.GraphView;
using ContextWinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.UI;

namespace ContextWinUI.Features.GraphView;

public class DiffHighlighterStrategy : IHighlighterStrategy
{
	private readonly List<TokenChange> _changes;
	private const string FontFamilyName = "Consolas, Courier New, Monospace";
	private const double FontSizeValue = 14;

	public DiffHighlighterStrategy(List<TokenChange> changes)
	{
		_changes = changes;
	}

	public void ApplyHighlighting(RichTextBlock richTextBlock, string content, string contextParam)
	{
		richTextBlock.Blocks.Clear();
		if (string.IsNullOrEmpty(content)) return;

		// Criamos um mapa para saber qual ChangeType aplicar a cada caractere do texto
		ChangeType?[] diffMap = new ChangeType?[content.Length];

		foreach (var change in _changes)
		{
			// Usamos o NewToken para o snippet ou o OriginalToken se for remoção
			var node = change.NewToken ?? change.OriginalToken;
			if (node == null) continue;

			// Preenchemos o mapa no intervalo do token
			int start = node.StartPosition;
			int end = Math.Min(start + node.Length, content.Length);

			for (int i = start; i < end; i++)
			{
				if (i >= 0) diffMap[i] = change.ChangeType;
			}
		}

		int currentIdx = 0;
		int length = content.Length;

		while (currentIdx < length)
		{
			int lineEnd = content.IndexOf('\n', currentIdx);
			if (lineEnd == -1) lineEnd = length;

			var paragraph = new Paragraph();
			int i = currentIdx;

			while (i < lineEnd)
			{
				int runStart = i;
				ChangeType? currentType = diffMap[i];

				// Agrupa caracteres com o mesmo tipo de alteração para otimizar a renderização
				while (i < lineEnd && diffMap[i] == currentType)
				{
					i++;
				}

				string textSegment = content.Substring(runStart, i - runStart);

				// Remove caracteres de controle de linha para evitar quebras estranhas no RichTextBlock
				textSegment = textSegment.Replace("\r", "");

				if (currentType == null || currentType == ChangeType.Unchanged)
				{
					// Texto normal (preserva formatação original)
					paragraph.Inlines.Add(new Run
					{
						Text = textSegment,
						FontSize = FontSizeValue,
						Foreground = new SolidColorBrush(GetDefaultForegroundColor())
					});
				}
				else
				{
					// Texto com marcação de Diff
					var border = new Border
					{
						Background = new SolidColorBrush(GetBackgroundColor(currentType.Value)),
						Padding = new Thickness(0),
						CornerRadius = new CornerRadius(2),
						Child = new TextBlock
						{
							Text = textSegment,
							FontSize = FontSizeValue,
							FontFamily = new FontFamily(FontFamilyName),
							Foreground = new SolidColorBrush(GetDefaultForegroundColor())
						}
					};
					paragraph.Inlines.Add(new InlineUIContainer { Child = border });
				}
			}

			richTextBlock.Blocks.Add(paragraph);
			currentIdx = lineEnd + 1;
		}
	}

	private Color GetBackgroundColor(ChangeType type) => type switch
	{
		ChangeType.Inserted => Color.FromArgb(100, 39, 174, 96),  // Verde
		ChangeType.Removed => Color.FromArgb(100, 192, 57, 43),   // Vermelho
		ChangeType.Modified => Color.FromArgb(100, 241, 196, 15), // Amarelo
		_ => Colors.Transparent
	};

	private Color GetDefaultForegroundColor() =>
		ContextWinUI.Helpers.ThemeHelper.IsDarkTheme() ? Colors.White : Colors.Black;
}