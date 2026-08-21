namespace CaeriusNet.Mappers;

public interface ISpMapper<out T>
{
    static abstract T MapFromDataReader(SqlDataReader reader);
}

public interface ITvpMapper<T>
{
    static abstract string SqlTypeName { get; }
    static abstract ImmutableArray<SqlMetaData> Metadata { get; }
    static abstract SqlDataRecord CreateRecord();
    static abstract void WriteRow(SqlDataRecord record, T row);
}

public interface ICaeriusGeneratedProcedure<TSelf>
    where TSelf : struct, ICaeriusGeneratedProcedure<TSelf>
{
    static abstract string SchemaName { get; }
    static abstract string ProcedureName { get; }
    static abstract string FullName { get; }
    static abstract string ContractHash { get; }
    static abstract int ParameterCount { get; }
    static abstract int ResultSetCount { get; }
}

public interface ICaeriusGeneratedProcedureParameters<TProcedure, TSelf>
    where TProcedure : struct, ICaeriusGeneratedProcedure<TProcedure>
    where TSelf : ICaeriusGeneratedProcedureParameters<TProcedure, TSelf>
{
    static abstract void Bind(StoredProcedureCommandBuilder<TProcedure> builder, TSelf parameters);
}
