param(
    [Parameter(Mandatory = $true)]
    [string]$OsMessagingPackage
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskPackage = (Resolve-Path -LiteralPath $OsMessagingPackage).Path
$taskArchive = [IO.Compression.ZipFile]::OpenRead($taskPackage)
try {
    $taskEntry = $taskArchive.Entries | Where-Object { $_.Name.EndsWith('.nuspec') } | Select-Object -First 1
    if ($null -eq $taskEntry) { throw 'O arquivo não contém metadados NuGet.' }
    $taskReader = [IO.StreamReader]::new($taskEntry.Open())
    try { [xml]$taskMetadata = $taskReader.ReadToEnd() } finally { $taskReader.Dispose() }
    if ($taskMetadata.package.metadata.id -ne 'FIAP.TechChallenge.OS.Application.Messaging' -or
        $taskMetadata.package.metadata.version -ne '0.1.0') {
        throw 'Informe o pacote FIAP.TechChallenge.OS.Application.Messaging 0.1.0.'
    }
} finally { $taskArchive.Dispose() }
$taskFeed = Join-Path $taskRoot 'artifacts/packages'
[IO.Directory]::CreateDirectory($taskFeed) | Out-Null
$taskDestination = Join-Path $taskFeed 'FIAP.TechChallenge.OS.Application.Messaging.0.1.0.nupkg'
if ([IO.Path]::GetFullPath($taskPackage) -ne [IO.Path]::GetFullPath($taskDestination)) {
    Copy-Item -LiteralPath $taskPackage -Destination $taskDestination
}
[xml]$taskConfig = '<configuration><packageSources><clear /><add key="local" value="" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources></configuration>'
$taskConfig.configuration.packageSources.add[0].SetAttribute('value', $taskFeed)
$taskConfigPath = Join-Path $taskRoot 'artifacts/NuGet.Local.Config'
$taskConfig.Save($taskConfigPath)
dotnet restore (Join-Path $taskRoot 'Execution.slnx') --configfile $taskConfigPath
