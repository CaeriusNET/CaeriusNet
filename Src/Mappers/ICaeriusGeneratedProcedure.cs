namespace CaeriusNet.Mappers;

/// <summary>
///     Describes a stored procedure contract generated from SQL Server metadata.
/// </summary>
/// <typeparam name="TSelf">The generated procedure descriptor type.</typeparam>
public interface ICaeriusGeneratedProcedure<TSelf>
    where TSelf : struct, ICaeriusGeneratedProcedure<TSelf>
{
    /// <summary>Gets the SQL Server schema name.</summary>
    public static abstract string SchemaName { get; }

    /// <summary>Gets the SQL Server stored procedure name.</summary>
    public static abstract string ProcedureName { get; }

    /// <summary>Gets the fully qualified SQL Server stored procedure name.</summary>
    public static abstract string FullName { get; }

    /// <summary>Gets the stable contract hash from the manifest.</summary>
    public static abstract string ContractHash { get; }

    /// <summary>Gets the number of generated procedure parameters.</summary>
    public static abstract int ParameterCount { get; }

    /// <summary>Gets the number of generated result sets supported by this descriptor.</summary>
    public static abstract int ResultSetCount { get; }
}