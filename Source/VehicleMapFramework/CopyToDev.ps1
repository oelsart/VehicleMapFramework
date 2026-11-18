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

Invoke-Robocopy (Join-Path $repoRoot "1.5") (Join-Path $devRoot "1.5") @("/MIR", "/E") | Out-Null
Invoke-Robocopy (Join-Path $repoRoot "1.6") (Join-Path $devRoot "1.6") @("/MIR", "/E", "/XD", "AssetBundles") | Out-Null
Invoke-Robocopy (Join-Path $repoRoot "1.7") (Join-Path $devRoot "1.7") @("/MIR", "/E") | Out-Null
Invoke-Robocopy (Join-Path $repoRoot "Common") (Join-Path $devRoot "Common") @("/MIR", "/E") | Out-Null
Invoke-Robocopy (Join-Path $repoRoot "Dev/About") (Join-Path $devRoot "About") @("/MIR", "/E") | Out-Null
Invoke-Robocopy (Join-Path $repoRoot "Dev") $devRoot @("LoadFolders.xml") | Out-Null
Invoke-Robocopy $repoRoot $devRoot @("LICENSE") | Out-Null
Invoke-Robocopy $repoRoot $devRoot @("LICENSE.VehicleFramework") | Out-Null
