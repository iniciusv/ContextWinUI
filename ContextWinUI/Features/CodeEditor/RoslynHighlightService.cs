// ARQUIVO: RoslynHighlightService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ContextWinUI.Features.CodeEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
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

	public async Task<List<HighlightSpan>> CalculateHighlightsAsync(string text, ColorCode.Styling.StyleDictionary theme, bool isDark)
	{
		if (string.IsNullOrWhiteSpace(text)) return new List<HighlightSpan>();

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
				var color = GetColorForClassification(span.ClassificationType, theme, isDark);

				// Só adiciona se a cor for diferente da padrão (otimização)
				if (color != Colors.Transparent)
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

	private Color GetColorForClassification(string classificationType, ColorCode.Styling.StyleDictionary theme, bool isDark)
	{
		// Helper para buscar do tema ou usar fallback
		Color C(string scope, Color fallback)
		{
			if (theme.Contains(scope))
				return ContextWinUI.Helpers.ThemeHelper.GetColorFromHex(theme[scope].Foreground);
			return fallback;
		}

		// Mapeamento: Roslyn Classification Type -> VS Code Colors
		// Baseado no VS Code Dark+ (Dark) e Visual Studio Light (Light)
		return classificationType switch
		{
			// Keywords & Control
			ClassificationTypeNames.Keyword => C("Keyword", isDark ? Color.FromArgb(255, 86, 156, 214) : Color.FromArgb(255, 0, 0, 255)),
			ClassificationTypeNames.ControlKeyword => C("Control Keyword", isDark ? Color.FromArgb(255, 197, 134, 192) : Color.FromArgb(255, 143, 8, 196)),
			ClassificationTypeNames.Operator => isDark ? Color.FromArgb(255, 212, 212, 212) : Colors.Black,
			ClassificationTypeNames.Punctuation => isDark ? Color.FromArgb(255, 255, 215, 0) : Colors.Black, // Gold no Dark para destaque

			// Strings & Characters
			ClassificationTypeNames.StringLiteral => C("String", isDark ? Color.FromArgb(255, 206, 145, 120) : Color.FromArgb(255, 163, 21, 21)),
			ClassificationTypeNames.VerbatimStringLiteral => C("String", isDark ? Color.FromArgb(255, 206, 145, 120) : Color.FromArgb(255, 163, 21, 21)),

			// Classes, Structs, Interfaces
			ClassificationTypeNames.ClassName => C("Class", isDark ? Color.FromArgb(255, 78, 201, 176) : Color.FromArgb(255, 43, 145, 175)),
			ClassificationTypeNames.StructName => C("Struct", isDark ? Color.FromArgb(255, 134, 198, 145) : Color.FromArgb(255, 43, 145, 175)),
			ClassificationTypeNames.InterfaceName => C("Interface Name", isDark ? Color.FromArgb(255, 184, 215, 163) : Color.FromArgb(255, 43, 145, 175)),
			ClassificationTypeNames.EnumName => C("Enum", isDark ? Color.FromArgb(255, 184, 215, 163) : Color.FromArgb(255, 43, 145, 175)),

			// Members
			ClassificationTypeNames.MethodName => C("Method Name", isDark ? Color.FromArgb(255, 220, 220, 170) : Color.FromArgb(255, 116, 83, 31)),
			ClassificationTypeNames.ExtensionMethodName => C("Method Name", isDark ? Color.FromArgb(255, 220, 220, 170) : Color.FromArgb(255, 116, 83, 31)),
			ClassificationTypeNames.PropertyName => isDark ? Colors.White : Colors.Black, // VS Code geralmente deixa propriedades em branco/preto
			ClassificationTypeNames.FieldName => isDark ? Color.FromArgb(255, 156, 220, 254) : Colors.Black,
			ClassificationTypeNames.ConstantName => isDark ? Color.FromArgb(255, 156, 220, 254) : Colors.Black,

			// Variables & Parameters
			ClassificationTypeNames.ParameterName => C("Parameter", isDark ? Color.FromArgb(255, 156, 220, 254) : Color.FromArgb(255, 31, 55, 127)),
			ClassificationTypeNames.LocalName => C("Local Variable", isDark ? Color.FromArgb(255, 156, 220, 254) : Color.FromArgb(255, 31, 55, 127)),

			// Comments
			ClassificationTypeNames.Comment => C("Comment", isDark ? Color.FromArgb(255, 106, 153, 85) : Color.FromArgb(255, 0, 128, 0)),
			ClassificationTypeNames.XmlDocCommentText => C("Comment", isDark ? Color.FromArgb(255, 106, 153, 85) : Color.FromArgb(255, 0, 128, 0)),
			ClassificationTypeNames.XmlDocCommentAttributeName => C("Comment", isDark ? Color.FromArgb(255, 106, 153, 85) : Color.FromArgb(255, 128, 128, 128)),

			// Misc
			ClassificationTypeNames.NumericLiteral => C("Number", isDark ? Color.FromArgb(255, 181, 206, 168) : Colors.Black),
			ClassificationTypeNames.PreprocessorKeyword => C("Keyword", isDark ? Color.FromArgb(255, 155, 155, 155) : Colors.Gray),

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