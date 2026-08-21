#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 <package-directory> <new-consumer-root>" >&2
  exit 64
fi

package_directory="$(cd "$1" && pwd)"
consumer_root="$2"
script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_directory/../.." && pwd)"

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

mkdir -p "$consumer_root"
# The package-only consumers must resolve the same .NET 10 SDK policy as the release build.  This also
# prevents a globally installed preview SDK from silently changing analyzer or generator behavior.
cp "$repository_root/global.json" "$consumer_root/global.json"
printf '<Project />\n' > "$consumer_root/Directory.Build.props"
printf '<Project />\n' > "$consumer_root/Directory.Build.targets"
printf '<Project />\n' > "$consumer_root/Directory.Packages.props"
dotnet new nugetconfig --force --output "$consumer_root" >/dev/null
dotnet nuget add source "$package_directory" --name caeriusnet-local --configfile "$consumer_root/nuget.config" >/dev/null

for package_id in CaeriusNet CaeriusNet.Redis CaeriusNet.Aspire CaeriusNet.Contracts; do
  consumer="$consumer_root/$package_id"
  dotnet new classlib --name "Consumer.$package_id" --framework net10.0 --no-restore --output "$consumer" >/dev/null
  project="$(find "$consumer" -maxdepth 1 -type f -name '*.csproj' -print -quit)"
  dotnet add "$project" package "$package_id" --version "$version" >/dev/null

  case "$package_id" in
    CaeriusNet)
      cat > "$consumer/Class1.cs" <<'EOF'
using CaeriusNet;

namespace Consumer.Core;

[GenerateDto]
public sealed partial record ConsumerDto(int Id, string Name);

[GenerateTvp(Schema = "dbo", TvpName = "ConsumerRows")]
public sealed partial record ConsumerRows(
    [SqlServerTvpColumn("Id", 0, System.Data.SqlDbType.Int, false)] int Id);

public static class CoreSmoke
{
    public static string Run() => new ConsumerDtoCaeriusCacheCodec().CodecId + ConsumerRows.SqlTypeName;
}
EOF
      ;;
    CaeriusNet.Redis)
      cat > "$consumer/Class1.cs" <<'EOF'
using CaeriusNet.Redis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Consumer.Redis;

public static class RedisSmoke
{
    public static IServiceCollection Add(IServiceCollection services, IConnectionMultiplexer multiplexer) =>
        services.AddSingleton(multiplexer).AddCaeriusNetRedis();
}
EOF
      ;;
    CaeriusNet.Aspire)
      cat > "$consumer/Class1.cs" <<'EOF'
using CaeriusNet.Aspire;
using Microsoft.Extensions.Hosting;

namespace Consumer.Aspire;

public static class AspireSmoke
{
    public static IHostApplicationBuilder Add(IHostApplicationBuilder builder) =>
        builder.AddCaeriusNetAspire("sql");
}
EOF
      ;;
    CaeriusNet.Contracts)
      cat > "$consumer/Class1.cs" <<'EOF'
using CaeriusNet.Contracts;

namespace Consumer.Contracts;

public static class ContractsSmoke
{
    public const int ManifestVersion = ContractsPackage.ManifestVersion;
}
EOF
      ;;
  esac

  dotnet build "$project" --configuration Release --no-restore
done

if grep -R -q '<ProjectReference' "$consumer_root"; then
  echo "Package consumers must not use ProjectReference." >&2
  exit 1
fi

echo "Validated four isolated consumers for CaeriusNet $version."
