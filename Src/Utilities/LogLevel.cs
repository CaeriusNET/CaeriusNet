namespace CaeriusNet.Utilities;

/// <summary>
///     Définit les niveaux de gravité pour les logs.
/// </summary>
public enum LogLevel : byte
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}