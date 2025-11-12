namespace CaeriusNet.Mappers;

/// <summary>
///     Represents a mapper that defines a method for converting a collection of objects of type <typeparamref name="T" />
///     into a DataTable to be used as a Table-Valued Parameter (TVP) in SQL Server.
/// </summary>
/// <typeparam name="T">The type of object to be mapped to a DataTable.</typeparam>
public interface ITvpMapper<in T> where T : class
{
	/// <summary>
	///     Gets the SQL Server type name for this TVP (e.g., "dbo.tvp_MyType").
	/// </summary>
	static abstract string TvpTypeName { get; }

	/// <summary>
	///     Maps a collection of <typeparamref name="T" /> objects to a DataTable.
	///     This method is used to convert a collection of objects into a format that can be used
	///     as a TVP (Table-Valued Parameter) in SQL Server stored procedures.
	/// </summary>
	/// <param name="items">The collection of <typeparamref name="T" /> objects to map.</param>
	/// <returns>A DataTable representing the mapped collection of objects, suitable for use as a TVP.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="items" /> is null.</exception>
	/// <remarks>
	///     The returned DataTable should match the structure defined in the corresponding SQL Server TVP type.
	///     Each property of type <typeparamref name="T" /> should map to a column in the DataTable.
	/// </remarks>
	public DataTable MapAsDataTable(IEnumerable<T> items);
}