namespace ContextWinUI.Features.GraphParser.Models;
public class SearchSuggestion
{
	public string Title { get; set; } = string.Empty;       // Nome do arquivo
	public string Subtitle { get; set; } = string.Empty;    // Símbolos encontrados
	public string FilePath { get; set; } = string.Empty;    // Caminho completo
	public string Icon { get; set; } = "\uE943";            // Ícone
	public int MatchCount { get; set; } = 0;                // Número de símbolos encontrados
}