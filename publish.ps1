param(
    [string]$Version = "",
    [ValidateSet("portable", "standalone", "all")]
    [string]$Mode = "all",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "artifacts",
    [switch]$NoClean
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $MyInvocation.MyCommand.Path))
$outputRootPath = Join-Path $repoRoot $OutputRoot

function Resolve-Version {
    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        return $Version.Trim()
    }

    $tag = (& git -C $repoRoot describe --tags --exact-match HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($tag)) {
        throw "No git tag found on the current commit. Create a release tag or pass -Version vX.Y.Z."
    }

    return $tag.Trim()
}

function Convert-ToAssetNamePart {
    param([string]$Value)

    $invalidChars = [System.IO.Path]::GetInvalidFileNameChars()
    $result = $Value
    foreach ($char in $invalidChars) {
        $result = $result.Replace([string]$char, "-")
    }

    return $result
}

function Resolve-UnderRepo {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $repoFullPath = [System.IO.Path]::GetFullPath($repoRoot)
    $repoPrefix = $repoFullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not $fullPath.Equals($repoFullPath, [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $fullPath.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write outside the repository: $fullPath"
    }

    return $fullPath
}

function Clear-Directory {
    param([string]$Path)

    $fullPath = Resolve-UnderRepo $Path
    $repoFullPath = [System.IO.Path]::GetFullPath($repoRoot)

    if ($fullPath.Equals($repoFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear the repository root. Choose a dedicated output directory."
    }

    if ((Test-Path -LiteralPath $fullPath) -and -not $NoClean) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

function Invoke-DotNetPublish {
    param(
        [string]$Project,
        [string]$OutputPath,
        [bool]$SelfContained
    )

    $selfContainedArg = if ($SelfContained) { "--self-contained" } else { "--no-self-contained" }

    dotnet publish $Project `
        -c $Configuration `
        -r $Runtime `
        $selfContainedArg `
        -o $OutputPath `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:PublishReadyToRun=false `
        -p:PublishSingleFile=false
}

function New-ReleaseAsset {
    param(
        [string]$Suffix,
        [bool]$SelfContained,
        [string]$ReleaseVersion
    )

    $assetVersion = Convert-ToAssetNamePart $ReleaseVersion
    $assetName = "ServerPing-$assetVersion-$Runtime-$Suffix"
    $publishPath = Resolve-UnderRepo (Join-Path $outputRootPath $assetName)
    $zipStagePath = Resolve-UnderRepo (Join-Path $outputRootPath "$assetName-zip")
    $zipRootPath = Join-Path $zipStagePath "ServerPing"
    $zipPath = Resolve-UnderRepo (Join-Path $outputRootPath "$assetName.zip")

    Clear-Directory $publishPath
    Clear-Directory $zipStagePath

    Invoke-DotNetPublish "ServerPing.Service" $publishPath $SelfContained
    Invoke-DotNetPublish "ServerPing.GUI" $publishPath $SelfContained

    New-Item -ItemType Directory -Path $zipRootPath -Force | Out-Null
    Copy-Item -Path (Join-Path $publishPath "*") -Destination $zipRootPath -Recurse -Force

    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path $zipRootPath -DestinationPath $zipPath -Force
    Remove-Item -LiteralPath $zipStagePath -Recurse -Force

    Write-Host "Created $zipPath"
}

$releaseVersion = Resolve-Version
Write-Host "Publishing ServerPing $releaseVersion for $Runtime"

Clear-Directory $outputRootPath

if ($Mode -eq "portable" -or $Mode -eq "all") {
    New-ReleaseAsset "no-dotnet" $false $releaseVersion
}

if ($Mode -eq "standalone" -or $Mode -eq "all") {
    New-ReleaseAsset "dotnet" $true $releaseVersion
}
