namespace CaeriusNet.Tests.Helpers;

public sealed class ResultSetMaterializerTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(16, 16)]
    public void NormalizeCapacity_Uses_At_Least_One(int capacity, int expected)
    {
        Assert.Equal(expected, ResultSetMaterializer.NormalizeCapacity(capacity));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(16, 24)]
    public void GrowCapacity_Uses_One_And_A_Half_Growth(int capacity, int expected)
    {
        Assert.Equal(expected, ResultSetMaterializer.GrowCapacity(capacity));
    }

    [Fact]
    public void GrowCapacity_Caps_At_Array_MaxLength_Before_Overflow()
    {
        Assert.Equal(Array.MaxLength, ResultSetMaterializer.GrowCapacity(Array.MaxLength - 1));
    }

    [Fact]
    public void GrowCapacity_Throws_When_Already_At_Array_MaxLength()
    {
        Assert.Throws<InvalidOperationException>(() => ResultSetMaterializer.GrowCapacity(Array.MaxLength));
    }

    [Fact]
    public void GrowCapacity_Negative_Capacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ResultSetMaterializer.GrowCapacity(-1));
    }
}
