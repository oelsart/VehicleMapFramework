# UpdateAboutXmlVersion.ps1

param(
    [string]$AboutXmlPath,
    [string]$BuiltDllPath
)

Write-Host "--- Starting UpdateAboutXmlVersion.ps1 ---"
Write-Host "About.xml Path: $AboutXmlPath"
Write-Host "Built DLL Path: $BuiltDllPath"

if (-not (Test-Path -Path $AboutXmlPath)) {
    Write-Error "Error: About.xml not found at $AboutXmlPath"
    exit 1
}

if (-not (Test-Path -Path $BuiltDllPath)) {
    Write-Error "Error: Built DLL not found at $BuiltDllPath. Ensure the project builds successfully before this step."
    exit 1
}

$newVersion = ""
try {
    $assembly = [System.Reflection.Assembly]::LoadFile($BuiltDllPath)
    $newVersion = $assembly.GetName().Version.ToString()
    Write-Host "Detected DLL Version: $newVersion"
}
catch {
    Write-Error "Error: Failed to load DLL or get version: $_"
    exit 1
}

$versionParts = $newVersion.Split('.')
if ($versionParts.Length -eq 4) {
    $newVersion = "$($versionParts[0]).$($versionParts[1]).$($versionParts[2])"
}

Write-Host "Updating XML file: $AboutXmlPath"
Write-Host "Setting modVersion to: $newVersion"

try {
    [xml]$xmlDoc = Get-Content $AboutXmlPath
    $modVersionNode = $xmlDoc.SelectSingleNode('//ModMetaData/modVersion')

    if ($modVersionNode -ne $null) {
        $modVersionNode.'#text' = $newVersion
        $xmlDoc.Save($AboutXmlPath)
        Write-Host "Successfully updated About.xml to version: $newVersion"
    } else {
        Write-Error "Error: <modVersion> node not found in $AboutXmlPath. Please check the XML structure."
        exit 1
    }
}
catch {
    Write-Error "Error during XML update: $_"
    exit 1
}

Write-Host "--- UpdateAboutXmlVersion.ps1 Finished ---"