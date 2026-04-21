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
    public void ByteArray_Generates_GetFieldValue()
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
        Assert.Contains("reader.GetFieldValue<byte[]>(1)", generated);
    }

    [Fact]
    public void GeneratedFile_Uses_Stable_Unique_HintName()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record ProductDto(int Id, string Name);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        Assert.Contains("Test.Models.ProductDto.Dto.g.cs", result.GeneratedTrees[0].FilePath);
    }

    [Fact]
    public void Duplicate_Type_Names_In_Different_Namespaces_Generate_Distinct_HintNames()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Alpha.Models
                              {
                                  [GenerateDto]
                                  public sealed partial record UserDto(int Id);
                              }
                              namespace Beta.Models
                              {
                                  [GenerateDto]
                                  public sealed partial record UserDto(int Id);
                              }
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        var filePaths = result.GeneratedTrees
            .Select(static tree => tree.FilePath)
            .OrderBy(static path => path)
            .ToArray();

        Assert.Equal(2, filePaths.Length);
        Assert.Contains(filePaths, path => path.Contains("Alpha.Models.UserDto.Dto.g.cs", StringComparison.Ordinal));
        Assert.Contains(filePaths, path => path.Contains("Beta.Models.UserDto.Dto.g.cs", StringComparison.Ordinal));
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

    [Fact]
    public void Guid_Field_Generates_GetGuid()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record EntityDto(int Id, System.Guid TraceId);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("reader.GetGuid(1)", generated);
    }

    [Fact]
    public void Bool_Field_Generates_GetBoolean()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record FlagDto(int Id, bool IsActive);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("reader.GetBoolean(1)", generated);
    }

    [Fact]
    public void Long_Field_Generates_GetInt64()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record MetricDto(int Id, long Ticks);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("reader.GetInt64(1)", generated);
    }

    [Fact]
    public void Short_Field_Generates_GetInt16()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record CodeDto(int Id, short Code);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("reader.GetInt16(1)", generated);
    }

    [Fact]
    public void Decimal_Field_Generates_GetDecimal()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record PriceDto(int Id, decimal Price);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("reader.GetDecimal(1)", generated);
    }

    [Fact]
    public void Float_Field_Generates_GetFloat()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record ScoreDto(int Id, float Score);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("reader.GetFloat(1)", generated);
    }

    [Fact]
    public void Double_Field_Generates_GetDouble()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record RatioDto(int Id, double Ratio);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("reader.GetDouble(1)", generated);
    }

    [Fact]
    public void Byte_Field_Generates_GetByte()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record LevelDto(int Id, byte Level);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("reader.GetByte(1)", generated);
    }

    [Fact]
    public void Nullable_Guid_Generates_IsDBNull_Check()
    {
        const string source = """
                              #nullable enable
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record TraceDto(int Id, System.Guid? TraceId);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("reader.IsDBNull(1)", generated);
        Assert.Contains("reader.GetGuid(1)", generated);
    }

    [Fact]
    public void Nested_Namespace_Generates_Correct_Namespace()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace My.App.Data.Models;
                              [GenerateDto]
                              public sealed partial record DeepDto(int Id, string Name);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("namespace My.App.Data.Models;", generated);
    }

    [Fact]
    public void Enum_Int_Property_Generates_Cast_From_GetInt32()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              public enum Status { Active = 1, Inactive = 2 }
                              [GenerateDto]
                              public sealed partial record ItemDto(int Id, Status Status);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("reader.GetInt32(1)", generated);
        Assert.Contains("(Test.Models.Status)", generated);
    }

    [Fact]
    public void EmptyParameterRecord_DoesNotGenerate()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record EmptyDto();
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void Half_Field_Generates_GetFloat_With_Half_Cast()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record SensorDto(int Id, System.Half Temperature);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("GetFloat", generated);
        Assert.Contains("(Half)", generated);
    }

    [Fact]
    public void Nullable_Half_Generates_IsDBNull_Check()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record SensorDto(int Id, System.Half? Temperature);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("reader.IsDBNull(1)", generated);
        Assert.Contains("(Half)", generated);
        Assert.Contains("GetFloat", generated);
    }

    [Fact]
    public void ManyColumns_Generates_Ordinal_Constants()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record BigDto(
                                  int Id, string Name, int Qty, decimal Price,
                                  bool Active, System.Guid TraceId, long Ticks,
                                  System.DateTime Created, string Category, float Score);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("const int Ord", generated);
    }

    [Fact]
    public void FewColumns_DoesNot_Generate_Ordinal_Constants()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record SmallDto(int Id, string Name);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.DoesNotContain("const int Ord", generated);
    }

    [Fact]
    public void Generated_Code_Contains_GeneratedCodeAttribute()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record SimpleDto(int Id, string Name);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("[GeneratedCode(\"CaeriusNet.Generator\"", generated);
    }

    [Fact]
    public void Generated_Code_Contains_AggressiveInlining()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record SimpleDto(int Id, string Name);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("MethodImplOptions.AggressiveInlining", generated);
    }

    [Fact]
    public void Generated_Code_Contains_PragmaWarningDisable()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record SimpleDto(int Id, string Name);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("#pragma warning disable CS1591", generated);
    }

    [Fact]
    public void Generated_Code_Contains_XmlDocComments()
    {
        const string source = """
                              using CaeriusNet.Attributes.Dto;
                              namespace Test.Models;
                              [GenerateDto]
                              public sealed partial record SimpleDto(int Id, string Name);
                              """;

        var result = SourceGeneratorTestHelper.RunGenerator<DtoSourceGenerator>(source);

        var generated = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("/// <summary>", generated);
    }
}