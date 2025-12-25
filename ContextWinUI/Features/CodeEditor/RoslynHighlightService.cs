// ARQUIVO: RoslynHighlightService.cs
using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace ContextWinUI.Services;

public class RoslynHighlightService
{
	// Cache básico de referências para performance (mscorlib, system, etc)
	private static readonly List<MetadataReference> _defaultReferences = new();

	static RoslynHighlightService()
	{
		// Carrega referências básicas para que o 'var', 'string', 'int' sejam reconhecidos semanticamente
		var assemblies = new[]
		{
			typeof(object).Assembly,                  // mscorlib / System.Private.CoreLib
                typeof(Uri).Assembly,                     // System.Private.Uri
                typeof(System.Linq.Enumerable).Assembly   // System.Linq
            };

		foreach (var assembly in assemblies)
		{
			try { _defaultReferences.Add(MetadataReference.CreateFromFile(assembly.Location)); } catch { }
		}
	}

	public async Task<List<HighlightSpan>> CalculateHighlightsAsync(string text, bool isDark)
	{
		if (string.IsNullOrWhiteSpace(text)) return new List<HighlightSpan>();

		var theme = ThemeHelper.GetCurrentThemeStyle();

		return await Task.Run(async () =>
		{
			var highlights = new List<HighlightSpan>();

			// 1. Criar um "Adhoc Workspace" para análise
			using var workspace = new AdhocWorkspace();
			var projectId = ProjectId.CreateNewId();
			var versionStamp = VersionStamp.Create();

			var projectInfo = ProjectInfo.Create(projectId, versionStamp, "TempProject", "TempAssembly", LanguageNames.CSharp)
				.WithMetadataReferences(_defaultReferences);

			var project = workspace.AddProject(projectInfo);
			var document = workspace.AddDocument(project.Id, "TempFile.cs", SourceText.From(text));

			// 2. Obter spans classificados (A mágica acontece aqui)
			// Isso usa a mesma engine do Visual Studio para categorizar cada pedaço do código
			var classifiedSpans = await Classifier.GetClassifiedSpansAsync(document, TextSpan.FromBounds(0, text.Length));

			// 3. Mapear classificações do Roslyn para Cores
			foreach (var span in classifiedSpans)
			{
				var color = GetColorForClassification(span.ClassificationType, theme);

				// Só adiciona se a cor for diferente da padrão (otimização)
				if (color.A > 0)
				{
					highlights.Add(new HighlightSpan
					{
						Start = span.TextSpan.Start,
						Length = span.TextSpan.Length,
						Color = color
					});
				}
			}
			return highlights;
		});
	}

	private Color GetColorForClassification(string classificationType, ColorCode.Styling.StyleDictionary theme)
	{
		Color GetThemeColor(string scope)
		{
			if (theme.Contains(scope))
			{
				return ThemeHelper.GetColorFromHex(theme[scope].Foreground);
			}
			// Retorna transparente se não achar, para manter a cor padrão do texto
			return Colors.Transparent;
		}

		return classificationType switch
		{
			ClassificationTypeNames.Keyword => GetThemeColor(ThemeHelper.ScopeKeyword),
			ClassificationTypeNames.ControlKeyword => GetThemeColor(ThemeHelper.ScopeControlKeyword),

			ClassificationTypeNames.Operator => GetThemeColor(ThemeHelper.ScopeOperator),
			ClassificationTypeNames.Punctuation => GetThemeColor(ThemeHelper.ScopePunctuation),

			ClassificationTypeNames.StringLiteral => GetThemeColor(ThemeHelper.ScopeString),
			ClassificationTypeNames.VerbatimStringLiteral => GetThemeColor(ThemeHelper.ScopeString),

			ClassificationTypeNames.ClassName => GetThemeColor(ThemeHelper.ScopeClass),
			ClassificationTypeNames.StructName => GetThemeColor(ThemeHelper.ScopeStruct),
			ClassificationTypeNames.InterfaceName => GetThemeColor(ThemeHelper.ScopeInterface),
			ClassificationTypeNames.EnumName => GetThemeColor(ThemeHelper.ScopeEnum),

			ClassificationTypeNames.MethodName => GetThemeColor(ThemeHelper.ScopeMethod),
			ClassificationTypeNames.ExtensionMethodName => GetThemeColor(ThemeHelper.ScopeMethod),

			ClassificationTypeNames.PropertyName => GetThemeColor(ThemeHelper.ScopeProperty),
			ClassificationTypeNames.FieldName => GetThemeColor(ThemeHelper.ScopeField),
			ClassificationTypeNames.ConstantName => GetThemeColor(ThemeHelper.ScopeField),

			ClassificationTypeNames.ParameterName => GetThemeColor(ThemeHelper.ScopeParameter),
			ClassificationTypeNames.LocalName => GetThemeColor(ThemeHelper.ScopeVariable),

			ClassificationTypeNames.Comment => GetThemeColor(ThemeHelper.ScopeComment),
			ClassificationTypeNames.XmlDocCommentText => GetThemeColor(ThemeHelper.ScopeComment),

			ClassificationTypeNames.NumericLiteral => GetThemeColor(ThemeHelper.ScopeNumber),
			ClassificationTypeNames.PreprocessorKeyword => GetThemeColor(ThemeHelper.ScopePreprocessor),

			_ => Colors.Transparent
		};
	}

	public void ApplyHighlights(RichEditBox editor, List<HighlightSpan> highlights)
	{
		if (editor == null || highlights == null) return;

		// Congela atualizações visuais para performance
		editor.Document.BatchDisplayUpdates();

		try
		{
			// Obter texto total
			editor.Document.GetText(TextGetOptions.None, out string text);
			int totalLength = text.Length;

			// 1. Resetar cor base (evita sobras de highlights antigos)
			var defaultColor = ContextWinUI.Helpers.ThemeHelper.IsDarkTheme()
				? Color.FromArgb(255, 212, 212, 212)
				: Colors.Black;

			editor.Document.GetRange(0, totalLength).CharacterFormat.ForegroundColor = defaultColor;

			// 2. Aplicar highlights
			// Ordenar por início ajuda o motor de renderização
			foreach (var span in highlights.OrderBy(h => h.Start))
			{
				int start = Math.Clamp(span.Start, 0, totalLength);
				int end = Math.Clamp(span.Start + span.Length, 0, totalLength);

				if (end > start)
				{
					var range = editor.Document.GetRange(start, end);
					range.CharacterFormat.ForegroundColor = span.Color;
				}
			}
		}
		catch
		{
			// Ignorar erros de range durante edição rápida
		}
		finally
		{
			editor.Document.ApplyDisplayUpdates();
		}
	}

	public void ApplyDiffHighlights(RichEditBox editor, List<DiffLine> diffLines)
	{
		if (editor == null || diffLines == null) return;

		// Cores para o Diff (Verde claro para adição)
		var addedColor = ContextWinUI.Helpers.ThemeHelper.IsDarkTheme()
			? Color.FromArgb(60, 40, 80, 40)  // Verde escuro transparente (Dark Mode)
			: Color.FromArgb(60, 200, 255, 200); // Verde claro transparente (Light Mode)

		editor.Document.BatchDisplayUpdates();
		try
		{
			// 1. Reseta cor de fundo
			editor.Document.GetText(TextGetOptions.None, out string text);
			editor.Document.GetRange(0, text.Length).CharacterFormat.BackgroundColor = Colors.Transparent;

			// Precisamos mapear as linhas do Diff para posições de caracteres no RichEditBox
			int currentCharIndex = 0;

			foreach (var line in diffLines)
			{
				int lineLength = line.Text.Length;

				// +1 conta o \r ou \n. O RichEditBox no Windows normaliza para \r
				int rangeLength = lineLength + 1;

				if (line.Type == DiffType.Added)
				{
					// Proteção para não estourar o tamanho do texto
					int safeEnd = Math.Min(currentCharIndex + rangeLength, text.Length + 1);

					if (safeEnd > currentCharIndex)
					{
						var range = editor.Document.GetRange(currentCharIndex, safeEnd);
						range.CharacterFormat.BackgroundColor = addedColor;
					}
				}

				currentCharIndex += rangeLength;
			}
		}
		finally
		{
			editor.Document.ApplyDisplayUpdates();
		}
	}
}