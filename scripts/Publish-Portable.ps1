$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$publish = Join-Path $PWD 'artifacts/Arconte-v0.2.0-win-x64'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish src/Arconte.App/Arconte.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
foreach ($required in @('Arconte.exe', 'Arconte.dll', 'perfiles.json', 'coreclr.dll', 'PresentationFramework.dll')) {
    if (!(Test-Path (Join-Path $publish $required))) { throw "Missing portable dependency: $required" }
}
Copy-Item README.md, CHANGELOG.md $publish
Copy-Item docs $publish -Recurse
Get-ChildItem $publish -Filter *.pdb -Recurse | Remove-Item
$zip = "$publish.zip"
Compress-Archive -Path $publish -DestinationPath $zip -Force
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $zip -Leaf)" | Set-Content "$zip.sha256" -Encoding ascii
