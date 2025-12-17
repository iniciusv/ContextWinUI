using ColorCode.Styling;
using ContextWinUI.Helpers;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;

namespace ContextWinUI.Services;

public class RegexHighlightService
{
	public struct HighlightSpan
	{
		public int Start;
		public int Length;
		public Color Color;
	}

	public async Task<List<HighlightSpan>> CalculateHighlightsAsync(string text, string extension, StyleDictionary theme)
	{
		return await Task.Run(() =>
		{
			var spans = new List<HighlightSpan>();
			if (string.IsNullOrEmpty(text) || theme == null) return spans;

			ColorCode.ILanguage language = extension.ToLower() switch
			{
				".js" or ".jsx" or ".ts" or ".tsx" or ".vue" => BetterJavascript.Language,
				".xml" or ".xaml" or ".csproj" => ColorCode.Languages.Xml,
				".html" or ".htm" => ColorCode.Languages.Html,
				".css" or ".scss" => ColorCode.Languages.Css,
				".sql" => ColorCode.Languages.Sql,
				".json" => ColorCode.Languages.JavaScript,
				_ => null
			};

			if (language == null) return spans;

			foreach (var rule in language.Rules)
			{
				try
				{
					var matches = Regex.Matches(text, rule.Regex, RegexOptions.Multiline);
					foreach (Match match in matches)
					{
						foreach (var scopeIndex in rule.Captures.Keys)
						{
							if (scopeIndex < match.Groups.Count)
							{
								var group = match.Groups[scopeIndex];
								if (group.Success && group.Length > 0)
								{
									var scopeName = rule.Captures[scopeIndex];
									if (theme.Contains(scopeName))
									{
										var style = theme[scopeName];
										var color = ThemeHelper.GetColorFromHex(style.Foreground);

										spans.Add(new HighlightSpan
										{
											Start = group.Index,
											Length = group.Length,
											Color = color
										});
									}
								}
							}
						}
					}
				}
				catch { }
			}
			return spans;
		});
	}

	public void ApplyHighlights(RichEditBox editor, List<HighlightSpan> spans)
	{
		if (spans == null) return;

		// Congela a tela para evitar piscar
		editor.Document.BatchDisplayUpdates();

		var document = editor.Document;

		// 1. OBTEM O TAMANHO TOTAL
		document.GetText(TextGetOptions.None, out string fullText);
		int totalLength = fullText.Length;

		var defaultColor = ThemeHelper.IsDarkTheme()
			? Color.FromArgb(255, 220, 220, 220) // Cinza claro
			: Colors.Black;

		var fullRange = document.GetRange(0, totalLength);
		fullRange.CharacterFormat.ForegroundColor = defaultColor;

		// 3. APLICA OS SPANS
		foreach (var span in spans)
		{
			try
			{
				// Verifica limites
				if (span.Start + span.Length <= totalLength)
				{
					var range = document.GetRange(span.Start, span.Start + span.Length);
					range.CharacterFormat.ForegroundColor = span.Color;
				}
			}
			catch { }
		}

		// Libera a atualização
		editor.Document.ApplyDisplayUpdates();
	}
}