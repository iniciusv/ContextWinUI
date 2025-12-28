using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.Parser;
public enum MatchType
{
	Modification, // Atualizar método existente
	Insertion,    // Criar novo método
	Deletion      // (Raramente usado em Paste, mas bom ter)
}
