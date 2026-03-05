$ErrorActionPreference = "Stop"

$Root = Split-Path $PSScriptRoot -Parent
$Project = Join-Path $Root "FezEditor\FezEditor.csproj"
$Dist = Join-Path $Root "publish"
$Version = (Select-Xml -Path $Project -XPath "//Version").Node.InnerText

New-Item -ItemType Directory -Force -Path $Dist | Out-Null

$Targets = @("win-x64", "linux-x64", "osx-arm64")
foreach ($Rid in $Targets) {
    Write-Host "Publishing $Rid..."
    $PublishDir = Join-Path $Root "FezEditor\bin\publish\$Rid"

    dotnet publish $Project -c Release -r $Rid -o $PublishDir

    if ($Rid.StartsWith("win")) {
        $Archive = Join-Path $Dist "FEZEditor-$Version-$Rid.zip"
        $Files = Get-ChildItem -Path $PublishDir -Recurse | Where-Object { $_.Extension -ne ".pdb" -and !$_.PSIsContainer }
        Compress-Archive -Path $Files.FullName -DestinationPath $Archive -Force
        Write-Host "Created $Archive"
    } else {
        $Archive = Join-Path $Dist "FEZEditor-$Version-$Rid.tar.gz"
        tar -czf $Archive --exclude='*.pdb' -C $PublishDir .
        Write-Host "Created $Archive"
    }
}

Write-Host "Done!"
