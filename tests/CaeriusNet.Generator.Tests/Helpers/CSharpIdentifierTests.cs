namespace CaeriusNet.Generator.Tests.Helpers;

public sealed class CSharpIdentifierTests
{
    [Theory]
    [InlineData("class", "@class")]
    [InlineData("record", "@record")]
    [InlineData("event", "@event")]
    [InlineData("foo", "foo")]
    [InlineData("café", "café")]
    public void Escape_Preserves_Unicode_And_Escapes_CSharp_Keywords(string identifier, string expected)
    {
        Assert.Equal(expected, CSharpIdentifier.Escape(identifier));
    }
}
