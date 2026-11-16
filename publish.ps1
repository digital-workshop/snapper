# Baut die fertige Snapper.exe in den Ordner "publish".
# Aufruf: powershell -File publish.ps1
$root = $PSScriptRoot
dotnet publish "$root\src\Snapper\Snapper.csproj" `
    -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o "$root\publish"
if ($LASTEXITCODE -eq 0) { Write-Host "Fertig: $root\publish\Snapper.exe" }
