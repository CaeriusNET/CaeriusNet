namespace CaeriusNet.Generator.Helpers;

/// <summary>
///     Metadata stamped into generated code.
/// </summary>
/// <remarks>
///     The SDK emits <see cref="System.Reflection.AssemblyInformationalVersionAttribute" /> from the repository's
///     central <c>Version</c> MSBuild property.  Reading it here prevents a second hand-maintained package version.
/// </remarks>
internal static class GeneratorMetadata
{
    internal const string ToolName = "CaeriusNet.Generator";

    internal static string Version { get; } = GetBuildVersion();

    private static string GetBuildVersion()
    {
        var informationalVersion = typeof(GeneratorMetadata).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;

        if (informationalVersion is null || string.IsNullOrWhiteSpace(informationalVersion))
            return "12.0.0";

        var sourceRevisionSeparator = informationalVersion.IndexOf('+');
        return sourceRevisionSeparator < 0
            ? informationalVersion
            : informationalVersion.Substring(0, sourceRevisionSeparator);
    }
}
