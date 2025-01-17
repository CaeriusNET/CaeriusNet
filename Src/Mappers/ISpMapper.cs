namespace CaeriusNet.Mappers;

/// <summary>
///     Represents a mechanism for mapping data from a <see cref="SqlDataReader" /> to an instance of a specified type.
/// </summary>
/// <typeparam name="T">The type of object to map the data to.</typeparam>
public interface ISpMapper<out T> where T : class
{
    /// <summary>
    ///     Maps data from a <see cref="SqlDataReader" /> to an instance of <typeparamref name="T" />.
    /// </summary>
    /// <param name="reader">The <see cref="SqlDataReader" /> containing the data to map.</param>
    /// <returns>An instance of <typeparamref name="T" /> populated with data from the reader.</returns>
    public static abstract T MapFromDataReader(SqlDataReader reader);
}