namespace CaeriusNet.Exceptions;

/// <summary>
///     Represents exceptions that occur during SQL operations within the CaeriusNet application.
/// </summary>
/// <remarks>
///     This exception is designed to provide clearer context for database-related failures,
///     specifically for SQL command executions or stored procedure interactions. The initial
///     <see cref="SqlException" /> is passed as the inner exception to provide detailed diagnostic information.
/// </remarks>
/// <exception cref="SqlException">Represents the original SQL exception encountered during the operation.</exception>
internal sealed class CaeriusNetSqlException(string message, SqlException innerException)
	: Exception(message, innerException);