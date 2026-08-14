using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CaeriusNet.SqlServer.Contracts;

internal static class ContractHasher
{
    internal static string HashTableType(
        string schema,
        string name,
        string clrName,
        IReadOnlyList<ColumnContract> columns)
    {
        var builder = new StringBuilder();
        AppendValue(builder, "table-type");
        AppendValue(builder, schema);
        AppendValue(builder, name);
        AppendValue(builder, clrName);
        foreach (var column in columns.OrderBy(column => column.Ordinal))
            AppendColumn(builder, column);

        return Hash(builder);
    }

    internal static string HashProcedure(
        string schema,
        string name,
        string clrName,
        string parametersClrName,
        IReadOnlyList<ParameterContract> parameters,
        IReadOnlyList<ResultSetContract> resultSets)
    {
        var builder = new StringBuilder();
        AppendValue(builder, "procedure");
        AppendValue(builder, schema);
        AppendValue(builder, name);
        AppendValue(builder, clrName);
        AppendValue(builder, parametersClrName);
        foreach (var parameter in parameters.OrderBy(parameter => parameter.Ordinal))
            AppendParameter(builder, parameter);

        foreach (var resultSet in resultSets.OrderBy(resultSet => resultSet.Index))
            AppendResultSet(builder, resultSet);

        return Hash(builder);
    }

    private static string Hash(StringBuilder builder)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void AppendParameter(StringBuilder builder, ParameterContract parameter)
    {
        AppendValue(builder, parameter.Ordinal);
        AppendValue(builder, parameter.Name);
        AppendValue(builder, parameter.SqlType);
        AppendValue(builder, parameter.ClrType);
        AppendValue(builder, parameter.IsTableType);
        AppendValue(builder, parameter.Direction);
        AppendValue(builder, parameter.Nullable);
        AppendValue(builder, parameter.MaxLength);
        AppendValue(builder, parameter.Precision);
        AppendValue(builder, parameter.Scale);
    }

    internal static string HashResultSet(
        int index,
        string? clrName,
        string status,
        string? errorMessage,
        IReadOnlyList<ColumnContract> columns)
    {
        var builder = new StringBuilder();
        AppendResultSet(builder, new ResultSetContract(index, clrName, status, columns, errorMessage, string.Empty));
        return Hash(builder);
    }

    private static void AppendResultSet(StringBuilder builder, ResultSetContract resultSet)
    {
        AppendValue(builder, "result-set");
        AppendValue(builder, resultSet.Index);
        AppendValue(builder, resultSet.ClrName);
        AppendValue(builder, resultSet.Status);
        AppendValue(builder, resultSet.ErrorMessage);
        foreach (var column in resultSet.Columns.OrderBy(column => column.Ordinal))
            AppendColumn(builder, column);
    }

    private static void AppendColumn(StringBuilder builder, ColumnContract column)
    {
        AppendValue(builder, column.Ordinal);
        AppendValue(builder, column.Name);
        AppendValue(builder, column.SqlType);
        AppendValue(builder, column.ClrType);
        AppendValue(builder, column.Nullable);
        AppendValue(builder, column.MaxLength);
        AppendValue(builder, column.Precision);
        AppendValue(builder, column.Scale);
    }

    private static void AppendValue(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("null;");
            return;
        }

        builder
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append(';');
    }

    private static void AppendValue(StringBuilder builder, int? value)
    {
        AppendValue(builder, value?.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendValue(StringBuilder builder, byte? value)
    {
        AppendValue(builder, value?.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendValue(StringBuilder builder, bool value)
    {
        AppendValue(builder, value ? "true" : "false");
    }
}
