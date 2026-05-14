namespace CaeriusNet.Generator.Tests.AutoContracts;

public sealed class AutoContractsSourceGeneratorTests
{
    private const string ValidManifest = """
                                         {
                                           "version": 1,
                                           "namespace": "Consumer.Contracts",
                                           "tableTypes": [
                                             {
                                               "schema": "dbo",
                                               "name": "UserIdList",
                                               "clrName": "UserIdListTvp",
                                               "columns": [
                                                 {
                                                   "ordinal": 1,
                                                   "name": "UserId",
                                                   "sqlType": "int",
                                                   "clrType": "int",
                                                   "nullable": false
                                                 }
                                               ],
                                               "contractHash": "sha256:table"
                                             }
                                           ],
                                           "procedures": [
                                             {
                                               "schema": "dbo",
                                               "name": "User_RetrieveByIds",
                                               "clrName": "UserRetrieveByIdsProcedure",
                                               "parametersClrName": "UserRetrieveByIdsParameters",
                                               "resultClrName": "UserRetrieveByIdsResult",
                                               "parameters": [
                                                 {
                                                   "ordinal": 1,
                                                   "name": "Ids",
                                                   "sqlType": "dbo.UserIdList",
                                                   "clrType": "ReadOnlyMemory<UserIdListTvp>",
                                                   "isTableType": true,
                                                   "nullable": false
                                                 },
                                                 {
                                                   "ordinal": 2,
                                                   "name": "IncludeDisabled",
                                                   "sqlType": "bit",
                                                   "clrType": "bool",
                                                   "isTableType": false,
                                                   "nullable": false
                                                 }
                                               ],
                                               "resultSet": {
                                                 "status": "Available",
                                                 "columns": [
                                                   {
                                                     "ordinal": 1,
                                                     "name": "UserId",
                                                     "sqlType": "int",
                                                     "clrType": "int",
                                                     "nullable": false
                                                   },
                                                   {
                                                     "ordinal": 2,
                                                     "name": "Username",
                                                     "sqlType": "nvarchar(128)",
                                                     "clrType": "string",
                                                     "nullable": false,
                                                     "maxLength": 256
                                                   }
                                                 ]
                                               },
                                               "contractHash": "sha256:procedure"
                                             }
                                           ]
                                         }
                                         """;

    [Fact]
    public void ValidManifest_Generates_Procedure_Tvp_Dto_And_Builder_Extensions()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest(ValidManifest));

        Assert.Single(result.GeneratedTrees);
        var generated = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("public readonly partial record struct UserIdListTvp", generated);
        Assert.Contains("public readonly partial struct UserRetrieveByIdsProcedure", generated);
        Assert.Contains("public sealed partial record UserRetrieveByIdsParameters", generated);
        Assert.Contains("ICaeriusGeneratedProcedureParameters<UserRetrieveByIdsProcedure, UserRetrieveByIdsParameters>",
            generated);
        Assert.Contains("public static void Bind(StoredProcedureParametersBuilder<UserRetrieveByIdsProcedure> builder",
            generated);
        Assert.Contains("public sealed partial record UserRetrieveByIdsResult", generated);
        Assert.Contains("public static partial class UserRetrieveByIdsProcedureCacheKeys", generated);
        Assert.Contains("public static string ByParameters(UserRetrieveByIdsParameters parameters)", generated);
        Assert.Contains("ReadOnlyMemory<UserIdListTvp> Ids,", generated);
        Assert.Contains("bool IncludeDisabled", generated);
        Assert.Contains("public static SqlMetaData[] Metadata => (SqlMetaData[])_metadata.Clone();", generated);
        Assert.Contains("AppendString(hash, \"sha256:table\");", generated);
        Assert.Contains("return builder.WithParameters(new UserRetrieveByIdsParameters(Ids, IncludeDisabled));",
            generated);
        Assert.Contains("[AutoContractGenerateTvp", generated);
        Assert.Contains("[AutoContractGenerateProcedure", generated);
        Assert.Contains("[AutoContractGenerateDto", generated);
        Assert.Contains("StoredProcedureParametersBuilder<UserRetrieveByIdsProcedure>", generated);
        Assert.Contains("reader.GetInt32(0)", generated);
        Assert.Contains("reader.GetString(1)", generated);
        Assert.DoesNotContain("[Caerius" + "GeneratedTvp", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("[Caerius" + "GeneratedProcedure", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("[Caerius" + "GeneratedDto", generated, StringComparison.Ordinal);
        Assert.DoesNotContain($"V{1}", generated, StringComparison.Ordinal);
        Assert.DoesNotContain($"v{1}", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidManifest_Generated_Code_Compiles()
    {
        var (_, compilation) = SourceGeneratorTestHelper.RunGeneratorWithCompilation<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest(ValidManifest));

        var diagnostics = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(diagnostic => diagnostic.Id != "CS0009")
            .ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void NonManifestAdditionalFile_Is_Ignored()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            new TestAdditionalText("ignored.json", ValidManifest));

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void CustomManifestPath_WithMetadata_Is_Consumed()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest(ValidManifest, "generated.contracts.json", true));

        Assert.Single(result.GeneratedTrees);
    }

    [Fact]
    public void ManifestFileName_WithMetadataFalse_Is_Ignored()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest(ValidManifest, isManifest: false));

        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void MultipleManifests_WithSameNamespace_Use_Distinct_HintNames()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest(ValidManifest, "a/caerius.contracts.json"),
            Manifest("""
                     {
                       "version": 1,
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": [
                         {
                           "schema": "dbo",
                           "name": "CountUsers",
                           "clrName": "CountUsersProcedure",
                           "parametersClrName": "CountUsersParameters",
                           "parameters": [],
                           "resultSet": { "status": "None", "columns": [] },
                           "contractHash": "sha256:count"
                         }
                       ]
                     }
                     """, "b/caerius.contracts.json"));

        Assert.Equal(2, result.GeneratedTrees.Length);

        var hintNames = result.GeneratedTrees
            .Select(tree => Path.GetFileName(tree.FilePath))
            .ToArray();
        Assert.Equal(2, hintNames.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ScalarParameterFacets_Are_Emitted()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 1,
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": [
                         {
                           "schema": "dbo",
                           "name": "SearchUsers",
                           "clrName": "SearchUsersProcedure",
                           "parametersClrName": "SearchUsersParameters",
                           "parameters": [
                             {
                               "ordinal": 1,
                               "name": "Name",
                               "sqlType": "nvarchar(64)",
                               "clrType": "string",
                               "isTableType": false,
                               "nullable": false,
                               "maxLength": 128
                             },
                             {
                               "ordinal": 2,
                               "name": "Amount",
                               "sqlType": "decimal(18,2)",
                               "clrType": "decimal",
                               "isTableType": false,
                               "nullable": false,
                               "precision": 18,
                               "scale": 2
                             }
                           ],
                           "resultSet": { "status": "None", "columns": [] },
                           "contractHash": "sha256:test"
                         }
                       ]
                     }
                     """));

        var generated = result.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("SqlDbType.NVarChar, size: 64", generated);
        Assert.Contains("SqlDbType.Decimal, precision: 18, scale: 2", generated);
    }

    [Fact]
    public void UndeterminedResultSet_Is_Not_Emitted_By_Generator()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 1,
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": [
                         {
                           "schema": "dbo",
                           "name": "SomeProcedure",
                           "clrName": "SomeProcedure",
                           "parametersClrName": "SomeProcedureParameters",
                           "resultClrName": "SomeProcedureResult",
                           "parameters": [],
                           "resultSet": { "status": "Undetermined", "columns": [] },
                           "contractHash": "sha256:test"
                         }
                       ]
                     }
                     """));

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void UnsupportedManifestVersion_Is_Not_Emitted_By_Generator()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 2,
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [],
                       "procedures": []
                     }
                     """));

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void ManifestWithMemberNameCollision_Is_Not_Emitted_By_Generator()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<AutoContractsSourceGenerator>(
            "namespace Consumer;",
            Manifest("""
                     {
                       "version": 1,
                       "namespace": "Consumer.Contracts",
                       "tableTypes": [
                         {
                           "schema": "dbo",
                           "name": "CollisionRows",
                           "clrName": "CollisionRowsTvp",
                           "columns": [
                             { "ordinal": 1, "name": "User-Id", "sqlType": "int", "clrType": "int", "nullable": false },
                             { "ordinal": 2, "name": "User_Id", "sqlType": "int", "clrType": "int", "nullable": false }
                           ],
                           "contractHash": "sha256:table"
                         }
                       ],
                       "procedures": []
                     }
                     """));

        Assert.Empty(result.GeneratedTrees);
    }

    private static TestAdditionalText Manifest(
        string json,
        string path = "caerius.contracts.json",
        bool? isManifest = null)
    {
        var options = isManifest.HasValue
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_metadata.AdditionalFiles.CaeriusContractManifest"] =
                    isManifest.Value ? "true" : "false"
            }
            : null;

        return new TestAdditionalText(path, json, options);
    }
}
