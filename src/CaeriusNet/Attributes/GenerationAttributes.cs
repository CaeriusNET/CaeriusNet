using System.Data.SqlTypes;

namespace CaeriusNet;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateDtoAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateTvpAttribute : Attribute
{
    public string Schema { get; init; } = "dbo";
    public required string TvpName { get; init; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AutoContractGenerateDtoAttribute : Attribute
{
    public string Schema { get; init; } = string.Empty;
    public string Procedure { get; init; } = string.Empty;
    public int ResultSetIndex { get; init; }
    public string ContractHash { get; init; } = string.Empty;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class AutoContractGenerateTvpAttribute : Attribute
{
    public string Schema { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public string ContractHash { get; init; } = string.Empty;
}

[AttributeUsage(AttributeTargets.Struct)]
public sealed class AutoContractGenerateProcedureAttribute : Attribute
{
    public string Schema { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ContractHash { get; init; } = string.Empty;
}

/// <summary>Declares the exact SQL Server metadata for one manually generated TVP column.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SqlServerTvpColumnAttribute(
    string name,
    int ordinal,
    SqlDbType sqlDbType,
    bool isNullable) : Attribute
{
    public string Name { get; } = name;
    public int Ordinal { get; } = ordinal;
    public SqlDbType SqlDbType { get; } = sqlDbType;
    public bool IsNullable { get; } = isNullable;
    public long MaxLength { get; init; } = long.MinValue;
    public byte Precision { get; init; } = byte.MaxValue;
    public byte Scale { get; init; } = byte.MaxValue;
    public long LocaleId { get; init; } = -1;

    public SqlCompareOptions CompareOptions { get; init; } =
        (SqlCompareOptions)(-1);
}
