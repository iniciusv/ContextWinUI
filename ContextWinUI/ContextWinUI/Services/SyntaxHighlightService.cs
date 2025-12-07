using ColorCode;
using ColorCode.Common;
using ColorCode.Styling;
using ContextWinUI.Helpers;
using ContextWinUI.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;

namespace ContextWinUI.ContextWinUI.Services;

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

			// O ColorCode usa as strings do LanguageRule para buscar no styleDictionary.
			// Como usamos ThemeHelper.MethodScope em ambos os lugares, o match vai acontecer.
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
			".cs" => BetterCSharp.Language, // Nossa versão customizada
			".vb" => Languages.VbDotNet,
			".java" => Languages.Java,
			".js" => Languages.JavaScript,
			".ts" or ".tsx" => Languages.Typescript,
			".cpp" or ".c" or ".h" or ".hpp" => Languages.Cpp,
			".css" => Languages.Css,
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