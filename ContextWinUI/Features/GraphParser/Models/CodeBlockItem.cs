// ARQUIVO: CodeBlockItem.cs
using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ContextWinUI.Features.GraphParser.Models;


// 3. A Classe Principal do Bloco
public partial class CodeBlockItem : ObservableObject
{
	// --- Identidade ---
	public string Id { get; set; } = Guid.NewGuid().ToString();

	[ObservableProperty]
	private string name = string.Empty;

	// --- Tipagem ---
	// Define O QUE é semanticamente (para Ícones e Cores)
	public SymbolType SymbolType { get; set; }

	// Define COMO o parser dividiu (para comportamento de edição)
	public SegmentType SegmentType { get; set; }


	public int AbsoluteStartPosition { get; set; }
	public int StartLine { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
	private string content = string.Empty;

	public int DepthLevel { get; set; } = 0;
	public string FileExtension { get; set; } = ".cs";

	[ObservableProperty]
	private string typeDescription = string.Empty;

	// --- Controle de Diff Visual ---
	// Usado pelo ViewModel para filtrar o que aparece na lista
	[ObservableProperty]
	private bool isVisibleInDiff = true;

	// --- Histórico de Versões ---
	public ObservableCollection<CodeBlockVersion> Versions { get; private set; } = new();

	// Índice da versão atualmente carregada (geralmente a última)
	public int CurrentVersionIndex { get; private set; } = -1;

	// --- Propriedades Calculadas ---

	// Define se o bloco é "interessante" o suficiente para ser editado isoladamente.
	// Blocos estruturais (usings, namespace) ou trivia geralmente não são.
	public bool IsGranular => SegmentType == SegmentType.Method ||
							  SegmentType == SegmentType.Property ||
							  SegmentType == SegmentType.Class ||
							  SegmentType == SegmentType.Comment;

	public bool HasUnsavedChanges
	{
		get
		{
			if (Versions.Count == 0) return !string.IsNullOrEmpty(Content);

			// Compara o conteúdo atual com a última versão salva
			// (Assumindo que a última da lista é o estado "salvo" mais recente)
			var lastSaved = Versions.Last();
			return Content != lastSaved.Content;
		}
	}

	// --- Lógica Visual (Ícones e Cores baseados no SymbolType) ---

	// Fonte: Segoe MDL2 Assets
	public string Icon => SymbolType switch
	{
		SymbolType.Class => "\uEA86",       // Class Icon
		SymbolType.Interface => "\uE943",   // Interface/Abstract
		SymbolType.Method => "\uEA37",      // Cube/Method
		SymbolType.Property => "\uEA39",    // Wrench/Property
		SymbolType.Field => "\uEA38",       // Field
		SymbolType.Constructor => "\uEA8C", // Constructor
		SymbolType.Struct => "\uEA86",      // Struct (usando Class)
		SymbolType.Enum => "\uE8FD",        // List

		// Granulares
		SymbolType.ControlFlow => "\uE8A1", // Shuffle/Flow
		SymbolType.LocalVariable => "\uE71D", // Variable
		SymbolType.StringLiteral => "\uE8C8", // Font

		_ => "\uE82D" // Code/Script Genérico
	};

	public SolidColorBrush HeaderBrush => SymbolType switch
	{
		SymbolType.Class => new SolidColorBrush(Colors.Orange),
		SymbolType.Interface => new SolidColorBrush(Colors.LightGreen),
		SymbolType.Method => new SolidColorBrush(Colors.MediumPurple),
		SymbolType.Property => new SolidColorBrush(Colors.CornflowerBlue),
		SymbolType.Constructor => new SolidColorBrush(Colors.Gold),
		SymbolType.Field => new SolidColorBrush(Colors.CadetBlue),
		SymbolType.ControlFlow => new SolidColorBrush(Colors.LightGray),
		_ => new SolidColorBrush(Colors.Gray)
	};

	// --- Métodos de Gerenciamento de Versão ---

	public void InitializeVersions(string initialContent)
	{
		Versions.Clear();
		Versions.Add(new CodeBlockVersion
		{
			Content = initialContent,
			Description = "Original",
			IsOriginal = true,
			Timestamp = DateTime.MinValue // Marca como início absoluto
		});

		// Não define o Content aqui propositalmente se você quiser 
		// criar um bloco "Novo" que já nasce modificado.
		CurrentVersionIndex = 0;
	}

	public void CreateNewVersion(string newContent, string description, DateTime timestamp)
	{
		Versions.Add(new CodeBlockVersion
		{
			Content = newContent,
			Description = description,
			IsOriginal = false,
			Timestamp = timestamp
		});

		// Atualiza o ponteiro
		CurrentVersionIndex = Versions.Count - 1;

		// Sincroniza o conteúdo atual para bater com a nova versão
		Content = newContent;

		// Notifica a UI que o estado "Unsaved" mudou (agora está salvo)
		OnPropertyChanged(nameof(HasUnsavedChanges));
	}

	public void RestoreVersion(int index)
	{
		if (index >= 0 && index < Versions.Count)
		{
			Content = Versions[index].Content;
			CurrentVersionIndex = index;
			OnPropertyChanged(nameof(HasUnsavedChanges));
		}
	}

	public bool RestoreOriginal()
	{
		var original = Versions.FirstOrDefault(v => v.IsOriginal);
		if (original != null)
		{
			Content = original.Content;
			// Não mudamos o CurrentVersionIndex para 0 necessariamente,
			// pois queremos indicar que o TEXTO voltou ao original,
			// mas o usuário ainda não "Salvou" essa reversão como uma nova versão.
			// Mas visualmente, HasUnsavedChanges ficará false se bater com a última.
			OnPropertyChanged(nameof(HasUnsavedChanges));
			return true;
		}
		return false;
	}

	// --- Clone (Essencial para a Coluna de Referência) ---
	public CodeBlockItem Clone()
	{
		var newItem = new CodeBlockItem
		{
			Id = this.Id,
			Name = this.Name,
			SymbolType = this.SymbolType,
			SegmentType = this.SegmentType,
			Content = this.Content,
			DepthLevel = this.DepthLevel,
			FileExtension = this.FileExtension,
			TypeDescription = this.TypeDescription,
			// Importante: Não clonamos a referência da coleção, criamos uma nova
			// para que a UI de referência não afete a de edição se algo bizarro acontecer.
		};

		foreach (var v in this.Versions)
		{
			newItem.Versions.Add(v.Clone());
		}

		return newItem;
	}
}