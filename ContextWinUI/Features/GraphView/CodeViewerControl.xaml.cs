using ContextWinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Text;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.Features.GraphView
{
	public sealed partial class CodeViewerControl : UserControl
	{
		public static readonly DependencyProperty TextProperty =
			DependencyProperty.Register(nameof(Text), typeof(string), typeof(CodeViewerControl), new PropertyMetadata(string.Empty, OnContentChanged));

		public string Text
		{
			get => (string)GetValue(TextProperty);
			set => SetValue(TextProperty, value);
		}

		public static readonly DependencyProperty FileExtensionProperty =
			DependencyProperty.Register(nameof(FileExtension), typeof(string), typeof(CodeViewerControl), new PropertyMetadata(".txt", OnContentChanged));

		public string FileExtension
		{
			get => (string)GetValue(FileExtensionProperty);
			set => SetValue(FileExtensionProperty, value);
		}

		public static readonly DependencyProperty HighlighterStrategyProperty =
			DependencyProperty.Register(nameof(HighlighterStrategy), typeof(IHighlighterStrategy), typeof(CodeViewerControl), new PropertyMetadata(null, OnContentChanged));

		public IHighlighterStrategy HighlighterStrategy
		{
			get => (IHighlighterStrategy)GetValue(HighlighterStrategyProperty);
			set => SetValue(HighlighterStrategyProperty, value);
		}

		public CodeViewerControl()
		{
			this.InitializeComponent();
		}

		private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is CodeViewerControl control)
			{
				control.RenderContent();
			}
		}


		private void RenderContent()
		{
			CodeViewer.Blocks.Clear();
			string content = Text ?? string.Empty;

			UpdateLineNumbers(content);

			if (string.IsNullOrEmpty(content)) return;

			// Se tiver estratégia, usa. Se não, renderiza texto plano.
			if (HighlighterStrategy != null)
			{
				try
				{
					HighlighterStrategy.ApplyHighlighting(CodeViewer, content, FileExtension);
				}
				catch (Exception ex)
				{
					// Fallback se a estratégia falhar
					var p = new Paragraph();
					p.Inlines.Add(new Run { Text = content, Foreground = new SolidColorBrush(Colors.Red) });
					CodeViewer.Blocks.Add(p);
				}
			}
			else
			{
				// RENDERIZAÇÃO PADRÃO (Importante para quando acabou de colar e ainda não analisou)
				var p = new Paragraph();
				p.Inlines.Add(new Run { Text = content });
				CodeViewer.Blocks.Add(p);
			}
		}

		private void UpdateLineNumbers(string content)
		{
			if (string.IsNullOrEmpty(content))
			{
				LineNumbersDisplay.Text = "1";
				return;
			}

			int lineCount = content.Count(c => c == '\n') + 1;
			var sb = new StringBuilder();
			for (int i = 1; i <= lineCount; i++)
			{
				sb.AppendLine(i.ToString());
			}
			LineNumbersDisplay.Text = sb.ToString();
		}

		private void BtnCopy_Click(object sender, RoutedEventArgs e)
		{
			if (!string.IsNullOrEmpty(Text))
			{
				var package = new DataPackage();
				package.SetText(Text);
				Clipboard.SetContent(package);
			}
		}
	}
}