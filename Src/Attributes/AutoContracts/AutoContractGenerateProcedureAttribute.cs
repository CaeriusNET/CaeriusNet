namespace CaeriusNet.Attributes.AutoContracts;

/// <summary>
///     Marks a stored procedure descriptor emitted by AutoContracts from a SQL Server contract manifest.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class AutoContractGenerateProcedureAttribute : Attribute
{
    /// <summary>Gets or sets the SQL Server schema name.</summary>
    public string Schema { get; set; } = string.Empty;

    /// <summary>Gets or sets the SQL Server stored procedure name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the manifest contract hash.</summary>
    public string ContractHash { get; set; } = string.Empty;
}