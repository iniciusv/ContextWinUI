using ContextWinUI.Features.GraphParser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser;

public interface IBlockLoaderService
{
	/// <summary>
	/// Lê o arquivo, faz o parsing dos blocos e recupera a indentação perdida,
	/// ajustando as posições absolutas para manter a integridade do highlight.
	/// </summary>
	Task<List<CodeBlockItem>> LoadAndProcessFileAsync(string filePath);
}