using CaeriusNet.Core.Mappers;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CaeriusNet.Core.Builders;

/// <summary>
///     Provides functionality to build parameters for a stored procedure call, including support for regular,
///     Table-Valued Parameters (TVPs), and caching mechanisms.
/// </summary>
public sealed record StoredProcedureParametersBuilder(string ProcedureName, int Capacity = 1)
{
	/// <summary>
	///     Gets the collection of SQL parameters to be used in the stored procedure call.
	/// </summary>
	private List<SqlParameter> Parameters { get; } = [];

	/// <summary>
	///     Adds a parameter to the stored procedure call.
	/// </summary>
	/// <param name="parameter">The name of the parameter.</param>
	/// <param name="value">The value of the parameter.</param>
	/// <param name="dbType">The TSQL data type of the parameter. Use <see cref="SqlDbType" /> enumeration.</param>
	/// <returns>The <see cref="StoredProcedureParametersBuilder" /> instance for chaining.</returns>
	public StoredProcedureParametersBuilder AddParameter(string parameter, object value, SqlDbType dbType)
	{
		var currentItemParameter = new SqlParameter(parameter, dbType) { Value = value };
		Parameters.Add(currentItemParameter);
		return this;
	}

	/// <summary>
	///     Adds a Table-Valued Parameter (TVP) to the stored procedure call.
	/// </summary>
	/// <typeparam name="T">The type of the object that maps to the TVP.</typeparam>
	/// <param name="parameter">The name of the TVP parameter.</param>
	/// <param name="tvpName">The name of the TVP type in SQL Server.</param>
	/// <param name="items">The collection of items to map to the TVP using the ITvpMapper interface.</param>
	/// <returns>The StoredProcedureParametersBuilder instance for chaining.</returns>
	/// <exception cref="ArgumentException">Thrown when the items collection is empty.</exception>
	public StoredProcedureParametersBuilder AddTvpParameter<T>(string parameter, string tvpName, IEnumerable<T> items)
		where T : class, ITvpMapper<T>
	{
		var tvpMappers = items.ToList();
		if (tvpMappers.Count == 0)
			throw new ArgumentException("No items found in the collection to map to a Table-Valued Parameter.");

		var dataTable = tvpMappers[0].MapAsDataTable(tvpMappers);
		var currentTvpParameter = new SqlParameter(parameter, SqlDbType.Structured)
		{
			TypeName = tvpName,
			Value = dataTable
		};

		Parameters.Add(currentTvpParameter);
		return this;
	}

	/// <summary>
	///     Builds and returns a <see cref="StoredProcedureParameters" /> object containing all configured parameters.
	/// </summary>
	/// <returns>
	///     A <see cref="StoredProcedureParameters" /> instance containing the stored procedure name, capacity,
	///     parameters, and optional caching settings.
	/// </returns>
	public StoredProcedureParameters Build()
	{
		return new StoredProcedureParameters(ProcedureName, Capacity, Parameters);
	}
}