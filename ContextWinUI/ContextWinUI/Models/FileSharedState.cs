using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;

namespace ContextWinUI.Models;

/// <summary>
/// FLYWEIGHT: Representa o estado intrínseco e compartilhado de um arquivo.
/// Existe apenas UMA instância desta classe por caminho de arquivo em toda a aplicação.
/// </summary>
public partial class FileSharedState : ObservableObject
{
	[ObservableProperty]
	private string fullPath = string.Empty;

	[ObservableProperty]
	private string name = string.Empty;

	// Esta é a propriedade mágica. Mudou aqui, reflete em todas as árvores.
	[ObservableProperty]
	private bool isChecked;

	[ObservableProperty]
	private long? fileSize;

	// Cache de conteúdo (opcional, para não ler disco repetidamente)
	public string? ContentCache { get; set; }

	public string Extension => Path.GetExtension(FullPath);

	public FileSharedState(string fullPath)
	{
		FullPath = fullPath;
		Name = Path.GetFileName(fullPath);
	}
}