using ColorCode;
using ColorCode.Common;
using ContextWinUI.Helpers;
using System.Collections.Generic;

namespace ContextWinUI.Features.CodeEditor; // Ajuste o namespace conforme sua pasta

public class BetterCSharp : ILanguage
{
	public static ILanguage Language { get; } = new BetterCSharp();

	public string Id => LanguageId.CSharp;
	public string Name => "C# (Enhanced)";
	public string CssClassName => "csharp";
	public string? FirstLinePattern => null;

	public IList<LanguageRule> Rules { get; }

	private BetterCSharp()
	{
		Rules = new List<LanguageRule>
		{
            // Comentários de Documentação XML (/// summary)
            new LanguageRule(
				@"(///)(?:\s*<.+>)?(?:.+)?",
				new Dictionary<int, string>
				{
					{ 1, ThemeHelper.ScopeComment },
					{ 0, ThemeHelper.ScopeComment }
				}),

            // Comentários de Bloco /* ... */
            new LanguageRule(
				@"/\*([^*]|[\r\n]|(\*+([^*/]|[\r\n])))*\*+/",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeComment } }),

            // Comentários de Linha // ...
            new LanguageRule(
				@"(//).*?$",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeComment } }),

            // Strings Verbatim @"..."
            new LanguageRule(
				@"(?s)@""(?:""""|[^""])*""(?!"")",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeString } }),

            // Strings Interpoladas $""
            new LanguageRule(
				@"(?s)\$""(?:\\""|[^""])*""(?!"")",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeString } }),

            // Strings Normais "..."
            new LanguageRule(
				@"(?s)""(?:\\""|[^""])*""(?!"")",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeString } }),

            // Char 'c'
            new LanguageRule(
				@"'[^\n]*?'(?<!\\')",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeString } }),

            // Palavras-chave de Controle (if, else, return, etc) - Roxo no VS
            new LanguageRule(
				@"\b(break|case|continue|default|do|else|for|foreach|goto|if|return|switch|throw|try|catch|finally|while|yield|await)\b",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeControlKeyword } }),

            // Palavras-chave Gerais (public, static, void, etc) - Azul no VS
            new LanguageRule(
				@"\b(abstract|as|async|base|bool|byte|char|checked|class|const|decimal|delegate|double|enum|event|explicit|extern|false|fixed|float|get|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|sbyte|sealed|set|short|sizeof|stackalloc|static|string|struct|this|true|typeof|uint|ulong|unchecked|unsafe|ushort|using|var|virtual|void|volatile|record|init)\b",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeKeyword } }),

            // Interfaces (Começa com I maiúsculo seguido de outra maiúscula)
            new LanguageRule(
				@"\bI[A-Z]\w*\b",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeInterface } }),

            // Classes (Começa com maiúscula, excluindo palavras-chave)
            new LanguageRule(
				@"\b[A-Z]\w*\b",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeClass } }),

            // Métodos (Palavra antes de um parêntese de abertura)
            new LanguageRule(
				@"\b[\w]+(?=\s*\()",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeMethod } }),

            // Atributos [Attribute]
            new LanguageRule(
				@"\[\s*\w+(?:\(.*\))?\s*\]",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeAttribute } }),

            // Números (Hex, Float, Int)
            new LanguageRule(
				@"\b0x[0-9a-fA-F]+\b|(\b\d+(\.[0-9]+)?(f|d|m|u|l)?\b)",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeNumber } }),
		};
	}

	public bool HasAlias(string lang)
	{
		switch (lang.ToLower())
		{
			case "cs":
			case "csharp":
			case "c#":
				return true;
			default:
				return false;
		}
	}

	public override string ToString()
	{
		return Name;
	}
}