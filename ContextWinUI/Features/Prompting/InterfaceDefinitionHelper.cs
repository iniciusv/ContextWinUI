using System;
using System.Linq;
using System.Text;

namespace ContextWinUI.Features.Prompting;

public static class InterfaceDefinitionHelper
{
	public static string GenerateInterfaceDefinition<T>()
	{
		var type = typeof(T);
		var sb = new StringBuilder();

		sb.AppendLine($"public interface {type.Name}");
		sb.AppendLine("{");

		foreach (var prop in type.GetProperties())
		{
			var typeName = GetFriendlyTypeName(prop.PropertyType);
			sb.AppendLine($"    {typeName} {prop.Name} {{ get; }}");
		}

		sb.AppendLine("}");
		return sb.ToString();
	}

	private static string GetFriendlyTypeName(Type type)
	{
		if (type.IsGenericType)
		{
			var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
			var cleanName = type.Name.Split('`')[0];
			return $"{cleanName}<{genericArgs}>";
		}
		return type.Name;
	}
}