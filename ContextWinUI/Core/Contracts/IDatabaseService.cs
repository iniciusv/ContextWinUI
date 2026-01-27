using ContextWinUI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;

public interface IDatabaseService
{
    Task<bool> TestConnectionAsync(string connectionString);
    Task<List<DatabaseSchemaDto>> GetSchemaAsync(string connectionString);
    Task<string> ExecuteQueryAsync(string connectionString, string query);
    Task<List<Dictionary<string, object>>> GetRawDataAsync(string connectionString, string query);
}
