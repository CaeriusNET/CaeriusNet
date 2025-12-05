namespace CaeriusNet.Exceptions;

/// <summary>
///     Represents errors that occur during SQL operations within the CaeriusNet application.
/// </summary>
/// <remarks>
///     This exception is designed to provide clearer context for database-related failures,
///     specifically for SQL command executions or stored procedure interactions. The initial
///     <see cref="SqlException" /> is passed as the inner exception to provide detailed diagnostic information.
/// </remarks>
/// <param name="message">The message that describes the error.</param>
/// <param name="innerException">The <see cref="SqlException" /> that is the cause of the current exception.</param>
/// <exception cref="SqlException">Represents the original SQL exception encountered during the operation.</exception>
internal sealed class CaeriusNetSqlException(string message, SqlException innerException)
    : Exception(message, innerException);