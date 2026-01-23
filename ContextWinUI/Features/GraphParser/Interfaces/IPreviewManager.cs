using ContextWinUI.Features.GraphParser.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.Interfaces;

public interface IPreviewManager : IDisposable
{
	/// <summary>
	/// Inicia o monitoramento da seleção de arquivos.
	/// </summary>
	/// <param name="viewModelContract">
	/// O contrato do ViewModel principal que possui o método OpenAsPreview.
	/// </param>
	void Start(IGraphParserContract viewModelContract);
}