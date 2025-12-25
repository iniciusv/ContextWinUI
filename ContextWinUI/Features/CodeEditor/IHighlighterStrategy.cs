// ARQUIVO: IHighlighterStrategy.cs
using Microsoft.UI.Xaml.Controls;

namespace ContextWinUI.Services;

public interface IHighlighterStrategy
{
	/// <summary>
	/// Aplica a lógica de coloração ao RichTextBlock.
	/// </summary>
	/// <param name="richTextBlock">O controle visual alvo.</param>
	/// <param name="content">O texto do código a ser processado.</param>
	/// <param name="contextParam">Parâmetro de contexto (ex: extensão do arquivo ou ID do nó).</param>
	void ApplyHighlighting(RichTextBlock richTextBlock, string content, string contextParam);
}