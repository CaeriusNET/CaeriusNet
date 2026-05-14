namespace CaeriusNet.Tests.Helpers;

/// <summary>
///     Helper to forge a <see cref="SqlException" /> instance from a string message — needed because
///     <see cref="SqlException" /> has no public constructor. Used by transaction unit tests to drive
///     the failure paths without standing up a real SQL Server.
/// </summary>
internal static class SqlExceptionFactory
{
    private static readonly Func<string, SqlException> Factory = BuildFactory();

    public static SqlException Create(string message)
    {
        return Factory(message);
    }

    private static Func<string, SqlException> BuildFactory()
    {
        var collectionCtor = typeof(SqlErrorCollection)
            .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!;

        var addMethod = typeof(SqlErrorCollection).GetMethod("Add",
            BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(SqlError)], null)!;

        // SqlError(int infoNumber, byte errorState, byte errorClass, string server, string errorMessage,
        //         string procedure, int lineNumber, Exception? exception = null)
        var sqlErrorCtor = typeof(SqlError).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            [
                typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int),
                typeof(Exception)
            ],
            null);

        var exceptionCreate = typeof(SqlException).GetMethod("CreateException",
            BindingFlags.Static | BindingFlags.NonPublic, null,
            [typeof(SqlErrorCollection), typeof(string)],
            null)!;

        return message =>
        {
            var collection = (SqlErrorCollection)collectionCtor.Invoke(null)!;
            if (sqlErrorCtor is not null)
            {
                var error = sqlErrorCtor.Invoke(
                    [50000, (byte)0, (byte)10, "caerius-tests", message, string.Empty, 0, null!])!;
                addMethod.Invoke(collection, [error]);
            }

            return (SqlException)exceptionCreate.Invoke(null, [collection, "0.0.0.0"])!;
        };
    }
}
