param(
	[switch]$GenerateHelpArtifacts,
	[string]$BuildConfiguration = "Debug"
)

dotnet build "$PSScriptRoot/../cirrus.slnx" -c $BuildConfiguration

if ($GenerateHelpArtifacts)
{
	$repoRoot = Resolve-Path "$PSScriptRoot/.."
	$artifactsRoot = Join-Path $repoRoot "artifacts/$BuildConfiguration"
	$outputRoot = Join-Path $repoRoot "artifacts/help"

	dotnet run --project "$repoRoot/tools/Help/Cirrus.Help.Tool/Cirrus.Help.Tool.csproj" -- --artifacts-root "$artifactsRoot" --output-root "$outputRoot"
}
