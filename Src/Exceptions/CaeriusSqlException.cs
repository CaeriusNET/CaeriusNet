namespace CaeriusNet.Exceptions;

/// <summary>
///     Represents errors that occur during the execution of a SQL command within the CaeriusNet application.
/// </summary>
/// <remarks>
///     This exception is used to encapsulate database-related errors, providing specific context
///     for SQL command or stored procedure execution failures. It includes the original
///     <see cref="SqlException" /> as the inner exception for additional details about the failure.
/// </remarks>
/// <exception cref="SqlException">The inner exception is the underlying SQL exception that caused the error.</exception>
public sealed class CaeriusSqlException(string message, SqlException innerException)
    : Exception(message, innerException);