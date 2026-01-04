using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ContextWinUI.Features.GraphParser.ViewModels;
using ContextWinUI.Features.GraphParser.IAParser; // Importante para ver o AiPreviewViewModel

namespace ContextWinUI.Features.GraphParser;

public class TabHeaderTemplateSelector : DataTemplateSelector
{
	public DataTemplate FileHeaderTemplate { get; set; }
	public DataTemplate PreviewHeaderTemplate { get; set; }

	protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
	{
		if (item is FileSegmentsViewModel) return FileHeaderTemplate;
		if (item is AiPreviewViewModel) return PreviewHeaderTemplate;

		return base.SelectTemplateCore(item, container);
	}
}