using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Features.GraphParser.Models;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.ViewModels;

public partial class SegmentRowViewModel : ObservableObject
{
	// Lado Direito (Editável) - Sempre existe
	public CodeBlockItem Current { get; }
	public string? OriginalContent => Current.Versions.FirstOrDefault(v => v.IsOriginal)?.Content;

	// Lado Esquerdo (Referência) - Pode ser null (ex: bloco novo)
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasReference))]
	private CodeBlockItem? reference;

	// Controle de Visibilidade da Linha (para o filtro "Hide Unchanged")
	[ObservableProperty]
	private bool isVisible = true;

	public bool HasReference => Reference != null;

	public SegmentRowViewModel(CodeBlockItem current)
	{
		Current = current;
	}
}