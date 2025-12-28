using ColorCode.Styling;
using Windows.UI;

namespace ContextWinUI.Helpers;

public interface IThemeService
{
	string ScopePlainText { get; }
	string ScopeKeyword { get; }
	string ScopeControlKeyword { get; }
	string ScopeString { get; }
	string ScopeNumber { get; }
	string ScopeComment { get; }
	string ScopeClass { get; }
	string ScopeInterface { get; }
	string ScopeStruct { get; }
	string ScopeEnum { get; }
	string ScopeMethod { get; }
	string ScopeProperty { get; }
	string ScopeField { get; }
	string ScopeAttribute { get; }
	string ScopeParameter { get; }
	string ScopeVariable { get; }
	string ScopePunctuation { get; }
	string ScopeOperator { get; }
	string ScopePreprocessor { get; }

	bool IsDarkTheme();
	StyleDictionary GetCurrentThemeStyle();

	Color GetColorFromHex(string hex);
}