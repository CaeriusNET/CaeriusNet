namespace CaeriusNet;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateDtoAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
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

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
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
