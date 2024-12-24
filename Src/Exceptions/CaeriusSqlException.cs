namespace CaeriusNet.Exceptions;

public sealed class CaeriusSqlException(string message, SqlException innerException)
    : Exception(message, innerException);