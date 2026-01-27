using System.Collections.Generic;

namespace ContextWinUI.Models;

public class DatabaseSchemaDto
{
    public string TableName { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public List<DatabaseColumnDto> Columns { get; set; } = new();
}

public class DatabaseColumnDto
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
}
