using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Shared;


public static class TextIndexMapper
{
	/// <summary>
	/// Converte um índice do Editor (WinUI/RichEditBox - \r) para o índice do Roslyn (Arquivo Físico - \r\n).
	/// Usado no OnCaretMoved.
	/// </summary>
	public static int VisualToPhysical(string textWithCRLF, int visualIndex)
	{
		if (string.IsNullOrEmpty(textWithCRLF)) return visualIndex;

		int physicalIdx = 0;
		int visualCounter = 0;

		while (physicalIdx < textWithCRLF.Length && visualCounter < visualIndex)
		{
			// Detecta padrão Windows CRLF (\r\n) -> Visual +1, Físico +2
			if (physicalIdx + 1 < textWithCRLF.Length &&
				textWithCRLF[physicalIdx] == '\r' &&
				textWithCRLF[physicalIdx + 1] == '\n')
			{
				physicalIdx += 2;
				visualCounter++;
			}
			// Fallback: Se for apenas \r ou apenas \n -> Visual +1, Físico +1
			// (Isso protege caso a normalização falhe em algum ponto)
			else
			{
				physicalIdx++;
				visualCounter++;
			}
		}
		return physicalIdx;
	}

	/// <summary>
	/// Converte um índice do Roslyn (Arquivo Físico - \r\n) para o índice do Editor (WinUI - \r).
	/// Usado no ApplyHybridHighlights.
	/// </summary>
	public static int PhysicalToVisual(string textWithCRLF, int physicalIndex)
	{
		if (string.IsNullOrEmpty(textWithCRLF)) return physicalIndex;

		int visualIdx = 0;
		int physicalCounter = 0;

		while (physicalCounter < physicalIndex && physicalCounter < textWithCRLF.Length)
		{
			if (physicalCounter + 1 < textWithCRLF.Length &&
				textWithCRLF[physicalCounter] == '\r' &&
				textWithCRLF[physicalCounter + 1] == '\n')
			{
				// Encontrou CRLF no original: 
				// Avança 2 no físico
				physicalCounter += 2;
				// Avança apenas 1 no visual (O editor fundiu o \r\n em um único \r)
				visualIdx++;
			}
			else
			{
				physicalCounter++;
				visualIdx++;
			}
		}
		return visualIdx;
	}
}