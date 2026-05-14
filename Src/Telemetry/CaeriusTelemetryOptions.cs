namespace CaeriusNet.Telemetry;

/// <summary>
///     Controls what CaeriusNet emits to OpenTelemetry spans and metrics.
///     Configure via <c>CaeriusNetBuilder.WithTelemetryOptions(...)</c>.
/// </summary>
/// <remarks>
///     <para>
///         By default, only parameter <em>names</em> are captured in the
///         <c>caerius.sp.parameters</c> span tag (e.g. <c>@userId,@name</c>).
///         Enabling <see cref="CaptureParameterValues" /> adds the runtime values
///         (e.g. <c>@userId=42,@name=Alice</c>) which is useful during development
///         but should be disabled in production to avoid leaking PII or secrets into
///         telemetry back-ends.
///     </para>
/// </remarks>
public sealed class CaeriusTelemetryOptions
{
    /// <summary>
    ///     When <see langword="true" />, the <c>caerius.sp.parameters</c> tag includes
    ///     both parameter names and their runtime values (e.g. <c>@id=42,@Name=Alice</c>).
    ///     Defaults to <see langword="false" /> to prevent accidental PII exposure in
    ///     production telemetry pipelines.
    /// </summary>
    public bool CaptureParameterValues { get; init; }
}
