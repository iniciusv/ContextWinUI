using ColorCode.Styling;
using ContextWinUI.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace ContextWinUI.Features.CodeEditor.Highlight;

public sealed class ThemeService : IThemeService
{
	// Singleton (opcional)
	private static readonly Lazy<ThemeService> _instance = new(() => new ThemeService());
	public static ThemeService Instance => _instance.Value;

	// Implementação das propriedades
	public string ScopePlainText => ThemeHelper.ScopePlainText;
	public string ScopeKeyword => ThemeHelper.ScopeKeyword;
	public string ScopeControlKeyword => ThemeHelper.ScopeControlKeyword;
	public string ScopeString => ThemeHelper.ScopeString;
	public string ScopeNumber => ThemeHelper.ScopeNumber;
	public string ScopeComment => ThemeHelper.ScopeComment;
	public string ScopeClass => ThemeHelper.ScopeClass;
	public string ScopeInterface => ThemeHelper.ScopeInterface;
	public string ScopeStruct => ThemeHelper.ScopeStruct;
	public string ScopeEnum => ThemeHelper.ScopeEnum;
	public string ScopeMethod => ThemeHelper.ScopeMethod;
	public string ScopeProperty => ThemeHelper.ScopeProperty;
	public string ScopeField => ThemeHelper.ScopeField;
	public string ScopeAttribute => ThemeHelper.ScopeAttribute;
	public string ScopeParameter => ThemeHelper.ScopeParameter;
	public string ScopeVariable => ThemeHelper.ScopeVariable;
	public string ScopePunctuation => ThemeHelper.ScopePunctuation;
	public string ScopeOperator => ThemeHelper.ScopeOperator;
	public string ScopePreprocessor => ThemeHelper.ScopePreprocessor;

	// Implementação dos métodos (delega para a classe estática)
	public bool IsDarkTheme() => ThemeHelper.IsDarkTheme();

	public StyleDictionary GetCurrentThemeStyle() => ThemeHelper.GetCurrentThemeStyle();

	public Color GetColorFromHex(string hex) => ThemeHelper.GetColorFromHex(hex);
}