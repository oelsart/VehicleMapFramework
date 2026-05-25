$SourceDir = "../../../../RimWorldWin64_Data/Managed"
$OutputDir = "./Stubs"

# 出力先フォルダがなければ作成
if (-not (Test-Path $OutputDir))
{
    New-Item -Path $OutputDir -ItemType Directory
}

Get-ChildItem -Path $SourceDir -Filter "*.dll" | ForEach-Object {
    $dllName = $_.Name
    $inputPath = $_.FullName
    $outputPath = Join-Path $OutputDir $dllName

    & assembly-publicizer $inputPath -o $outputPath --strip-only
}