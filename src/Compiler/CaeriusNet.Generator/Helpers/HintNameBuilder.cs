namespace CaeriusNet.Generator.Helpers;

/// <summary>
///     Builds stable, generator-specific hint names for
///     <see cref="SourceProductionContext.AddSource(string, SourceText)" />.
/// </summary>
internal static class HintNameBuilder
{
    internal static string Build(string @namespace, string typeName, string generatorKind)
    {
        var namespacePrefix = string.IsNullOrEmpty(@namespace) ? "global" : ToHintNameSegment(@namespace);
        return $"{namespacePrefix}.{ToHintNameSegment(typeName)}.{generatorKind}.g.cs";
    }

    /// <summary>
    ///     Produces a stable, Roslyn-valid hint-name segment without changing the source identifier that is emitted.
    ///     Escaped and Unicode C# identifiers are legal in generated code but not necessarily in
    ///     <see cref="SourceProductionContext.AddSource(string, SourceText)" /> hint names.
    /// </summary>
    private static string ToHintNameSegment(string value)
    {
        var builder = new StringBuilder(value.Length);
        var encoded = false;
        foreach (var character in value)
        {
            if (character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-')
            {
                builder.Append(character);
                continue;
            }

            encoded = true;
            builder.Append('_')
                .Append(((int)character).ToString("x4", CultureInfo.InvariantCulture))
                .Append('_');
        }

        return encoded
            ? builder.Append('.').Append(BuildStableSuffix(value)).ToString()
            : builder.ToString();
    }

    private static string BuildStableSuffix(string value)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037;
            const ulong prime = 1099511628211;
            var hash = offset;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= prime;
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
