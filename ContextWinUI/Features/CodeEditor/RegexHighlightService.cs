// ARQUIVO: Services/RegexHighlightService.cs
using ColorCode.Styling;
using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Helpers;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;

namespace ContextWinUI.Services
{
	public class RegexHighlightService
	{
		// Estrutura interna simples para definir regras
		private class SimpleRule
		{
			public string Regex { get; set; }
			public string Scope { get; set; }
			public SimpleRule(string regex, string scope) { Regex = regex; Scope = scope; }
		}

		public async Task<List<HighlightSpan>> CalculateHighlightsAsync(string text, string extension, StyleDictionary theme)
		{
			return await Task.Run(() =>
			{
				var spans = new List<HighlightSpan>();
				if (string.IsNullOrEmpty(text) || theme == null) return spans;

				var rules = GetRulesForExtension(extension);

				// Processa cada regra
				foreach (var rule in rules)
				{
					try
					{
						var matches = Regex.Matches(text, rule.Regex, RegexOptions.Multiline);
						foreach (Match match in matches)
						{
							if (theme.Contains(rule.Scope))
							{
								var style = theme[rule.Scope];
								var color = ThemeHelper.GetColorFromHex(style.Foreground);
								spans.Add(new HighlightSpan
								{
									Start = match.Index,
									Length = match.Length,
									Color = color
								});
							}
						}
					}
					catch { }
				}
				return spans;
			});
		}

		private List<SimpleRule> GetRulesForExtension(string ext)
		{
			var rules = new List<SimpleRule>();

			// Definições comuns
			string stringPattern = "\"[^\"\\\\\\r\\n]*(?:\\\\.[^\"\\\\\\r\\n]*)*\"|'[^'\\\\\\r\\n]*(?:\\\\.[^'\\\\\\r\\n]*)*'";
			string commentPattern = "//.*|/\\*[\\s\\S]*?\\*/";
			string xmlTagPattern = "</?\\w+((\\s+\\w+(\\s*=\\s*(?:\".*?\"|'.*?'|[^'\">\\s]+))?)+\\s*|\\s*)/?>";
			string xmlAttributePattern = "\\s+\\w+(?=\\=)";

			switch (ext.ToLower())
			{
				case ".js":
				case ".ts":
				case ".jsx":
				case ".tsx":
				case ".vue":
					// JavaScript / TypeScript
					rules.Add(new SimpleRule(commentPattern, "Comment"));
					rules.Add(new SimpleRule(stringPattern, "String"));
					rules.Add(new SimpleRule(@"\b(const|let|var|function|class|import|export|from|return|if|else|switch|case|break|new|this|async|await)\b", "Keyword"));
					rules.Add(new SimpleRule(@"\b\d+(\.\d+)?\b", "Number"));
					rules.Add(new SimpleRule(@"\b[A-Z]\w+\b", ThemeHelper.ClassScope));
					break;

				case ".razor":
				case ".html":
				case ".htm":
					// Razor / HTML (Mistura XML com C# básico)
					rules.Add(new SimpleRule("", "Comment"));
					rules.Add(new SimpleRule(stringPattern, "String"));
					rules.Add(new SimpleRule(xmlTagPattern, "Keyword")); // Tags como Keyword (Azul)
					rules.Add(new SimpleRule(xmlAttributePattern, ThemeHelper.AttributeScope));
					rules.Add(new SimpleRule(@"@\w+", "Control Keyword")); // Razor syntax (@code, @foreach)
					rules.Add(new SimpleRule(@"@\{[\s\S]*?\}", "Unknown")); // Bloco Razor (difícil parsear via regex, mas pode tentar)
					break;

				case ".xml":
				case ".xaml":
				case ".csproj":
				case ".config":
					// XML puro
					rules.Add(new SimpleRule("", "Comment"));
					rules.Add(new SimpleRule(stringPattern, "String"));
					rules.Add(new SimpleRule("</?[\\w:.]+", "Keyword")); // Tag name com namespace
					rules.Add(new SimpleRule("(?<=\\s)[\\w:.]+(?==)", ThemeHelper.AttributeScope)); // Atributo
					break;

				case ".json":
					rules.Add(new SimpleRule(@"""\w+""(?=\s*:)", "Keyword")); // Chaves
					rules.Add(new SimpleRule(@"(?<=:\s*)""[^""]*""", "String")); // Valores string
					rules.Add(new SimpleRule(@"\b(true|false|null)\b", "Control Keyword"));
					rules.Add(new SimpleRule(@"\b\d+(\.\d+)?\b", "Number"));
					break;

				case ".sql":
					rules.Add(new SimpleRule("--.*", "Comment"));
					rules.Add(new SimpleRule(stringPattern, "String"));
					rules.Add(new SimpleRule(@"\b(SELECT|FROM|WHERE|INSERT|UPDATE|DELETE|JOIN|LEFT|RIGHT|INNER|OUTER|ON|GROUP|BY|ORDER|HAVING|LIMIT|TOP|AND|OR|NOT|NULL|IS|IN)\b", "Keyword"));
					break;
			}

			return rules;
		}

		public void ApplyHighlights(RichEditBox editor, List<HighlightSpan> spans)
		{
			if (editor == null || spans == null) return;

			// ... (O código de ApplyHighlights permanece igual ao das respostas anteriores)
			// Lembre-se de resetar a cor base aqui também!
			editor.Document.BatchDisplayUpdates();
			editor.Document.GetText(TextGetOptions.None, out string text);
			var defaultColor = ThemeHelper.IsDarkTheme() ? Colors.White : Colors.Black;
			editor.Document.GetRange(0, text.Length).CharacterFormat.ForegroundColor = defaultColor;

			foreach (var span in spans)
			{
				try
				{
					int start = System.Math.Clamp(span.Start, 0, text.Length);
					int end = System.Math.Clamp(span.Start + span.Length, 0, text.Length);
					if (end > start) editor.Document.GetRange(start, end).CharacterFormat.ForegroundColor = span.Color;
				}
				catch { }
			}
			editor.Document.ApplyDisplayUpdates();
		}
	}
}