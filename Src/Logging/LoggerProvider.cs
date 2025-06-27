namespace CaeriusNet.Logging;

/// <summary>
///     Fournisseur singleton pour l'instance de logger.
/// </summary>
internal static class LoggerProvider
{
	private static ICaeriusLogger? _logger;

    /// <summary>
    ///     Obtient l'instance de logger courante.
    /// </summary>
    /// <returns>L'instance de logger si elle est configurée, sinon null.</returns>
    internal static ICaeriusLogger? GetLogger()
	{
		return _logger;
	}

    /// <summary>
    ///     Définit l'instance de logger à utiliser.
    /// </summary>
    /// <param name="logger">L'instance de logger.</param>
    internal static void SetLogger(ICaeriusLogger logger)
	{
		_logger = logger;
	}
}