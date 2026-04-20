namespace CaeriusNet.Generator.Models;

/// <summary>
///     Value-equatable, pipeline-friendly diagnostic payload.
/// </summary>
/// <remarks>
///     <para>
///         Wrapping a <see cref="Diagnostic" /> directly in pipeline output is unsafe: the underlying
///         <see cref="Location" /> can pin live syntax trees in the incremental cache. <see cref="DiagnosticInfo" />
///         captures the descriptor identity, the location as a serialisable <see cref="LocationInfo" />, and
///         the message arguments. The actual <see cref="Diagnostic" /> is materialised at emit time.
///     </para>
/// </remarks>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    public Diagnostic ToDiagnostic()
    {
        return Diagnostic.Create(
            Descriptor,
            Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None,
            MessageArgs.AsImmutableArray().Cast<object?>().ToArray());
    }

    public static DiagnosticInfo Create(
        DiagnosticDescriptor descriptor,
        LocationInfo? location,
        params string[] messageArgs)
    {
        return new DiagnosticInfo(descriptor, location, new EquatableArray<string>(messageArgs.ToImmutableArray()));
    }
}