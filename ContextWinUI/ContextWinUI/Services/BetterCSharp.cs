using ColorCode;
using ColorCode.Common;
using System.Collections.Generic;
using ContextWinUI.Helpers;

namespace ContextWinUI.Services;

/// <summary>
/// Implementação customizada de ILanguage para C#.
/// Implementamos a interface diretamente para evitar erros de referência de assembly.
/// </summary>
public class BetterCSharp : ILanguage
{
	// Singleton instance para fácil acesso
	public static ILanguage Language { get; } = new BetterCSharp();

	// Propriedades da Interface ILanguage
	public string Id => LanguageId.CSharp;
	public string Name => "C# (Enhanced)";
	public string CssClassName => "csharp";
	public string? FirstLinePattern => null;
	public IList<LanguageRule> Rules { get; }

	// Construtor privado onde definimos as regras
	private BetterCSharp()
	{
		Rules = new List<LanguageRule>
		{
            // 1. Comentários
            new LanguageRule(
				@"/\*([^*]|[\r\n]|(\*+([^*/]|[\r\n])))*\*+/",
				new Dictionary<int, string> { { 0, ScopeName.Comment } }),

			new LanguageRule(
				@"(//.*?)\r?$",
				new Dictionary<int, string> { { 0, ScopeName.Comment } }),

            // 2. Strings
            new LanguageRule(
				@"'[^\n]*?'(?<!\\')",
				new Dictionary<int, string> { { 0, ScopeName.String } }),

			new LanguageRule(
				@"(?s)@""(?:""""|[^""])*""(?!"")",
				new Dictionary<int, string> { { 0, ScopeName.StringCSharpVerbatim } }),

			new LanguageRule(
				@"(?s)""(?:\\""|[^""])*""(?!"")",
				new Dictionary<int, string> { { 0, ScopeName.String } }),

            // 3. Keywords
            new LanguageRule(
				@"\b(abstract|as|async|await|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|get|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|set|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|var|virtual|void|volatile|while|record|init)\b",
				new Dictionary<int, string> { { 0, ScopeName.Keyword } }),

            // 4. Interfaces (Usando ThemeHelper.InterfaceScope)
            new LanguageRule(
				@"\bI[A-Z]\w*\b",
				new Dictionary<int, string> { { 0, ThemeHelper.InterfaceScope } }),

            // 5. Métodos (Usando ThemeHelper.MethodScope)
            new LanguageRule(
				@"\b[\w]+(?=\s*\()",
				new Dictionary<int, string> { { 0, ThemeHelper.MethodScope } }),

            // 6. Classes (Usando ThemeHelper.ClassScope)
            new LanguageRule(
				@"\b[A-Z]\w*\b",
				new Dictionary<int, string> { { 0, ThemeHelper.ClassScope } }),

            // 7. Números
            new LanguageRule(
				@"\b\d+(\.\d+)?(f|d|m|u|l)?\b",
				new Dictionary<int, string> { { 0, ScopeName.Number } }),
		};
	}

	/// <summary>
	/// Método necessário para algumas versões do ILanguage.
	/// Verifica se a linguagem corresponde a um alias (ex: "cs").
	/// </summary>
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