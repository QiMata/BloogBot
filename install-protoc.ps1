# Install protoc for Protocol Buffers compilation
$protocVersion = "28.3"
$protocUrl = "https://github.com/protocolbuffers/protobuf/releases/download/v$protocVersion/protoc-$protocVersion-win64.zip"
$downloadPath = "C:\protoc"
$zipPath = "$env:TEMP\protoc.zip"

Write-Host "Installing protoc v$protocVersion..."

# Create directory
if (-not (Test-Path $downloadPath)) {
    New-Item -ItemType Directory -Path $downloadPath -Force | Out-Null
}

# Download
Write-Host "Downloading from $protocUrl..."
Invoke-WebRequest -Uri $protocUrl -OutFile $zipPath

# Extract
Write-Host "Extracting to $downloadPath..."
Expand-Archive -Path $zipPath -DestinationPath $downloadPath -Force

# Cleanup
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue

# Verify
$protocExe = Join-Path $downloadPath "bin\protoc.exe"
if (Test-Path $protocExe) {
    Write-Host ""
    Write-Host "protoc installed successfully!"
    Write-Host "Location: $protocExe"
    & $protocExe --version
    Write-Host ""
    Write-Host "To regenerate proto files, run:"
    Write-Host "  .\Exports\BotCommLayer\Models\ProtoDef\protocsharp.bat .\ .\.. `"C:\protoc\bin\protoc.exe`""
} else {
    Write-Host "ERROR: Installation failed - protoc.exe not found"
    exit 1
}
