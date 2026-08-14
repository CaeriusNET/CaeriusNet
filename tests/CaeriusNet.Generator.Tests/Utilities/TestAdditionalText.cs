using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace CaeriusNet.Generator.Tests.Utilities;

internal sealed class TestAdditionalText(
    string path,
    string text,
    IReadOnlyDictionary<string, string>? options = null) : AdditionalText
{
    public override string Path { get; } = path;

    internal IReadOnlyDictionary<string, string> Options { get; } =
        options ?? new Dictionary<string, string>(StringComparer.Ordinal);

    public override SourceText GetText(CancellationToken cancellationToken = default)
    {
        return SourceText.From(text, Encoding.UTF8);
    }
}