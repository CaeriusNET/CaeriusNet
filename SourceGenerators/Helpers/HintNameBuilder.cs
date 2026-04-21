namespace CaeriusNet.Generator.Helpers;

/// <summary>
///     Builds stable, generator-specific hint names for
///     <see cref="SourceProductionContext.AddSource(string, SourceText)" />.
/// </summary>
internal static class HintNameBuilder
{
    internal static string Build(string @namespace, string typeName, string generatorKind)
    {
        var namespacePrefix = string.IsNullOrEmpty(@namespace) ? "global" : @namespace;
        return $"{namespacePrefix}.{typeName}.{generatorKind}.g.cs";
    }
}