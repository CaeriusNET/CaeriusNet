namespace CaeriusNet.SqlServer.Contracts;

internal sealed record ColumnContract(
    int Ordinal,
    string Name,
    string SqlType,
    string ClrType,
    bool Nullable,
    int? MaxLength,
    byte? Precision,
    byte? Scale);