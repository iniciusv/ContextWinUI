using CommunityToolkit.Mvvm.ComponentModel;

namespace ContextWinUI.Models;

public partial class ProposedFileChange : ObservableObject
{
	[ObservableProperty]
	private string filePath = string.Empty;

	[ObservableProperty]
	private string originalContent = string.Empty;

	[ObservableProperty]
	private string newContent = string.Empty;

	[ObservableProperty]
	private bool isSelected = true;

	[ObservableProperty]
	private string status = "Pendente"; // Pendente, Aplicado, Erro

	public string FileName => System.IO.Path.GetFileName(FilePath);

	// Calcula estatísticas simples de mudança
	public string ChangeSummary
	{
		get
		{
			if (string.IsNullOrEmpty(OriginalContent)) return "Novo Arquivo";
			var oldLines = OriginalContent.Split('\n').Length;
			var newLines = NewContent.Split('\n').Length;
			var diff = newLines - oldLines;
			return $"{newLines} linhas ({(diff >= 0 ? "+" : "")}{diff})";
		}
	}
}