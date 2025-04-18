namespace CaeriusNet.Generator.Infrastructure.Extensions;

internal static class AnalyzerConfigOptionsProviderExtensions
{
	internal static IncrementalValueProvider<bool> IsEnabled(
		this IncrementalValueProvider<AnalyzerConfigOptionsProvider> provider, string generatorName)
	{
		return provider.Select((p, _) =>
		{
			if (p.GlobalOptions.TryGetValue($"build_property.{generatorName}", out var value))
				return !string.Equals(value, "disabled", StringComparison.OrdinalIgnoreCase);

			return true;
		});
	}
}