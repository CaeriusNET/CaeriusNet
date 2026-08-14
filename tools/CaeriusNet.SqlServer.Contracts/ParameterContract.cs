namespace CaeriusNet.SqlServer.Contracts;

internal sealed record ParameterContract(
    int Ordinal,
    string Name,
    string SqlType,
    string ClrType,
    bool IsTableType,
    string Direction,
    bool Nullable,
    int? MaxLength,
    byte? Precision,
    byte? Scale);