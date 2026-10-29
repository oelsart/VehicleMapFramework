param(
    [string] $SolutionDir = $PSScriptRoot
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $SolutionDir "../..")
$devRoot = Join-Path $repoRoot "../VehicleMapFrameworkDev"

function Invoke-Robocopy {
    param(
        [string] $Source,
        [string] $Destination,
        [string[]] $Arguments
    )

    robocopy $Source $Destination @Arguments

    if ($LASTEXITCODE -gt 1) {
        exit $LASTEXITCODE
    }

    $global:LASTEXITCODE = 0
}

Invoke-Robocopy (Join-Path $repoRoot "1.5") (Join-Path $devRoot "1.5") @("/MIR", "/E")
Invoke-Robocopy (Join-Path $repoRoot "1.6") (Join-Path $devRoot "1.6") @("/MIR", "/E", "/XD", "AssetBundles")
Invoke-Robocopy (Join-Path $repoRoot "1.7") (Join-Path $devRoot "1.7") @("/MIR", "/E", "/XD", "AssetBundles")
Invoke-Robocopy (Join-Path $repoRoot "Common") (Join-Path $devRoot "Common") @("/MIR", "/E")
Invoke-Robocopy (Join-Path $repoRoot "Dev/About") (Join-Path $devRoot "About") @("/MIR", "/E")
Invoke-Robocopy (Join-Path $repoRoot "Dev/AssetBundles") (Join-Path $devRoot "2022.3/AssetBundles") @("/MIR", "/E")
Invoke-Robocopy (Join-Path $repoRoot "Dev") $devRoot @("LoadFolders.xml")
Invoke-Robocopy $repoRoot $devRoot @("LICENSE")
Invoke-Robocopy $repoRoot $devRoot @("LICENSE.VehicleFramework")
