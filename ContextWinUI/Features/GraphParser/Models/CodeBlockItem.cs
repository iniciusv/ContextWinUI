// ARQUIVO: CodeBlockItem.cs
using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Models;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ContextWinUI.Features.GraphParser.Models;

public partial class CodeBlockItem : ObservableObject
{
	public string Id { get; set; } = string.Empty;

	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private string content = string.Empty;

	[ObservableProperty]
	private string typeDescription = string.Empty;

	[ObservableProperty]
	private string icon = string.Empty;

	[ObservableProperty]
	private int startLine;

	[ObservableProperty]
	private int endLine;

	public SymbolType SymbolType { get; set; }

	public string FileExtension { get; set; } = ".cs";

	public int AbsoluteStartPosition { get; set; }

	// Propriedade para colorir o cabeçalho do bloco baseado no tipo
	public SolidColorBrush HeaderBrush => SymbolType switch
	{
		SymbolType.Method => new SolidColorBrush(Microsoft.UI.Colors.Goldenrod),
		SymbolType.Class => new SolidColorBrush(Microsoft.UI.Colors.Teal),
		SymbolType.Interface => new SolidColorBrush(Microsoft.UI.Colors.DarkSeaGreen),
		SymbolType.Property => new SolidColorBrush(Microsoft.UI.Colors.SlateGray),
		_ => new SolidColorBrush(Microsoft.UI.Colors.Gray)
	};

	public SegmentType SegmentType { get; set; }

	// Nível de profundidade (0 = raiz, 1 = dentro do namespace, 2 = dentro da classe)
	// Útil para desenhar margem na UI
	public int DepthLevel { get; set; }

	// Helper para saber se é um bloco de "código real" ou apenas estrutura/espaço
	public bool IsStructural => SegmentType == SegmentType.ClassHeader ||
								SegmentType == SegmentType.NamespaceDecl ||
								SegmentType == SegmentType.CloseBrace ||
								SegmentType == SegmentType.Trivia;

	public bool IsGranular => SegmentType == SegmentType.Method ||
							  SegmentType == SegmentType.Property ||
							  SegmentType == SegmentType.Field ||
							  SegmentType == SegmentType.Constructor ||
							  SegmentType == SegmentType.Enum;

	public CodeBlockItem Clone()
	{
		return new CodeBlockItem
		{
			Id = Guid.NewGuid().ToString(), // Novo ID para evitar conflitos de UI
			Name = this.Name,
			Content = this.Content, // O conteúdo é string (imutável), então ok
			TypeDescription = this.TypeDescription,
			Icon = this.Icon,
			StartLine = this.StartLine,
			EndLine = this.EndLine,
			SymbolType = this.SymbolType,
			FileExtension = this.FileExtension,
			SegmentType = this.SegmentType,
			DepthLevel = this.DepthLevel
		};
	}


	[ObservableProperty]
	private ObservableCollection<CodeBlockVersion> versions = new();

	[ObservableProperty]
	private int currentVersionIndex = 0;

	[ObservableProperty]
	private bool hasUnsavedChanges;

	private string _originalContent = string.Empty;


	public void InitializeVersions(string initialContent)
	{
		_originalContent = initialContent;

		// 1. Limpa qualquer lixo anterior
		Versions.Clear();

		// 2. Cria a Versão 0 (Original/Baseline)
		Versions.Add(new CodeBlockVersion
		{
			Id = Guid.NewGuid().ToString(),
			Content = initialContent,
			Description = "Versão Original", // Texto padrão para identificar o inicio
			Timestamp = DateTime.MinValue,
			IsOriginal = true // <--- ISSO É CRUCIAL
		});

		// 3. Define o ponteiro para a versão 0
		CurrentVersionIndex = 0;

		// 4. Garante que o sistema saiba que não há pendências
		HasUnsavedChanges = false;

		// Notifica a UI para atualizar ícones (deve ficar verde/original)
		OnPropertyChanged(nameof(CurrentVersionDescription));
		OnPropertyChanged(nameof(IsCurrentVersionOriginal));
	}

	public void CreateNewVersion(string newContent, string description = "Modificado")
	{
		// Removemos a verificação (Content != newContent) porque ao editar, 
		// o Content JÁ É o newContent (devido ao TwoWay binding).

		var newVersion = new CodeBlockVersion
		{
			Content = newContent,
			Description = description,
			Timestamp = DateTime.Now,
			IsOriginal = false
		};

		Versions.Add(newVersion);
		CurrentVersionIndex = Versions.Count - 1;

		// Se acabamos de salvar uma versão com este conteúdo, não há mudanças pendentes
		HasUnsavedChanges = false;

		OnPropertyChanged(nameof(CurrentVersionDescription));
	}

	public bool RestoreVersion(int versionIndex)
	{
		if (versionIndex >= 0 && versionIndex < Versions.Count)
		{
			var version = Versions[versionIndex];
			Content = version.Content;
			CurrentVersionIndex = versionIndex;
			HasUnsavedChanges = versionIndex != 0; // Não é original se não for índice 0

			OnPropertyChanged(nameof(CurrentVersionDescription));
			return true;
		}
		return false;
	}

	partial void OnContentChanged(string value)
	{
		// Verifica se temos versões para comparar
		if (Versions != null && Versions.Any() && CurrentVersionIndex >= 0 && CurrentVersionIndex < Versions.Count)
		{
			var savedContent = Versions[CurrentVersionIndex].Content;

			string currentNormalized = NormalizeLineEndings(value ?? string.Empty);
			string savedNormalized = NormalizeLineEndings(savedContent ?? string.Empty);

			HasUnsavedChanges = !string.Equals(currentNormalized, savedNormalized, StringComparison.Ordinal);

			OnPropertyChanged(nameof(HasUnsavedChanges));
		}
	}

	private string NormalizeLineEndings(string input)
	{
		return input.Replace("\r\n", "\n").Replace("\r", "\n");
	}

	public bool RestoreOriginal()
	{
		var originalVersion = Versions.FirstOrDefault(v => v.IsOriginal);
		if (originalVersion != null)
		{
			Content = originalVersion.Content;
			CurrentVersionIndex = Versions.IndexOf(originalVersion);
			HasUnsavedChanges = false;

			OnPropertyChanged(nameof(CurrentVersionDescription));
			return true;
		}
		return false;
	}

	public string CurrentVersionDescription
	{
		get
		{
			if (CurrentVersionIndex >= 0 && CurrentVersionIndex < Versions.Count)
			{
				var version = Versions[CurrentVersionIndex];
				return $"{version.Description} ({version.Timestamp:HH:mm:ss})";
			}
			return "Sem versões";
		}
	}

	public bool IsCurrentVersionOriginal =>
		CurrentVersionIndex == 0 && Versions.Any() && Versions[0].IsOriginal;
}
