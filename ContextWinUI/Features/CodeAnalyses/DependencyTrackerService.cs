using ContextWinUI.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.CodeAnalyses;

public class DependencyTrackerService
{
	public HashSet<string> GetDeepDependencies(DependencyGraph graph, string startNodeId)
	{
		var visited = new HashSet<string>();
		var queue = new Queue<string>();

		queue.Enqueue(startNodeId);

		while (queue.Count > 0)
		{
			var currentId = queue.Dequeue();
			if (!visited.Add(currentId)) continue;

			if (graph.Nodes.TryGetValue(currentId, out var node))
			{
				foreach (var link in node.OutgoingLinks)
				{
					// Lógica de Filtro: O que conta como "dependência recursiva"?
					// Geralmente queremos seguir Chamadas e Implementações.
					// Propriedades e Tipos Complexos (UsesType) geralmente pegamos apenas o nível 1
					// para não trazer o projeto inteiro.

					if (link.Type == LinkType.Calls || link.Type == LinkType.Implements)
					{
						if (!visited.Contains(link.TargetId))
							queue.Enqueue(link.TargetId);
					}
					else if (link.Type == LinkType.UsesType || link.Type == LinkType.Accesses)
					{
						// Adiciona ao resultado, mas talvez não continue a recursão profunda
						// para evitar "explosão de contexto"
						visited.Add(link.TargetId);
					}
				}
			}
		}
		return visited;
	}
}