using ColorCode;
using ColorCode.Common;
using System.Collections.Generic;
using ContextWinUI.Helpers;

namespace ContextWinUI.Services;

public class BetterJavascript : ILanguage
{
	public static ILanguage Language { get; } = new BetterJavascript();

	public string Id => "TypeScript"; // Usado internamente para JS/TS/Vue/React
	public string Name => "JavaScript/TypeScript";
	public string CssClassName => "javascript";
	public string? FirstLinePattern => null;
	public IList<LanguageRule> Rules { get; }

	private BetterJavascript()
	{
		Rules = new List<LanguageRule>
		{
            // Comentários de bloco
            new LanguageRule(
				@"/\*([^*]|[\r\n]|(\*+([^*/]|[\r\n])))*\*+/",
				new Dictionary<int, string> { { 0, ScopeName.Comment } }),

            // Comentários de linha
            new LanguageRule(
				@"(//).*$",
				new Dictionary<int, string> { { 0, ScopeName.Comment } }),

            // Strings (aspas simples, duplas e crase/template literals)
            new LanguageRule(
				@"'[^\n]*?'(?<!\\')|""[^\n]*?""(?<!\\"")|`[^`]*`",
				new Dictionary<int, string> { { 0, ScopeName.String } }),

            // Keywords JS/TS
            new LanguageRule(
				@"\b(abstract|any|async|await|boolean|break|case|catch|class|const|continue|debugger|default|delete|do|else|enum|export|extends|false|finally|for|from|function|get|if|implements|import|in|instanceof|interface|let|module|new|null|number|of|package|private|protected|public|require|return|set|static|string|super|switch|this|throw|true|try|type|typeof|var|void|while|with|yield|undefined)\b",
				new Dictionary<int, string> { { 0, ScopeName.Keyword } }),

            // Tags JSX/HTML (ex: <div, <Component)
            new LanguageRule(
				@"(?<=<|/)\w+",
				new Dictionary<int, string> { { 0, ThemeHelper.ClassScope } }),

            // Decorators (ex: @Component)
            new LanguageRule(
				@"@\w+",
				new Dictionary<int, string> { { 0, ThemeHelper.AttributeScope } }),

            // Funções invocadas ou declaradas
            new LanguageRule(
				@"\b[\w]+(?=\s*\()",
				new Dictionary<int, string> { { 0, ThemeHelper.MethodScope } }),

            // Números
            new LanguageRule(
				@"\b\d+(\.\d+)?\b",
				new Dictionary<int, string> { { 0, ScopeName.Number } }),
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