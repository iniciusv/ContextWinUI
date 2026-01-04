using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Features.GraphParser.IAParser;
using ContextWinUI.Features.GraphParser.ViewModels;
using System.Collections.ObjectModel;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxTokenParser;

namespace ContextWinUI.Features.GraphParser;


public partial class AiPreviewViewModel : ObservableObject
{
	private readonly GraphParserViewModel _parentViewModel;

	public string Title => "Análise IA (Preview)";

	[ObservableProperty]
	private AiMergeResult result;

	// Lista simplificada apenas para visualização do que vai acontecer
	public ObservableCollection<SegmentRowViewModel> PreviewRows { get; } = new();

	public AiPreviewViewModel(AiMergeResult result, GraphParserViewModel parentViewModel)
	{
		Result = result;
		_parentViewModel = parentViewModel;

		// Popula a visualização com os blocos resultantes do merge em memória
		foreach (var block in result.MergedBlocks)
		{
			// Marcamos como visível apenas o que foi alterado ou adicionado para focar no debug
			var row = new SegmentRowViewModel(block);
			bool isModified = block.HasUnsavedChanges || block.Versions.Count > 1 || block.TypeDescription.Contains("(Novo)");

			// Opcional: Mostrar tudo ou apenas modificados. Aqui mostramos tudo para contexto.
			PreviewRows.Add(row);
		}
	}

	[RelayCommand]
	public void ConfirmAndApply()
	{
		// 1. Chama o pai para abrir o arquivo real
		_parentViewModel.OpenFileCommand.Execute(Result.FilePath);

		// 2. Encontra a aba recém aberta
		var realTab = _parentViewModel.Tabs.OfType<FileSegmentsViewModel>()
						.FirstOrDefault(t => t.FilePath == Result.FilePath);

		if (realTab != null)
		{
			// 3. Aplica os blocos que já foram calculados
			realTab.ApplyExternalMerge(Result.MergedBlocks);
		}

		// 4. Fecha esta aba de preview
		_parentViewModel.CloseTabCommand.Execute(this);
	}

	[RelayCommand]
	public void Cancel()
	{
		_parentViewModel.CloseTabCommand.Execute(this);
	}
}