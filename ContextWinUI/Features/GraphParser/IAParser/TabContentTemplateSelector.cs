using ContextWinUI.Features.GraphParser.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ContextWinUI.Features.GraphParser;

public class TabContentTemplateSelector : DataTemplateSelector
{
	public DataTemplate FileTemplate { get; set; }
	public DataTemplate PreviewTemplate { get; set; }

	protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
	{
		if (item is FileSegmentsViewModel) return FileTemplate;
		if (item is AiPreviewViewModel) return PreviewTemplate;

		return base.SelectTemplateCore(item, container);
	}
}
