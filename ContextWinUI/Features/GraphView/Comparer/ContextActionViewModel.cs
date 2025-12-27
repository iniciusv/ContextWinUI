// ARQUIVO: ContextActionViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Features.GraphView;

namespace ContextWinUI.Features.GraphView;

public partial class ContextActionViewModel : ObservableObject
{
	public ScopeMatch Match { get; }
	public int LineNumber { get; }
	// Cálculo do posicionamento vertical (20px por linha + ajuste de margem)
	public double VerticalOffset => (LineNumber - 1) * 20 + 12;

	[ObservableProperty] private string selectedAction = "V"; // "V", "+", "*"

	public ContextActionViewModel(ScopeMatch match, int lineNumber)
	{
		Match = match;
		LineNumber = lineNumber;
	}
}