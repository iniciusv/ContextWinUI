using ColorCode;
using ColorCode.Common;
using ContextWinUI.Helpers;
using System.Collections.Generic;

namespace ContextWinUI.Features.CodeEditor; // Verifique se o namespace está correto para sua pasta

public class BetterJavascript : ILanguage
{
	public static ILanguage Language { get; } = new BetterJavascript();

	public string Id => "TypeScript";
	public string Name => "JavaScript/TypeScript";
	public string CssClassName => "javascript";
	public string? FirstLinePattern => null;

	public IList<LanguageRule> Rules { get; }

	private BetterJavascript()
	{
		Rules = new List<LanguageRule>
		{
            // Comentário de Bloco
            new LanguageRule(
				@"/\*([^*]|[\r\n]|(\*+([^*/]|[\r\n])))*\*+/",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeComment } }),
            
            // Comentário de Linha
            new LanguageRule(
				@"(//).*?$",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeComment } }),

            // Strings (Simples, Duplas e Template Literals)
            new LanguageRule(
				@"'[^\n]*?'(?<!\\')|""[^\n]*?""(?<!\\"")|`[^`]*`",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeString } }),

            // Keywords
            new LanguageRule(
				@"\b(abstract|any|async|await|boolean|break|case|catch|class|const|continue|debugger|default|delete|do|else|enum|export|extends|false|finally|for|from|function|get|if|implements|import|in|instanceof|interface|let|module|new|null|number|of|package|private|protected|public|require|return|set|static|string|super|switch|this|throw|true|try|type|typeof|var|void|while|with|yield|undefined)\b",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeKeyword } }),

            // Classes (ex: <MyClass> ou new MyClass)
            new LanguageRule(
				@"(?<=<|/)\w+",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeClass } }), // CORRIGIDO

            // Decorators/Attributes (@Component)
            new LanguageRule(
				@"@\w+",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeAttribute } }), // CORRIGIDO

            // Métodos (palavra antes de parêntese)
            new LanguageRule(
				@"\b[\w]+(?=\s*\()",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeMethod } }), // CORRIGIDO

            // Números
            new LanguageRule(
				@"\b\d+(\.\d+)?\b",
				new Dictionary<int, string> { { 0, ThemeHelper.ScopeNumber } }),
		};
	}

	public bool HasAlias(string lang)
	{
		switch (lang.ToLower())
		{
			case "js":
			case "javascript":
			case "ts":
			case "typescript":
			case "vue":
			case "tsx":
			case "jsx":
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