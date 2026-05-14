namespace CaeriusNet.Attributes.AutoContracts;

/// <summary>
///     Marks a TVP row type emitted by AutoContracts from a SQL Server contract manifest.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class AutoContractGenerateTvpAttribute : Attribute
{
    /// <summary>Gets or sets the SQL Server schema name.</summary>
    public string Schema { get; set; } = string.Empty;

    /// <summary>Gets or sets the SQL Server user-defined table type name.</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Gets or sets the manifest contract hash.</summary>
    public string ContractHash { get; set; } = string.Empty;
}
