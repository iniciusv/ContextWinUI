// ARQUIVO: Helpers/ThemeHelper.cs
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;
using ColorCode.Styling;

// CORREÇÃO AQUI: Criamos um apelido (Alias) para o Style do ColorCode
// para não confundir com o Style do XAML
using CCStyle = ColorCode.Styling.Style;

namespace ContextWinUI.Helpers;

public static class ThemeHelper
{
	// Constantes de Escopo (Para garantir consistência entre Roslyn e Regex)
	public const string ScopePlainText = "PlainText";
	public const string ScopeKeyword = "Keyword";
	public const string ScopeControlKeyword = "ControlKeyword";
	public const string ScopeString = "String";
	public const string ScopeNumber = "Number";
	public const string ScopeComment = "Comment";
	public const string ScopeClass = "Class";
	public const string ScopeInterface = "Interface";
	public const string ScopeStruct = "Struct";
	public const string ScopeEnum = "Enum";
	public const string ScopeMethod = "Method";
	public const string ScopeProperty = "Property";
	public const string ScopeField = "Field";
	public const string ScopeAttribute = "Attribute";
	public const string ScopeParameter = "Parameter";
	public const string ScopeVariable = "Variable";
	public const string ScopePunctuation = "Punctuation";
	public const string ScopeOperator = "Operator";
	public const string ScopePreprocessor = "Preprocessor";

	public static bool IsDarkTheme()
	{
		if (App.MainWindow?.Content is FrameworkElement rootElement)
		{
			return rootElement.ActualTheme == ElementTheme.Dark;
		}
		return Application.Current.RequestedTheme == ApplicationTheme.Dark;
	}

	public static StyleDictionary GetCurrentThemeStyle()
	{
		return IsDarkTheme() ? GetDarkThemeStyle() : GetLightThemeStyle();
	}

	public static Color GetColorFromHex(string hex)
	{
		if (string.IsNullOrEmpty(hex)) return Colors.Transparent;
		hex = hex.Replace("#", "");

		byte a = 255;
		byte r = 0, g = 0, b = 0;

		if (hex.Length == 6)
		{
			r = Convert.ToByte(hex.Substring(0, 2), 16);
			g = Convert.ToByte(hex.Substring(2, 2), 16);
			b = Convert.ToByte(hex.Substring(4, 2), 16);
		}
		else if (hex.Length == 8)
		{
			a = Convert.ToByte(hex.Substring(0, 2), 16);
			r = Convert.ToByte(hex.Substring(2, 2), 16);
			g = Convert.ToByte(hex.Substring(4, 2), 16);
			b = Convert.ToByte(hex.Substring(6, 2), 16);
		}
		return Color.FromArgb(a, r, g, b);
	}

	private static StyleDictionary GetDarkThemeStyle()
	{
		// Agora usamos 'new CCStyle' em vez de apenas 'new Style'
		return new StyleDictionary
		{
			new CCStyle(ScopePlainText) { Foreground = "#DCDCDC", Background = "#1E1E1E" },
			new CCStyle(ScopeKeyword) { Foreground = "#569CD6" },
			new CCStyle(ScopeControlKeyword) { Foreground = "#D8A0DF" },
			new CCStyle(ScopeString) { Foreground = "#D69D85" },
			new CCStyle(ScopeComment) { Foreground = "#57A64A" },
			new CCStyle(ScopeNumber) { Foreground = "#B5CEA8" },
			new CCStyle(ScopeClass) { Foreground = "#4EC9B0" },
			new CCStyle(ScopeInterface) { Foreground = "#B8D7A3" },
			new CCStyle(ScopeStruct) { Foreground = "#86C691" },
			new CCStyle(ScopeEnum) { Foreground = "#B8D7A3" },
			new CCStyle(ScopeMethod) { Foreground = "#DCDCAA" },
			new CCStyle(ScopeProperty) { Foreground = "#FFFFFF" },
			new CCStyle(ScopeField) { Foreground = "#9CDCFE" },
			new CCStyle(ScopeAttribute) { Foreground = "#DCDCAA" },
			new CCStyle(ScopeParameter) { Foreground = "#9CDCFE" },
			new CCStyle(ScopeVariable) { Foreground = "#9CDCFE" },
			new CCStyle(ScopePunctuation) { Foreground = "#D4D4D4" },
			new CCStyle(ScopeOperator) { Foreground = "#D4D4D4" },
			new CCStyle(ScopePreprocessor) { Foreground = "#9B9B9B" },
		};
	}

	private static StyleDictionary GetLightThemeStyle()
	{
		return new StyleDictionary
		{
			new CCStyle(ScopePlainText) { Foreground = "#000000", Background = "#FFFFFF" },
			new CCStyle(ScopeKeyword) { Foreground = "#0000FF" },
			new CCStyle(ScopeControlKeyword) { Foreground = "#8F08C4" },
			new CCStyle(ScopeString) { Foreground = "#A31515" },
			new CCStyle(ScopeComment) { Foreground = "#008000" },
			new CCStyle(ScopeNumber) { Foreground = "#09885A" },
			new CCStyle(ScopeClass) { Foreground = "#2B91AF" },
			new CCStyle(ScopeInterface) { Foreground = "#2B91AF" },
			new CCStyle(ScopeStruct) { Foreground = "#2B91AF" },
			new CCStyle(ScopeEnum) { Foreground = "#2B91AF" },
			new CCStyle(ScopeMethod) { Foreground = "#74531F" },
			new CCStyle(ScopeProperty) { Foreground = "#000000" },
			new CCStyle(ScopeField) { Foreground = "#000000" },
			new CCStyle(ScopeAttribute) { Foreground = "#74531F" },
			new CCStyle(ScopeParameter) { Foreground = "#1F377F" },
			new CCStyle(ScopeVariable) { Foreground = "#1F377F" },
			new CCStyle(ScopePunctuation) { Foreground = "#000000" },
			new CCStyle(ScopeOperator) { Foreground = "#000000" },
			new CCStyle(ScopePreprocessor) { Foreground = "#808080" },
		};
	}
}