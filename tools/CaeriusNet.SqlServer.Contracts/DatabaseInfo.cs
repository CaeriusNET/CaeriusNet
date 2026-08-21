namespace CaeriusNet.SqlServer.Contracts;

internal sealed record DatabaseInfo(
    string Name,
    string ServerVersion,
    int ServerMajor,
    int CompatibilityLevel);
