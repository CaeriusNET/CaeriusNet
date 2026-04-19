namespace CaeriusNet.Generator.Tests.Dto;

public sealed class DtoSourceGeneratorTests
{
    [Fact]
    public void BasicRecord_Generates_ISpMapper_Implementation()
    {
        const string source = """
            using CaeriusNet.Attributes.Dto;
            namespace Test.Models;
            [GenerateDto]
            public sealed partial record UserDto(int Id, string Name);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("ISpMapper<UserDto>", generated);
        Assert.Contains("MapFromDataReader", generated);
        Assert.Contains("reader.GetInt32(0)", generated);
        Assert.Contains("reader.GetString(1)", generated);
    }

    [Fact]
    public void NullableReferenceType_Generates_IsDBNull_Check()
    {
        const string source = """
            #nullable enable
            using CaeriusNet.Attributes.Dto;
            namespace Test.Models;
            [GenerateDto]
            public sealed partial record UserDto(int Id, string? Name);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("reader.IsDBNull(1)", generated);
    }

    [Fact]
    public void NullableValueType_Generates_IsDBNull_Check()
    {
        const string source = """
            using CaeriusNet.Attributes.Dto;
            namespace Test.Models;
            [GenerateDto]
            public sealed partial record ItemDto(int Id, int? Quantity);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("reader.IsDBNull(1)", generated);
    }

    [Fact]
    public void DateOnly_Generates_FromDateTime_Conversion()
    {
        const string source = """
            using CaeriusNet.Attributes.Dto;
            namespace Test.Models;
            [GenerateDto]
            public sealed partial record EventDto(int Id, System.DateOnly EventDate);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("DateOnly.FromDateTime", generated);
    }

    [Fact]
    public void TimeOnly_Generates_FromDateTime_Conversion()
    {
        const string source = """
            using CaeriusNet.Attributes.Dto;
            namespace Test.Models;
            [GenerateDto]
            public sealed partial record ScheduleDto(int Id, System.TimeOnly StartTime);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("TimeOnly.FromDateTime", generated);
    }

    [Fact]
    public void ByteArray_Generates_GetValue_Cast()
    {
        const string source = """
            using CaeriusNet.Attributes.Dto;
            namespace Test.Models;
            [GenerateDto]
            public sealed partial record BlobDto(int Id, byte[] Data);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("(byte[])reader.GetValue(1)", generated);
    }

    [Fact]
    public void GeneratedFile_Is_Named_After_Type()
    {
        const string source = """
            using CaeriusNet.Attributes.Dto;
            namespace Test.Models;
            [GenerateDto]
            public sealed partial record ProductDto(int Id, string Name);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        Assert.Contains("ProductDto.g.cs", result.GeneratedTrees[0].FilePath);
    }

    [Fact]
    public void NonSealed_Type_Does_Not_Generate()
    {
        const string source = """
            using CaeriusNet.Attributes.Dto;
            namespace Test.Models;
            [GenerateDto]
            public partial record UserDto(int Id, string Name);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void NonPartial_Type_Does_Not_Generate()
    {
        const string source = """
            using CaeriusNet.Attributes.Dto;
            namespace Test.Models;
            [GenerateDto]
            public sealed record UserDto(int Id, string Name);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void Type_Without_Attribute_Does_Not_Generate()
    {
        const string source = """
            namespace Test.Models;
            public sealed partial record UserDto(int Id, string Name);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void Class_With_Attribute_Generates_Class_Keyword()
    {
        const string source = """
            using CaeriusNet.Attributes.Dto;
            namespace Test.Models;
            [GenerateDto]
            public sealed partial class UserClass(int Id, string Name);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("partial class UserClass", generated);
    }

    [Fact]
    public void Multiple_Types_Generate_Multiple_Files()
    {
        const string source = """
            using CaeriusNet.Attributes.Dto;
            namespace Test.Models;
            [GenerateDto]
            public sealed partial record UserDto(int Id, string Name);
            [GenerateDto]
            public sealed partial record OrderDto(int Id, decimal Total);
            """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Equal(2, result.GeneratedTrees.Length);
    }
}
