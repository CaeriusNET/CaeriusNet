using Microsoft.Data.SqlClient;

namespace CaeriusNet.Core.Mappers;

/// <summary>
///     Represents the parameters to be passed to a stored procedure, including the procedure name,
///     capacity, list of parameters.
/// </summary>
public sealed record StoredProcedureParameters(string ProcedureName, int Capacity, List<SqlParameter> Parameters);