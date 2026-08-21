namespace CaeriusNet.Aspire;

internal sealed record CaeriusSqlServerVersionCheckRegistration(string Name, string? SqlClientKey, TimeSpan Timeout);
