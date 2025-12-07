using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml; // Necessário para Visibility
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace ContextWinUI.Models;

public partial class FileSystemItem : ObservableObject
{
	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private string fullPath = string.Empty;

	[ObservableProperty]
	private bool isDirectory;

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

	// Binding direto no XAML para ocultar itens filtrados sem removê-los da lista
	public Visibility Visibility => IsVisibleInSearch ? Visibility.Visible : Visibility.Collapsed;

	// --- LÓGICA DO BOTÃO (+) ---
	// Só mostramos o botão se for um arquivo físico de código (não pasta, nem agrupador lógico)
	public bool CanDeepAnalyze => !IsDirectory && !string.IsNullOrEmpty(FullPath) && IsCodeFile;

	public Visibility DeepAnalyzeVisibility => CanDeepAnalyze ? Visibility.Visible : Visibility.Collapsed;

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> children = new();

	public string Extension => IsDirectory ? string.Empty : Path.GetExtension(FullPath);
	public bool IsCodeFile => !IsDirectory && _codeExtensions.Contains(Extension);
	public long? FileSize { get; set; }
	public string FileSizeFormatted => FileSize.HasValue ? FormatBytes(FileSize.Value) : string.Empty;

	public string Icon
	{
		get
		{
			if (!string.IsNullOrEmpty(CustomIcon)) return CustomIcon;
			return IsDirectory ? "\uE8B7" : GetFileIcon();
		}
	}

	private static readonly HashSet<string> _codeExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".cs", ".xaml", ".csproj", ".json", ".xml", ".js", ".ts", ".tsx", ".jsx",
		".html", ".css", ".scss", ".py", ".java", ".cpp", ".c", ".h", ".hpp",
		".go", ".rs", ".swift", ".kt", ".php", ".sql", ".sh", ".bat", ".ps1", ".md", ".txt"
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
}