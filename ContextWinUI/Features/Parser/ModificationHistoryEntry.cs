using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace ContextWinUI.Models; // Ajuste o namespace conforme seu projeto

public partial class ModificationHistoryEntry : ObservableObject
{
	public string FilePath { get; }
	public string PreviousContent { get; }
	public DateTime AppliedAt { get; }

	public string FileName => Path.GetFileName(FilePath);
	public string TimeString => AppliedAt.ToString("HH:mm:ss");

	public ModificationHistoryEntry(string filePath, string previousContent)
	{
		FilePath = filePath;
		PreviousContent = previousContent;
		AppliedAt = DateTime.Now;
	}
}