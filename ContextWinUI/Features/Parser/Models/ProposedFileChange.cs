using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.Linq;

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

	[ObservableProperty]
	private ObservableCollection<string> validationWarnings = new();

	[ObservableProperty]
	private bool isSnippet; // True se tiver "// ..."

	[ObservableProperty]
	private bool hasDestructiveComments; // True se tiver "// Deletar"

	public bool HasWarnings => ValidationWarnings.Any();

	public Microsoft.UI.Xaml.Media.Brush StatusColor
	{
		get
		{
			if (HasWarnings) return new SolidColorBrush(Colors.Orange);
			if (Status == "Aplicado") return new SolidColorBrush(Colors.Green);
			return new SolidColorBrush(Colors.Gray);
		}
	}

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