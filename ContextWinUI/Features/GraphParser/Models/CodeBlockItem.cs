using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ContextWinUI.Features.GraphParser.Models;

public partial class CodeBlockItem : ObservableObject
{
	// --- Identidade e Metadados ---
	public string Id { get; set; } = Guid.NewGuid().ToString();

	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private string typeDescription = string.Empty;

	[ObservableProperty]
	private bool isSelected;

	public SymbolType SymbolType { get; set; }
	public SegmentType SegmentType { get; set; }

	public int SignatureHash { get; set; }

	// --- Posicionamento e Arquivo ---
	public int AbsoluteStartPosition { get; set; }
	public int StartLine { get; set; }
	public int DepthLevel { get; set; } = 0;
	public string FileExtension { get; set; } = ".cs";
	public DateTime Timestamp { get; set; } = DateTime.Now;
	public int CurrentVersionIndex { get; set; } = -1;
	public string Signature { get; set; } = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
	private string content = string.Empty;

	[ObservableProperty]
	private bool isVisibleInDiff = true;

	public ObservableCollection<CodeBlockVersion> Versions { get; private set; } = new();

	public bool IsGranular => SegmentType == SegmentType.Method ||
							  SegmentType == SegmentType.Property ||
							  SegmentType == SegmentType.Class ||
							  SegmentType == SegmentType.Comment;

	public bool HasUnsavedChanges
	{
		get
		{
			var original = Versions.FirstOrDefault(v => v.IsOriginal);
			if (original != null)
			{
				return Content != original.Content;
			}

			if (Versions.Count == 0) return !string.IsNullOrEmpty(Content);
			return Content != Versions.Last().Content;
		}
	}

	public void InitializeVersions(string initialContent)
	{
		Versions.Clear();
		Versions.Add(new CodeBlockVersion
		{
			Content = initialContent,
			Description = "Original",
			IsOriginal = true,
			Timestamp = DateTime.MinValue
		});
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
		CurrentVersionIndex = Versions.Count - 1;
		Content = newContent;
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
			OnPropertyChanged(nameof(HasUnsavedChanges));
			return true;
		}
		return false;
	}

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
		};

		foreach (var v in this.Versions)
		{
			newItem.Versions.Add(v.Clone());
		}

		return newItem;
	}
}