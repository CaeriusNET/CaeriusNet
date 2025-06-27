using CaeriusNet.Attributes.Dto;

namespace CaeriusNet.Mappers;

/// <summary>
///     Defines a contract for mapping a row from a <see cref="SqlDataReader" /> into an instance of a specific DTO type.
///     Mappers generated via <see cref="GenerateDtoAttribute" /> offer high-performance, ordinal-based,
///     compile-time-safe
///     mapping from stored procedure results to .NET records or classes, with explicit handling of nullability and type
///     compatibility.
/// </summary>
/// <typeparam name="T">
///     The DTO type to instantiate.
///     <para>
///         <b>Constraints:</b>
///         <list type="bullet">
///             <item>
///                 <typeparamref name="T" /> must be a <c>sealed partial</c> class or record, typically attributed with
///                 <see cref="GenerateDtoAttribute" />.
///             </item>
///             <item>
///                 All mapped parameters must be supported primitive, value, or string/byte[] types, with nullability
///                 matching SQL expectations.
///             </item>
///         </list>
///     </para>
/// </typeparam>
public interface ISpMapper<out T> where T : class
{
	/// <summary>
	///     Maps the current row of a provided <see cref="SqlDataReader" /> into a new DTO instance.
	/// </summary>
	/// <param name="reader">
	///     The SQL data reader, positioned at the target row. The mapping is ordinally matched (buffer column
	///     index matches constructor parameter index).
	/// </param>
	/// <returns>
	///     An initialized instance of <typeparamref name="T" /> with properties populated from the current data reader row.
	/// </returns>
	public static abstract T MapFromDataReader(SqlDataReader reader);
}