using ColorCode.Common;
using ColorCode.Styling;
using Microsoft.UI.Xaml;

// ALIAS: Resolve o conflito entre 'Microsoft.UI.Xaml.Style' e 'ColorCode.Styling.Style'
using Style = ColorCode.Styling.Style;

namespace ContextWinUI.Helpers;

/// <summary>
/// Helper responsável por definir as cores do Syntax Highlight baseado no tema do Windows.
/// Suporta tanto o ColorCode padrão quanto a análise semântica do Roslyn.
/// </summary>
public static class ThemeHelper
{
	// --- ESCOPOS SEMÂNTICOS (Usados pelo Roslyn) ---
	public const string FieldScope = "Field";
	public const string PropertyScope = "Property";
	public const string ParameterScope = "Parameter";
	public const string LocalVariableScope = "Local Variable";
	public const string StructScope = "Struct";
	public const string EnumScope = "Enum";
	public const string InterfaceScope = "Interface Name";
	public const string MethodScope = "Method Name";
	public const string NamespaceScope = "Namespace";
	public const string ClassScope = ScopeName.ClassName; // Reutiliza o nome padrão

	/// <summary>
	/// Detecta se o aplicativo está rodando em tema escuro.
	/// </summary>
	public static bool IsDarkTheme()
	{
		if (App.MainWindow?.Content is FrameworkElement rootElement)
		{
			return rootElement.ActualTheme == ElementTheme.Dark;
		}
		return Application.Current.RequestedTheme == ApplicationTheme.Dark;
	}

	/// <summary>
	/// Retorna o dicionário de estilos apropriado para o tema atual.
	/// </summary>
	public static StyleDictionary GetCurrentThemeStyle()
	{
		return IsDarkTheme() ? GetDarkThemeStyle() : GetLightThemeStyle();
	}

	/// <summary>
	/// Tema Escuro (Baseado no VS Code Dark+)
	/// </summary>
	private static StyleDictionary GetDarkThemeStyle()
	{
		return new StyleDictionary
		{
            // --- BÁSICOS (ColorCode & Roslyn) ---
            new Style(ScopeName.PlainText)
			{
				Foreground = "#D4D4D4",
				Background = "#1E1E1E"
			},
			new Style(ScopeName.Keyword)
			{
				Foreground = "#569CD6" // Azul
            },
			new Style(ScopeName.String)
			{
				Foreground = "#CE9178" // Laranja/Marrom
            },
			new Style(ScopeName.StringCSharpVerbatim)
			{
				Foreground = "#CE9178"
			},
			new Style(ScopeName.Comment)
			{
				Foreground = "#6A9955" // Verde
            },
			new Style(ScopeName.XmlDocTag)
			{
				Foreground = "#6A9955"
			},
			new Style(ScopeName.XmlDocComment)
			{
				Foreground = "#6A9955"
			},
			new Style(ScopeName.Number)
			{
				Foreground = "#B5CEA8" // Verde Claro
            },
			new Style(ScopeName.PreprocessorKeyword)
			{
				Foreground = "#9B9B9B" // Cinza
            },

            // --- SEMÂNTICOS (Específicos do Roslyn) ---
            
            // Tipos
            new Style(ClassScope)     { Foreground = "#4EC9B0" }, // Verde Água
            new Style(StructScope)    { Foreground = "#86C691" }, // Verde diferente
            new Style(InterfaceScope) { Foreground = "#B8D7A3" }, // Verde Pálido
            new Style(EnumScope)      { Foreground = "#B8D7A3" }, 

            // Membros
            new Style(MethodScope)    { Foreground = "#DCDCAA" }, // Amarelo
            new Style(PropertyScope)  { Foreground = "#9CDCFE" }, // Azul Claro
            new Style(FieldScope)     { Foreground = "#9CDCFE" }, 
            
            // Variáveis
            new Style(ParameterScope)     { Foreground = "#9CDCFE" },
			new Style(LocalVariableScope) { Foreground = "#9CDCFE" },
            
            // Outros
            new Style(NamespaceScope) { Foreground = "#FFFFFF" }
		};
	}

	/// <summary>
	/// Tema Claro (Baseado no Visual Studio Light)
	/// </summary>
	private static StyleDictionary GetLightThemeStyle()
	{
		return new StyleDictionary
		{
            // --- BÁSICOS ---
            new Style(ScopeName.PlainText)
			{
				Foreground = "#000000",
				Background = "#FFFFFF"
			},
			new Style(ScopeName.Keyword)
			{
				Foreground = "#0000FF" // Azul Escuro
            },
			new Style(ScopeName.String)
			{
				Foreground = "#A31515" // Vermelho Escuro
            },
			new Style(ScopeName.StringCSharpVerbatim)
			{
				Foreground = "#A31515"
			},
			new Style(ScopeName.Comment)
			{
				Foreground = "#008000" // Verde Escuro
            },
			new Style(ScopeName.XmlDocTag)
			{
				Foreground = "#808080"
			},
			new Style(ScopeName.XmlDocComment)
			{
				Foreground = "#008000"
			},
			new Style(ScopeName.Number)
			{
				Foreground = "#09885A"
			},
			new Style(ScopeName.PreprocessorKeyword)
			{
				Foreground = "#808080"
			},

            // --- SEMÂNTICOS ---

            // Tipos
            new Style(ClassScope)     { Foreground = "#2B91AF" }, // Azul Petróleo
            new Style(StructScope)    { Foreground = "#2B91AF" },
			new Style(InterfaceScope) { Foreground = "#2B91AF" },
			new Style(EnumScope)      { Foreground = "#2B91AF" },

            // Membros
            new Style(MethodScope)    { Foreground = "#74531F" }, // Dourado/Marrom
            new Style(PropertyScope)  { Foreground = "#000000" }, // Preto
            new Style(FieldScope)     { Foreground = "#000000" },
            
            // Variáveis
            new Style(ParameterScope)     { Foreground = "#1F377F" }, // Azul acinzentado
            new Style(LocalVariableScope) { Foreground = "#1F377F" },
            
            // Outros
            new Style(NamespaceScope) { Foreground = "#000000" }
		};
	}
}