# Baut die fertige Snapper.exe in den Ordner "publish".
# Die exe bringt .NET selbst mit. Auf dem Rechner muss nichts installiert sein.
# Aufruf: powershell -File publish.ps1
$root = $PSScriptRoot
dotnet publish "$root\src\Snapper\Snapper.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none -p:DebugSymbols=false `
    -o "$root\publish"
if ($LASTEXITCODE -eq 0) {
    $exe = Get-Item "$root\publish\Snapper.exe"
    Write-Host ("Fertig: {0} ({1} MB)" -f $exe.FullName, [math]::Round($exe.Length / 1MB, 1))
}
