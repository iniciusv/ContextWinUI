using System.Collections.Generic;

namespace ContextWinUI.Models;

public class DatabaseConnectionDto
{
    public string Name { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string Provider { get; set; } = "System.Data.SqlClient"; // Future proofing
    
    // Cache de schemas descobertos para esta conexão
    public List<DatabaseSchemaDto> CachedSchemas { get; set; } = new();
}
