namespace CaeriusNet.Generator.Models;

/// <summary>
///     Value-equatable serialisable substitute for <see cref="Location" />.
/// </summary>
/// <remarks>
///     Roslyn <see cref="Location" /> instances reference live <see cref="SyntaxTree" /> objects; flowing
///     them through an incremental pipeline pins those trees in cache and creates spurious differences.
///     <see cref="LocationInfo" /> captures only the <em>data</em> required to reconstruct a usable
///     <see cref="Location" /> later via <see cref="ToLocation" />.
/// </remarks>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation()
    {
        return Location.Create(FilePath, TextSpan, LineSpan);
    }

    public static LocationInfo? CreateFrom(Location location)
    {
        if (location.SourceTree is null) return null;
        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan,
            location.GetLineSpan().Span);
    }
}