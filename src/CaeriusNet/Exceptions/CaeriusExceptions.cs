namespace CaeriusNet.Exceptions;

public sealed class CaeriusNetSqlException : Exception
{
    internal CaeriusNetSqlException(string procedure, SqlException innerException)
        : base($"SQL Server failed while executing stored procedure '{procedure}'.", innerException)
    {
        Procedure = procedure;
        Number = innerException.Number;
        State = innerException.State;
        Class = innerException.Class;
        ClientConnectionId = innerException.ClientConnectionId;
    }

    public string Procedure { get; }
    public int Number { get; }
    public byte State { get; }
    public byte Class { get; }
    public Guid ClientConnectionId { get; }
}

public sealed class CaeriusContractException : Exception
{
    public CaeriusContractException(string message) : base(message)
    {
    }

    public CaeriusContractException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class CaeriusMappingException : Exception
{
    public CaeriusMappingException(string procedure, int resultSetIndex, long rowIndex, Exception innerException)
        : base($"Mapping failed for '{procedure}', result set {resultSetIndex}, row {rowIndex}.", innerException)
    {
        Procedure = procedure;
        ResultSetIndex = resultSetIndex;
        RowIndex = rowIndex;
    }

    public string Procedure { get; }
    public int ResultSetIndex { get; }
    public long RowIndex { get; }
}

public sealed class CaeriusCacheException : Exception
{
    public CaeriusCacheException(string operation, Exception innerException)
        : base($"The strict cache operation '{operation}' failed.", innerException)
    {
        Operation = operation;
    }

    public string Operation { get; }
}

/// <summary>
///     Indicates that SQL Server completed the requested durable operation, but the mandatory
///     follow-up cache invalidation failed. Retrying the stored procedure may therefore duplicate
///     a non-idempotent write.
/// </summary>
public sealed class CaeriusCacheInvalidationException : Exception
{
    internal CaeriusCacheInvalidationException(Exception innerException)
        : base(
            "SQL Server completed the operation, but cache invalidation failed. Do not blindly retry a non-idempotent stored procedure.",
            innerException)
    {
        SqlWasCommitted = true;
    }

    public bool SqlWasCommitted { get; }
}
