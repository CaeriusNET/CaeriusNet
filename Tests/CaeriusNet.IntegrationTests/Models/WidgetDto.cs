namespace CaeriusNet.IntegrationTests.Models;

/// <summary>
///     Mirror of <c>dbo.Widgets</c>. Source-generated <c>ISpMapper&lt;WidgetDto&gt;</c> via
///     <see cref="GenerateDtoAttribute" />.
/// </summary>
[GenerateDto]
public sealed partial record WidgetDto(int Id, string Name, int Quantity, DateTime CreatedAt);

/// <summary>
///     Mirror of the COUNT_BIG(*) projection from <c>dbo.usp_GetWidgetsAndCount</c> /
///     <c>dbo.usp_CountWidgets</c>. Source-generated <c>ISpMapper&lt;WidgetCountDto&gt;</c>.
/// </summary>
[GenerateDto]
public sealed partial record WidgetCountDto(long Total);

/// <summary>
///     Mirror of <c>dbo.WidgetTvp</c>. Source-generated <c>ITvpMapper&lt;WidgetTvp&gt;</c> via
///     <see cref="GenerateTvpAttribute" />.
/// </summary>
[GenerateTvp(Schema = "dbo", TvpName = "WidgetTvp")]
public sealed partial record WidgetTvp(string Name, int Quantity);
