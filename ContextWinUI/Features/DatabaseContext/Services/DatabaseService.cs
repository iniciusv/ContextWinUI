using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.DatabaseContext.Services;

public class DatabaseService : IDatabaseService
{
    public async Task<bool> TestConnectionAsync(string connectionString, System.Threading.CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<List<DatabaseSchemaDto>> GetSchemaAsync(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var schemaQuery = @"
            SELECT 
                t.TABLE_SCHEMA AS [Schema],
                t.TABLE_NAME AS [TableName],
                c.COLUMN_NAME AS [ColumnName],
                c.DATA_TYPE AS [DataType],
                c.IS_NULLABLE AS [IsNullable],
                CASE WHEN k.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS [IsPrimaryKey]
            FROM INFORMATION_SCHEMA.TABLES t
            JOIN INFORMATION_SCHEMA.COLUMNS c 
                ON t.TABLE_SCHEMA = c.TABLE_SCHEMA 
                AND t.TABLE_NAME = c.TABLE_NAME
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) k 
                ON c.TABLE_SCHEMA = k.TABLE_SCHEMA 
                AND c.TABLE_NAME = k.TABLE_NAME 
                AND c.COLUMN_NAME = k.COLUMN_NAME
            WHERE t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION";

        var rawData = await connection.QueryAsync(schemaQuery);

        var grouped = rawData.GroupBy(r => new { Schema = r.Schema, TableName = r.TableName });
        
        var result = new List<DatabaseSchemaDto>();

        foreach (var group in grouped)
        {
            var dto = new DatabaseSchemaDto
            {
                Schema = group.Key.Schema,
                TableName = group.Key.TableName,
                Columns = group.Select(r => new DatabaseColumnDto
                {
                    Name = r.ColumnName,
                    DataType = r.DataType,
                    IsNullable = r.IsNullable == "YES",
                    IsPrimaryKey = r.IsPrimaryKey == 1
                }).ToList()
            };
            result.Add(dto);
        }

        return result;
    }

    public async Task<string> ExecuteQueryAsync(string connectionString, string query)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var result = await connection.QueryAsync(query);
            var rows = result.ToList();

            if (rows.Count == 0)
                return "No results.";

            var sb = new StringBuilder();

            // Get columns from first row (IDictionary<string, object>)
            var firstRow = (IDictionary<string, object>)rows[0];
            var columns = firstRow.Keys.ToList();

            // Header
            sb.AppendLine("| " + string.Join(" | ", columns) + " |");
            sb.AppendLine("| " + string.Join(" | ", columns.Select(_ => "---")) + " |");

            // Data
            foreach (var row in rows)
            {
                var dict = (IDictionary<string, object>)row;
                var values = columns.Select(c => 
                {
                    var val = dict[c];
                    return val?.ToString()?.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "") ?? "NULL";
                });
                sb.AppendLine("| " + string.Join(" | ", values) + " |");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error executing query: {ex.Message}";
        }
    }

    public async Task<List<Dictionary<string, object>>> GetRawDataAsync(string connectionString, string query)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var result = await connection.QueryAsync(query);
            var list = new List<Dictionary<string, object>>();

            foreach (var row in result)
            {
                var dict = (IDictionary<string, object>)row;
                list.Add(new Dictionary<string, object>(dict));
            }

            return list;
        }
        catch
        {
            return new List<Dictionary<string, object>>();
        }
    }
}
