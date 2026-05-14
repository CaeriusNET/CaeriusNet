namespace CaeriusNet.Attributes.AutoContracts;

/// <summary>
///     Marks a result DTO emitted by AutoContracts from a SQL Server contract manifest.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AutoContractGenerateDtoAttribute : Attribute
{
    /// <summary>Gets or sets the SQL Server schema name.</summary>
    public string Schema { get; set; } = string.Empty;

    /// <summary>Gets or sets the SQL Server stored procedure name.</summary>
    public string Procedure { get; set; } = string.Empty;

    /// <summary>Gets or sets the zero-based result-set index.</summary>
    public int ResultSetIndex { get; set; }

    /// <summary>Gets or sets the manifest contract hash.</summary>
    public string ContractHash { get; set; } = string.Empty;
}
