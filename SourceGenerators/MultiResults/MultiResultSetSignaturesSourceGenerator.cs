namespace CaeriusNet.Generator.MultiResults;

/// <summary>
///     Emits the generated multi-result-set overloads into the CaeriusNet runtime assembly.
/// </summary>
/// <remarks>
///     The generator is intentionally scoped to the runtime assembly. The NuGet package also ships
///     this analyzer DLL to consumers for DTO/TVP/AutoContracts generation; consumer projects must
///     not receive duplicate multi-result extension methods.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class MultiResultSetSignaturesSourceGenerator : IIncrementalGenerator
{
    private const string RuntimeAssemblyName = "CaeriusNet";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var shouldEmit = context.CompilationProvider
            .Select(static (compilation, _) =>
                string.Equals(compilation.AssemblyName, RuntimeAssemblyName, StringComparison.Ordinal));

        context.RegisterImplementationSourceOutput(shouldEmit, static (spc, emit) =>
        {
            if (!emit)
                return;

            spc.AddSource(
                "CaeriusNet.Commands.Reads.MultiResultSetGeneratedSignatures.g.cs",
                MultiResultSetSignaturesEmitter.Emit());
        });
    }
}
