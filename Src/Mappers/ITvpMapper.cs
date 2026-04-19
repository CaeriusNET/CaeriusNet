namespace CaeriusNet.Mappers;

/// <summary>
///     Represents a mapper that converts a collection of <typeparamref name="T" /> objects into a sequence of
///     <see cref="SqlDataRecord" /> instances for use as a Table-Valued Parameter (TVP) in SQL Server.
/// </summary>
/// <typeparam name="T">The type of object to be mapped to SQL data records.</typeparam>
public interface ITvpMapper<in T> where T : class
{
	/// <summary>
	///     Gets the SQL Server type name for this TVP (e.g., "dbo.tvp_MyType").
	/// </summary>
	static abstract string TvpTypeName { get; }

	/// <summary>
	///     Maps a collection of <typeparamref name="T" /> objects to an enumerable sequence of
	///     <see cref="SqlDataRecord" /> for streaming directly to SQL Server as a TVP.
	/// </summary>
	/// <param name="items">The collection of <typeparamref name="T" /> objects to map.</param>
	/// <returns>
	///     An <see cref="IEnumerable{T}" /> of <see cref="SqlDataRecord" /> instances, each representing one row
	///     in the TVP. The same <see cref="SqlDataRecord" /> instance is reused across iterations for minimal allocation.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="items" /> is null.</exception>
	/// <remarks>
	///     The sequence is streamed lazily; values are set on the shared <see cref="SqlDataRecord" /> immediately
	///     before each <c>yield return</c>. Microsoft.Data.SqlClient reads each record's values before advancing,
	///     making this single-instance reuse pattern safe for TVP streaming.
	/// </remarks>
	public IEnumerable<SqlDataRecord> MapAsSqlDataRecords(IEnumerable<T> items);
}