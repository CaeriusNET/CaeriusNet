namespace CaeriusNet.Analyzer.Tests.Utilities;

internal sealed class TestAnalyzerConfigOptions(
    IReadOnlyDictionary<string, string>? options) : AnalyzerConfigOptions
{
    public override bool TryGetValue(string key, out string value)
    {
        if (options is not null && options.TryGetValue(key, out var found))
        {
            value = found;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
