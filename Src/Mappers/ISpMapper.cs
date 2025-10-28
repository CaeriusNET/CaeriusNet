namespace CaeriusNet.Mappers;

/// <summary>
///     Defines a contract for mapping a row from a <see cref="System.Data.SqlClient.SqlDataReader" /> into an instance of
///     a specific DTO type.
/// </summary>
/// <typeparam name="T">The type of the data transfer object (DTO) to map to.</typeparam>
/// <remarks>
///     <para>
///         This interface is designed to provide high-performance, ordinal-based, compile-time-safe mapping from stored
///         procedure results
///         to .NET records or classes, with explicit handling of nullability and type compatibility.
///     </para>
///     <para>
///         Implementations of this interface are typically generated automatically using the
///         <see cref="GenerateDtoAttribute" />.
///     </para>
/// </remarks>
/// <example>
///     The following example demonstrates a basic implementation:
///     <code>
///  public sealed partial class CustomerDto : ISpMapper&lt;CustomerDto&gt;
///  {
/// 	 public static CustomerDto MapFromDataReader(SqlDataReader reader)
/// 	 {
/// 		 return new CustomerDto(
/// 			 reader.GetInt32(0),	// CustomerId
/// 			 reader.GetString(1)	 // CustomerName
/// 		 );
/// 	 }
///  }
///  </code>
/// </example>
public interface ISpMapper<out T> where T : class
{
	/// <summary>
	///     Maps the current row of the specified <see cref="System.Data.SqlClient.SqlDataReader" /> to a new instance of type
	///     <typeparamref name="T" />.
	/// </summary>
	/// <param name="reader">
	///     A <see cref="System.Data.SqlClient.SqlDataReader" /> that contains the data to map. The reader
	///     must be positioned at a valid row.
	/// </param>
	/// <returns>A new instance of <typeparamref name="T" /> populated with data from the current row of the reader.</returns>
	/// <remarks>
	///     <para>
	///         This method performs ordinal-based mapping, where the column indices in the result set must match the order of
	///         parameters
	///         in the target type's constructor.
	///     </para>
	///     <para>
	///         The mapping is performed using compile-time generated code for optimal performance.
	///     </para>
	/// </remarks>
	/// <exception cref="System.InvalidOperationException">Thrown when the reader is not positioned on a valid row.</exception>
	/// <exception cref="System.InvalidCastException">Thrown when data conversion between SQL and .NET types fails.</exception>
	public static abstract T MapFromDataReader(SqlDataReader reader);
}