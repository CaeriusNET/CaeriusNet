#!/usr/bin/env bash
set -euo pipefail

# Publishes a clean, package-only Native AOT consumer. This script deliberately
# runs on Linux because a linux-x64 Native AOT binary must be produced and
# executed on its target operating system. It never prints SQL connection data.

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 <package-directory> <new-consumer-root>" >&2
  exit 64
fi

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "Native AOT package smoke is Linux-only: run it on a linux-x64 CI runner." >&2
  exit 69
fi

package_directory="$(cd "$1" && pwd)"
consumer_root="$2"

if [[ ! -d "$package_directory" ]]; then
  echo "Package directory does not exist: $package_directory" >&2
  exit 66
fi

if [[ -e "$consumer_root" ]]; then
  echo "Consumer root must not already exist: $consumer_root" >&2
  exit 73
fi

core_package="$(find "$package_directory" -maxdepth 1 -type f -name 'CaeriusNet.*.nupkg' ! -name '*.symbols.nupkg' | \
  sed -nE 's#.*/CaeriusNet\.([0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?)\.nupkg#\1#p' | head -n 1)"
if [[ -z "$core_package" ]]; then
  echo "Unable to determine the CaeriusNet package version in $package_directory." >&2
  exit 65
fi
version="$core_package"

for package_id in CaeriusNet.Redis CaeriusNet.Aspire; do
  if [[ ! -f "$package_directory/$package_id.$version.nupkg" ]]; then
    echo "Required package '$package_id.$version.nupkg' is missing from $package_directory." >&2
    exit 65
  fi
done

mkdir -p "$consumer_root"
printf '<Project />\n' > "$consumer_root/Directory.Build.props"
printf '<Project />\n' > "$consumer_root/Directory.Build.targets"
printf '<Project />\n' > "$consumer_root/Directory.Packages.props"

package_source="${package_directory//&/&amp;}"
package_source="${package_source//</&lt;}"
package_source="${package_source//>/&gt;}"
cat > "$consumer_root/NuGet.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-caeriusnet" value="$package_source" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local-caeriusnet">
      <package pattern="CaeriusNet*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

cat > "$consumer_root/CaeriusNet.NativeAotPackageSmoke.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishAot>true</PublishAot>
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>full</TrimMode>
    <SuppressTrimAnalysisWarnings>false</SuppressTrimAnalysisWarnings>
    <InvariantGlobalization>true</InvariantGlobalization>
    <LangVersion>14.0</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CaeriusNet" Version="$version" />
    <PackageReference Include="CaeriusNet.Redis" Version="$version" />
    <PackageReference Include="CaeriusNet.Aspire" Version="$version" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.11" />
  </ItemGroup>
</Project>
EOF

cat > "$consumer_root/Program.cs" <<'EOF'
using System.Buffers;
using System.Data;
using CaeriusNet;
using CaeriusNet.Aspire;
using CaeriusNet.Commands;
using CaeriusNet.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NativeAotPackageSmoke;

internal static class Program
{
internal static async Task<int> Main()
{
var sqlConnectionString = Environment.GetEnvironmentVariable("CAERIUS_AOT_SQLSERVER_CONNECTION_STRING");
var sqlProcedure = Environment.GetEnvironmentVariable("CAERIUS_AOT_SQLSERVER_PROCEDURE") ?? "CaeriusNativeAotSmoke";
var expectedScalar = int.TryParse(
    Environment.GetEnvironmentVariable("CAERIUS_AOT_SQLSERVER_EXPECTED_SCALAR"),
    out var parsedExpectedScalar)
    ? parsedExpectedScalar
    : 1;

var codec = new NativeAotSmokeDtoCaeriusCacheCodec();
var payload = new ArrayBufferWriter<byte>();
codec.Encode(payload, new NativeAotSmokeDto(42, "native-aot"));
var decoded = codec.Decode(payload.WrittenSpan);
if (decoded != new NativeAotSmokeDto(42, "native-aot"))
    return 1;

var services = new ServiceCollection();
services.AddCaeriusNet(options => options.UseSqlServer(
    sqlConnectionString ?? "Server=localhost;Database=master;Integrated Security=true;Encrypt=Strict;TrustServerCertificate=False"));
services.AddCaeriusNetRedis();
services.AddNativeAotSmokeDtoCaeriusCodec();

var hostBuilder = Host.CreateApplicationBuilder();
hostBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:sql"] = sqlConnectionString ?? "Server=localhost;Database=master;Integrated Security=true;Encrypt=Strict;TrustServerCertificate=False"
});
hostBuilder.AddCaeriusNetAspire("sql");
using var host = hostBuilder.Build();

if (string.IsNullOrWhiteSpace(sqlConnectionString))
{
    Console.WriteLine("Native AOT package smoke passed. SQL Server 2022/2025 execution is gated: set CAERIUS_AOT_SQLSERVER_2022_CONNECTION_STRING and/or CAERIUS_AOT_SQLSERVER_2025_CONNECTION_STRING in the invoking environment.");
    return 0;
}

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var context = scope.ServiceProvider.GetRequiredService<CaeriusNet.Abstractions.ICaeriusNetDbContext>();
var command = new StoredProcedureCommandBuilder("dbo", sqlProcedure).Build();
var actualScalar = await context.ExecuteScalarAsync<int>(command);
if (actualScalar != expectedScalar)
    return 2;

Console.WriteLine($"Native AOT SQL Server smoke passed for {Environment.GetEnvironmentVariable("CAERIUS_AOT_SQLSERVER_LABEL") ?? "configured target"}.");
return 0;
}
}

[GenerateDto]
public sealed partial record NativeAotSmokeDto(int Id, string Name);
EOF

if grep -R -q '<ProjectReference' "$consumer_root"; then
  echo "Native AOT package smoke consumer must not use ProjectReference." >&2
  exit 1
fi

export NUGET_PACKAGES="$consumer_root/nuget-packages"
project="$consumer_root/CaeriusNet.NativeAotPackageSmoke.csproj"
publish_directory="$consumer_root/publish"
# This fresh, isolated consumer has no checked-in lock file. Bootstrap its lock graph from the
# package feed, then require the immediately repeated restore to consume that exact graph.
dotnet msbuild "$project" /t:Restore \
  /p:RestorePackagesWithLockFile=true \
  /p:RestoreLockedMode=false \
  "/p:RestoreConfigFile=$consumer_root/NuGet.config"
dotnet restore "$project" --configfile "$consumer_root/NuGet.config" --locked-mode
dotnet publish "$project" --configuration Release --no-restore --output "$publish_directory"

binary="$publish_directory/CaeriusNet.NativeAotPackageSmoke"
if [[ ! -x "$binary" ]]; then
  echo "Native AOT publish did not produce executable '$binary'." >&2
  exit 1
fi

run_sql_server_smoke_if_configured() {
  local label="$1"
  local connection_environment_variable="$2"
  local connection_string="${!connection_environment_variable:-}"

  if [[ -z "$connection_string" ]]; then
    echo "SQL Server $label Native AOT execution is gated: $connection_environment_variable is not configured."
    return
  fi

  CAERIUS_AOT_SQLSERVER_CONNECTION_STRING="$connection_string" \
  CAERIUS_AOT_SQLSERVER_LABEL="$label" \
  "$binary"
}

if [[ -z "${CAERIUS_AOT_SQLSERVER_2022_CONNECTION_STRING:-}" && -z "${CAERIUS_AOT_SQLSERVER_2025_CONNECTION_STRING:-}" ]]; then
  "$binary"
else
  run_sql_server_smoke_if_configured "2022" "CAERIUS_AOT_SQLSERVER_2022_CONNECTION_STRING"
  run_sql_server_smoke_if_configured "2025" "CAERIUS_AOT_SQLSERVER_2025_CONNECTION_STRING"
fi

echo "Validated Native AOT linux-x64 package-only consumer for CaeriusNet $version."
