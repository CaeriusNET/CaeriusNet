namespace CaeriusNet.Tests.Exceptions;

public sealed class CaeriusNetSqlExceptionTests
{
    [Fact]
    public void CaeriusNetSqlException_Inherits_From_Exception()
    {
        Assert.True(typeof(CaeriusNetSqlException).IsSubclassOf(typeof(Exception)));
    }

    [Fact]
    public void CaeriusNetSqlException_IsSealed()
    {
        Assert.True(typeof(CaeriusNetSqlException).IsSealed);
    }

    [Fact]
    public void CaeriusNetSqlException_Has_Single_Constructor()
    {
        var ctors = typeof(CaeriusNetSqlException).GetConstructors();

        Assert.Single(ctors);
    }

    [Fact]
    public void CaeriusNetSqlException_Constructor_Has_Two_Parameters()
    {
        var parameters = typeof(CaeriusNetSqlException).GetConstructors()[0].GetParameters();

        Assert.Equal(2, parameters.Length);
    }

    [Fact]
    public void CaeriusNetSqlException_FirstParameter_Is_String()
    {
        var parameters = typeof(CaeriusNetSqlException).GetConstructors()[0].GetParameters();

        Assert.Equal(typeof(string), parameters[0].ParameterType);
    }

    [Fact]
    public void CaeriusNetSqlException_SecondParameter_Is_SqlException()
    {
        var parameters = typeof(CaeriusNetSqlException).GetConstructors()[0].GetParameters();

        Assert.Equal(typeof(SqlException), parameters[1].ParameterType);
    }

    [Fact]
    public void CaeriusNetSqlException_SecondParameterType_IsAssignableTo_Exception()
    {
        var secondParamType = typeof(CaeriusNetSqlException).GetConstructors()[0].GetParameters()[1].ParameterType;

        // Verifies the constructor contract: SqlException satisfies base Exception(string, Exception)
        Assert.True(typeof(Exception).IsAssignableFrom(secondParamType));
    }
}