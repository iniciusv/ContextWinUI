// ==================== C:\Users\vinic\source\repos\ContextWinUI\ContextWinUI\Models\FileSystemItem.cs ====================

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace ContextWinUI.Models;

public enum FileSystemItemType
{
	File,
	Directory,
	LogicalGroup, // Agrupadores como "Métodos", "Dependências"
	Method,       // O nó específico de um método
	Dependency    // O nó de um arquivo dependente
}

public partial class FileSystemItem : ObservableObject
{
	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private string fullPath = string.Empty;

	// Propriedade nova para identificar métodos
	[ObservableProperty]
	private FileSystemItemType type;

	// Armazena a assinatura para o Roslyn encontrar o método no arquivo
	public string? MethodSignature { get; set; }

	[ObservableProperty]
	private bool isExpanded;

	[ObservableProperty]
	private bool isSelected;

	[ObservableProperty]
	private bool isChecked;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Icon))]
	private string? customIcon;

	// --- CONTROLE DE BUSCA ---
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Visibility))]
	private bool isVisibleInSearch = true;

	public Visibility Visibility => IsVisibleInSearch ? Visibility.Visible : Visibility.Collapsed;

	// --- LÓGICA DO BOTÃO (+) (Aprofundar Arquivo) ---
	public bool CanDeepAnalyze => Type == FileSystemItemType.File && IsCodeFile;
	public Visibility DeepAnalyzeVisibility => CanDeepAnalyze ? Visibility.Visible : Visibility.Collapsed;

	// --- LÓGICA DO BOTÃO (>) (Fluxo do Método) ---
	// Só aparece se for do tipo Method e tivermos o caminho do arquivo pai
	public bool CanAnalyzeMethodFlow => Type == FileSystemItemType.Method && !string.IsNullOrEmpty(FullPath);
	public Visibility MethodFlowVisibility => CanAnalyzeMethodFlow ? Visibility.Visible : Visibility.Collapsed;

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> children = new();

	// Helpers de compatibilidade com código anterior
	public bool IsDirectory => Type == FileSystemItemType.Directory;

	public string Extension => IsDirectory ? string.Empty : Path.GetExtension(FullPath);
	public bool IsCodeFile => !IsDirectory && _codeExtensions.Contains(Extension);
	public long? FileSize { get; set; }
	public string FileSizeFormatted => FileSize.HasValue ? FormatBytes(FileSize.Value) : string.Empty;

	public string Icon
	{
		get
		{
			if (!string.IsNullOrEmpty(CustomIcon)) return CustomIcon;
			if (Type == FileSystemItemType.Directory) return "\uE8B7";
			if (Type == FileSystemItemType.Method) return "\uEA86";
			return GetFileIcon();
		}
	}

	private static readonly HashSet<string> _codeExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".cs", ".xaml", ".csproj", ".json", ".xml", ".js", ".ts", ".tsx", ".jsx",
		".html", ".css", ".scss", ".py", ".java", ".cpp", ".c", ".h", ".hpp",
		".go", ".rs", ".swift", ".kt", ".php", ".sql", ".sh", ".bat", ".ps1", ".md", ".txt", ".razor"
	};

	private string GetFileIcon()
	{
		return Extension.ToLower() switch
		{
			".cs" => "\uE943",
			".xaml" => "\uE8A5",
			".json" => "\uE8D2",
			".xml" => "\uE8A5",
			".txt" => "\uE8A5",
			".md" => "\uE8A5",
			_ => "\uE8A5"
		};
	}

	private static string FormatBytes(long bytes)
	{
		string[] sizes = { "B", "KB", "MB", "GB" };
		double len = bytes;
		int order = 0;
		while (len >= 1024 && order < sizes.Length - 1)
		{
			order++;
			len /= 1024;
		}
		return $"{len:0.##} {sizes[order]}";
	}

	public void SetExpansionRecursively(bool expanded)
	{
		IsExpanded = expanded;
		foreach (var child in Children)
		{
			if (child.IsDirectory || child.Children.Count > 0)
			{
				child.SetExpansionRecursively(expanded);
			}
		}
	}
}