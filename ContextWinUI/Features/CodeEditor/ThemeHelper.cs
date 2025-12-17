using ColorCode.Common;
using ColorCode.Styling;
using Microsoft.UI.Xaml;
using Microsoft.UI;
using System;
using Windows.UI;

using Style = ColorCode.Styling.Style;

namespace ContextWinUI.Helpers;

public static class ThemeHelper
{
	public const string FieldScope = "Field";
	public const string PropertyScope = "Property";
	public const string ParameterScope = "Parameter";
	public const string LocalVariableScope = "Local Variable";
	public const string StructScope = "Struct";
	public const string EnumScope = "Enum";
	public const string InterfaceScope = "Interface Name";
	public const string MethodScope = "Method Name";
	public const string NamespaceScope = "Namespace";
	public const string ClassScope = ScopeName.ClassName;
	public const string ControlKeywordScope = "Control Keyword";
	public const string PunctuationScope = "Punctuation";

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

	// MÉTODO NOVO: Utilitário público para converter cores
	public static Color GetColorFromHex(string hex)
	{
		if (string.IsNullOrEmpty(hex)) return Colors.Transparent;

		hex = hex.Replace("#", "");
		byte a = 255;
		byte r = 0;
		byte g = 0;
		byte b = 0;

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
		return new StyleDictionary
		{
            // O Background definido aqui (#1E1E1E) será usado pela View agora
            new Style(ScopeName.PlainText) { Foreground = "#D4D4D4", Background = "#1E1E1E" },
			new Style(ScopeName.Keyword) { Foreground = "#569CD6" },
			new Style(ControlKeywordScope) { Foreground = "#C586C0" },
			new Style(ScopeName.String) { Foreground = "#CE9178" },
			new Style(ScopeName.StringCSharpVerbatim) { Foreground = "#CE9178" },
			new Style(ScopeName.Comment) { Foreground = "#6A9955" },
			new Style(ScopeName.XmlDocTag) { Foreground = "#6A9955" },
			new Style(ScopeName.Number) { Foreground = "#B5CEA8" },

			new Style(ClassScope)     { Foreground = "#4EC9B0" },
			new Style(InterfaceScope) { Foreground = "#B8D7A3" },
			new Style(MethodScope)    { Foreground = "#DCDCAA" },

			new Style(ParameterScope)     { Foreground = "#9CDCFE" },
			new Style(LocalVariableScope) { Foreground = "#9CDCFE" },

			new Style(PunctuationScope) { Foreground = "#FFD700" },

			new Style(NamespaceScope) { Foreground = "#FFFFFF" }
		};
	}

	private static StyleDictionary GetLightThemeStyle()
	{
		return new StyleDictionary
		{
			new Style(ScopeName.PlainText) { Foreground = "#000000", Background = "#FFFFFF" },
			new Style(ScopeName.Keyword) { Foreground = "#0000FF" },
			new Style(ControlKeywordScope) { Foreground = "#AF00DB" },
			new Style(ScopeName.String) { Foreground = "#A31515" },
			new Style(ScopeName.Comment) { Foreground = "#008000" },
			new Style(ScopeName.Number) { Foreground = "#09885A" },

			new Style(ClassScope)     { Foreground = "#2B91AF" },
			new Style(MethodScope)    { Foreground = "#74531F" },
			new Style(ParameterScope) { Foreground = "#1F377F" },
			new Style(PunctuationScope) { Foreground = "#000000" },
		};
	}
}