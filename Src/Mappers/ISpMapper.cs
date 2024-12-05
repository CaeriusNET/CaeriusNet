namespace CaeriusNet.Mappers;

/// <summary>
///     Defines a method for mapping data from a <see cref="SqlDataReader" /> to a specific type.
/// </summary>
/// <typeparam name="T">The type of object to map the data to.</typeparam>
public interface ISpMapper<out T> where T : class
{
    /// <summary>
    ///     Maps data from a <see cref="SqlDataReader" /> to an instance of <typeparamref name="T" />.
    /// </summary>
    /// <param name="reader">The <see cref="SqlDataReader" /> containing the data to map.</param>
    /// <returns>An instance of <typeparamref name="T" /> populated with data from the reader.</returns>
    public static abstract T MapFromReader(SqlDataReader reader);
}