namespace CaeriusNet.Generator.Models;

/// <summary>
///     Classifies how a primary-constructor parameter is read from / written to SQL Server.
/// </summary>
/// <remarks>
///     Computed at extraction time (semantic phase) and frozen onto <see cref="ColumnModel" /> so the
///     emission phase becomes a pure string transform: no <c>typeName.Contains("byte[]")</c> guesswork
///     and no risk of accidental substring matches.
/// </remarks>
internal enum ColumnKind
{
    /// <summary>Direct reader/writer call (e.g. <c>GetInt32</c> / <c>SetInt32</c>).</summary>
    Standard = 0,

    /// <summary><c>char</c> — read via <c>GetString(i)[0]</c>, written via <c>SetString(i, c.ToString())</c>.</summary>
    Char = 1,

    /// <summary><c>System.DateOnly</c> — read via <c>FromDateTime</c>, written via <c>SetDateTime(... .ToDateTime(...))</c>.</summary>
    DateOnly = 2,

    /// <summary><c>System.TimeOnly</c> — read via <c>FromDateTime</c>, written via <c>SetTimeSpan(... .ToTimeSpan())</c>.</summary>
    TimeOnly = 3,

    /// <summary><c>byte[]</c> — read via <c>(byte[])GetValue(i)</c>, written via <c>SetValue(i, value)</c>.</summary>
    ByteArray = 4,

    /// <summary>An <see cref="System.Enum" /> — cast to/from its underlying integral type.</summary>
    Enum = 5,

    /// <summary><c>System.Half</c> — read via <c>(Half)GetFloat(i)</c>, written via <c>SetFloat(i, (float)value)</c>.</summary>
    Half = 6
}
