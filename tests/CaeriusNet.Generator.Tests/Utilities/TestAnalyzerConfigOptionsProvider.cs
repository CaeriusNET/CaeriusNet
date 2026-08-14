using Microsoft.CodeAnalysis.Diagnostics;

namespace CaeriusNet.Generator.Tests.Utilities;

internal sealed class TestAnalyzerConfigOptionsProvider(
    IReadOnlyList<AdditionalText>? additionalTexts = null) : AnalyzerConfigOptionsProvider
{
    private static readonly AnalyzerConfigOptions EmptyOptions = new TestAnalyzerConfigOptions(null);

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _additionalOptions =
        BuildAdditionalOptions(additionalTexts);

    public override AnalyzerConfigOptions GlobalOptions => EmptyOptions;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        return EmptyOptions;
    }

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        return _additionalOptions.TryGetValue(textFile.Path, out var options)
            ? new TestAnalyzerConfigOptions(options)
            : EmptyOptions;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildAdditionalOptions(
        IReadOnlyList<AdditionalText>? additionalTexts)
    {
        if (additionalTexts is null || additionalTexts.Count == 0)
            return new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);

        var options = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var additionalText in additionalTexts)
            if (additionalText is TestAdditionalText testAdditionalText && testAdditionalText.Options.Count > 0)
                options[testAdditionalText.Path] = testAdditionalText.Options;

        return options;
    }
}