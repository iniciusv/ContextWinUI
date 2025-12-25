// ==================== ContextWinUI\Features\CodeEditor\SyntaxHighlightService.cs ====================

using ColorCode;
using ColorCode.Common;
using ColorCode.Styling;
using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;

namespace ContextWinUI.Services;

public class SyntaxHighlightService
{
	public void ApplySyntaxHighlighting(RichTextBlock richTextBlock, string content, string fileExtension)
	{
		richTextBlock.Blocks.Clear();

		if (string.IsNullOrEmpty(content)) return;

		try
		{
			var language = GetLanguageByExtension(fileExtension);

			if (language == null)
			{
				DisplayPlainText(richTextBlock, content);
				return;
			}

			var styleDictionary = ThemeHelper.GetCurrentThemeStyle();

			var formatter = new RichTextBlockFormatter(styleDictionary);
			formatter.FormatRichTextBlock(content, language, richTextBlock);
		}
		catch
		{
			DisplayPlainText(richTextBlock, content);
		}
	}

	private ILanguage? GetLanguageByExtension(string fileExtension)
	{
		return fileExtension.ToLowerInvariant() switch
		{
			".cs" => BetterCSharp.Language,
			// Agrupamos todas as variações de JS/TS/Vue na nova classe
			".js" or ".jsx" or ".ts" or ".tsx" or ".vue" => BetterJavascript.Language,

			".vb" => Languages.VbDotNet,
			".java" => Languages.Java,
			".cpp" or ".c" or ".h" or ".hpp" => Languages.Cpp,
			".css" or ".scss" => Languages.Css,
			".html" or ".htm" => Languages.Html,
			".xml" or ".xaml" or ".csproj" or ".config" => Languages.Xml,
			".sql" => Languages.Sql,
			".php" => Languages.Php,
			".py" => Languages.Python,
			".ps1" => Languages.PowerShell,
			".json" => Languages.JavaScript,
			".md" => Languages.Markdown,
			_ => null
		};
	}

	private void DisplayPlainText(RichTextBlock richTextBlock, string content)
	{
		var paragraph = new Paragraph();
		paragraph.Inlines.Add(new Run { Text = content });
		richTextBlock.Blocks.Add(paragraph);
	}
}