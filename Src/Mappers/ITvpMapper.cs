namespace CaeriusNet.Mappers;

/// <summary>
///     Defines a method for mapping a collection of objects of type <typeparamref name="T" /> to a DataTable.
///     This interface is intended for use with Table-Valued Parameters (TVPs) in SQL Server, allowing for efficient
///     bulk data operations.
/// </summary>
/// <typeparam name="T">The type of object to be mapped to a DataTable.</typeparam>
public interface ITvpMapper<in T> where T : class
{
    /// <summary>
    ///     Maps a collection of <typeparamref name="T" /> objects to a DataTable.
    ///     This method is used to convert a collection of objects into a format that can be used
    ///     as a TVP in SQL Server stored procedures.
    /// </summary>
    /// <param name="items">The collection of <typeparamref name="T" /> objects to map.</param>
    /// <returns>A DataTable representing the collection of objects, suitable for use as a TVP.</returns>
    DataTable MapToDataTable(IEnumerable<T> items);
}