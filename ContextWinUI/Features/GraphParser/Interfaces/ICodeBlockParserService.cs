using ContextWinUI.Features.GraphParser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser;
public interface ICodeBlockParserService
{
	Task<List<CodeBlockItem>> ParseFileAsync(string filePath, string fileContent);
}
