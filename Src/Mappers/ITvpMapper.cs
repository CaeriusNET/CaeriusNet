namespace CaeriusNet.Mappers;

/// <summary>
///     Represents a mapper that defines a method for converting a collection of objects of type <typeparamref name="T" />
///     into a DataTable to be used as a Table-Valued Parameter (TVP) in SQL Server.
/// </summary>
/// <typeparam name="T">The type of object to be mapped to a DataTable.</typeparam>
public interface ITvpMapper<in T> where T : class
{
    /// <summary>
    ///     Maps a collection of <typeparamref name="T" /> objects to a DataTable.
    ///     This method is used to convert a collection of objects into a format that can be used
    ///     as a TVP (Table-Valued Parameter) in SQL Server stored procedures.
    /// </summary>
    /// <param name="items">The collection of <typeparamref name="T" /> objects to map.</param>
    /// <returns>A DataTable representing the mapped collection of objects, suitable for use as a TVP.</returns>
    public DataTable MapAsDataTable(IEnumerable<T> items);
}