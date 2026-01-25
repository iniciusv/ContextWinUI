using ContextWinUI.Features.GraphParser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;
public interface ICodeBlockParserService
{
	Task<List<CodeBlockItem>> ParseFileAsync(string filePath, string fileContent);
    List<string> FindReferences(string codeContent, IEnumerable<string> knownSymbols);
}
