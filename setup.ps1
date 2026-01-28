<#
.SYNOPSIS
    Automates the setup of BloogBot by downloading and configuring the required World of Warcraft 1.12 (Vanilla) client files.

.DESCRIPTION
    This script downloads the WoW 1.12 client from a specified URL, validates its integrity, and copies the essential 
    game data files required for BloogBot operation to a specified bin directory. The script supports flexible 
    configuration for different deployment scenarios and provides comprehensive error handling and user feedback.

    The script will copy the following files and directories to the bin directory:
    - maps/ (navigation data for bot movement)
    - mmaps/ (movement maps for pathfinding algorithms)
    - vmaps/ (visual maps for line-of-sight calculations)
    - Data/terrain.MPQ (terrain data for world information)
    - CreatureModelData.dbc (creature model information)

.PARAMETER ClientDirectory
    Specifies the directory where the WoW 1.12 client will be extracted or is already located.
    Default: ".\WoW_Client"

.PARAMETER BinDirectory
    Specifies the target directory where game data files will be copied for BloogBot services.
    Default: ".\bin"

.PARAMETER ClientZipUrl
    Specifies the URL from which to download the WoW 1.12 client ZIP file. This parameter is required 
    unless -BypassClientDownload is used and a valid client already exists.
    Example: "https://archive.org/download/WoWVanilla_1121/WoW_enUS_1121.zip"

.PARAMETER BypassClientDownload
    When specified, skips the client download and validation process. Use this when you already have 
    a valid WoW 1.12 client in the ClientDirectory location.

.OUTPUTS
    Exit codes:
    0 - Success: Setup completed successfully
    1 - Error: Critical failure (download failed, validation failed, etc.)
    2 - Warning: Completed with warnings (some files not copied)

.EXAMPLE
    .\setup.ps1 -ClientZipUrl "https://archive.org/download/WoWVanilla_1121/WoW_enUS_1121.zip"
    
    Downloads the WoW client from the specified URL and sets up the game data files in the default directories.

.EXAMPLE
    .\setup.ps1 -ClientDirectory "D:\Games\WoW_1.12" -BypassClientDownload
    
    Uses an existing WoW client installation and copies the required files to the default bin directory.

.EXAMPLE
    .\setup.ps1 -ClientDirectory "E:\WoW_Client" -BinDirectory "C:\BloogBot\gamedata" -ClientZipUrl "https://your-url.zip"
    
    Downloads the client to a custom directory and copies game data to a custom bin directory.

.NOTES
    File Name      : setup.ps1
    Author         : BloogBot Development Team
    Prerequisite   : PowerShell 5.1 or later, Internet connection (for downloads)
    
    Security Requirements:
    - Execution policy must allow script execution
    - Write permissions required for target directories
    - Sufficient disk space for WoW client (~4-6 GB)
    
    Legal Considerations:
    - Ensure you have legal rights to use any WoW client files
    - This script is for educational and development purposes
    - Respect all applicable terms of service and copyright laws

.LINK
    https://github.com/YourRepo/BloogBot

.LINK
    SETUP_README.md
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$ClientDirectory = ".\WoW_Client",
    
    [Parameter(Mandatory = $false)]
    [string]$BinDirectory = ".\bin",
    
    [Parameter(Mandatory = $false)]
    [string]$ClientZipUrl = "",
    
    [Parameter(Mandatory = $false)]
    [switch]$BypassClientDownload = $false
)

# Script configuration
$ErrorActionPreference = "Stop"
$ProgressPreference = "Continue"

# Colors for output
$ColorSuccess = "Green"
$ColorWarning = "Yellow"
$ColorError = "Red"
$ColorInfo = "Cyan"

function Write-ColoredOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Write-Header {
    param([string]$Title)
    Write-Host ""
    Write-ColoredOutput ("=" * 60) $ColorInfo
    Write-ColoredOutput "  $Title" $ColorInfo
    Write-ColoredOutput ("=" * 60) $ColorInfo
    Write-Host ""
}

function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-WoWClientValid {
    param([string]$Path)
    
    $requiredFiles = @(
        "Data\terrain.MPQ",
        "CreatureModelData.dbc"
    )
    
    $requiredDirs = @(
        "maps",
        "mmaps", 
        "vmaps"
    )
    
    $valid = $true
    
    foreach ($file in $requiredFiles) {
        $filePath = Join-Path $Path $file
        if (-not (Test-Path $filePath)) {
            Write-ColoredOutput "Missing required file: $file" $ColorWarning
            $valid = $false
        }
    }
    
    foreach ($dir in $requiredDirs) {
        $dirPath = Join-Path $Path $dir
        if (-not (Test-Path $dirPath -PathType Container)) {
            Write-ColoredOutput "Missing required directory: $dir" $ColorWarning
            $valid = $false
        }
    }
    
    return $valid
}

function Download-WoWClient {
    param(
        [string]$Url,
        [string]$DestinationPath
    )
    
    if ([string]::IsNullOrEmpty($Url)) {
        Write-ColoredOutput "No download URL provided. Please specify -ClientZipUrl parameter." $ColorError
        Write-ColoredOutput "Example URLs for WoW 1.12 client:" $ColorInfo
        Write-ColoredOutput "  - Archive.org: https://archive.org/download/WoWVanilla_1121/WoW_enUS_1121.zip" $ColorInfo
        Write-ColoredOutput "  - Community sources (verify legitimacy before use)" $ColorInfo
        throw "Download URL required"
    }
    
    $tempZip = Join-Path $env:TEMP "wow_client_temp.zip"
    
    try {
        Write-ColoredOutput "Downloading WoW 1.12 client..." $ColorInfo
        Write-ColoredOutput "URL: $Url" $ColorInfo
        Write-ColoredOutput "This may take a while depending on your connection..." $ColorWarning
        
        # Create webclient with progress
        $webClient = New-Object System.Net.WebClient
        
        # Register progress event
        Register-ObjectEvent -InputObject $webClient -EventName DownloadProgressChanged -Action {
            $percent = $Event.SourceEventArgs.ProgressPercentage
            $received = $Event.SourceEventArgs.BytesReceived
            $total = $Event.SourceEventArgs.TotalBytesToReceive
            
            if ($total -gt 0) {
                $receivedMB = [math]::Round($received / 1MB, 2)
                $totalMB = [math]::Round($total / 1MB, 2)
                Write-Progress -Activity "Downloading WoW Client" -Status "$percent% Complete ($receivedMB MB / $totalMB MB)" -PercentComplete $percent
            }
        } | Out-Null
        
        $webClient.DownloadFile($Url, $tempZip)
        Write-Progress -Activity "Downloading WoW Client" -Completed
        
        Write-ColoredOutput "Download completed successfully!" $ColorSuccess
        
        # Extract the zip file
        Write-ColoredOutput "Extracting client files..." $ColorInfo
        if (Test-Path $DestinationPath) {
            Remove-Item $DestinationPath -Recurse -Force
        }
        
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($tempZip, $DestinationPath)
        
        Write-ColoredOutput "Extraction completed!" $ColorSuccess
        
    }
    catch {
        Write-ColoredOutput "Failed to download or extract WoW client: $($_.Exception.Message)" $ColorError
        throw
    }
    finally {
        # Cleanup
        if (Test-Path $tempZip) {
            Remove-Item $tempZip -Force
        }
        if ($webClient) {
            $webClient.Dispose()
        }
        Get-EventSubscriber | Unregister-Event
    }
}

function Copy-GameDataFiles {
    param(
        [string]$SourcePath,
        [string]$TargetPath
    )
    
    Write-ColoredOutput "Copying game data files..." $ColorInfo
    
    # Ensure target directory exists
    if (-not (Test-Path $TargetPath)) {
        New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
    }
    
    # Files and directories to copy
    $itemsToCopy = @(
        @{ Source = "maps"; Type = "Directory" },
        @{ Source = "mmaps"; Type = "Directory" },
        @{ Source = "vmaps"; Type = "Directory" },
        @{ Source = "Data\terrain.MPQ"; Type = "File" },
        @{ Source = "CreatureModelData.dbc"; Type = "File" }
    )
    
    $successCount = 0
    $totalCount = $itemsToCopy.Count
    
    foreach ($item in $itemsToCopy) {
        $sourcePath = Join-Path $SourcePath $item.Source
        $fileName = Split-Path $item.Source -Leaf
        $targetItemPath = Join-Path $TargetPath $fileName
        
        try {
            if ($item.Type -eq "Directory") {
                if (Test-Path $sourcePath -PathType Container) {
                    if (Test-Path $targetItemPath) {
                        Remove-Item $targetItemPath -Recurse -Force
                    }
                    Copy-Item $sourcePath $targetItemPath -Recurse -Force
                    Write-ColoredOutput "? Copied directory: $($item.Source)" $ColorSuccess
                    $successCount++
                }
                else {
                    Write-ColoredOutput "? Source directory not found: $($item.Source)" $ColorWarning
                }
            }
            else {
                if (Test-Path $sourcePath -PathType Leaf) {
                    Copy-Item $sourcePath $targetItemPath -Force
                    Write-ColoredOutput "? Copied file: $($item.Source)" $ColorSuccess
                    $successCount++
                }
                else {
                    Write-ColoredOutput "? Source file not found: $($item.Source)" $ColorWarning
                }
            }
        }
        catch {
            Write-ColoredOutput "? Failed to copy $($item.Source): $($_.Exception.Message)" $ColorError
        }
    }
    
    Write-ColoredOutput "Copy operation completed: $successCount/$totalCount items copied successfully" $ColorInfo
    
    if ($successCount -lt $totalCount) {
        Write-ColoredOutput "Some files were not copied. Please check the warnings above." $ColorWarning
        return $false
    }
    
    return $true
}

function Show-Summary {
    param(
        [string]$ClientPath,
        [string]$BinPath,
        [bool]$Success
    )
    
    Write-Header "Setup Summary"
    
    if ($Success) {
        Write-ColoredOutput "? Setup completed successfully!" $ColorSuccess
    }
    else {
        Write-ColoredOutput "? Setup completed with warnings or errors" $ColorWarning
    }
    
    Write-Host ""
    Write-ColoredOutput "Paths:" $ColorInfo
    Write-ColoredOutput "  Client Directory: $ClientPath" "White"
    Write-ColoredOutput "  Bin Directory: $BinPath" "White"
    
    Write-Host ""
    Write-ColoredOutput "Next Steps:" $ColorInfo
    Write-ColoredOutput "  1. Verify that the bin directory contains all required files" "White"
    Write-ColoredOutput "  2. Configure your BloogBot services to use the bin directory" "White"
    Write-ColoredOutput "  3. For pathfinding tests, set BLOOGBOT_DATA_DIR environment variable:" "White"
    Write-ColoredOutput "     `$env:BLOOGBOT_DATA_DIR = '$BinPath'" "Cyan"
    Write-ColoredOutput "     Or use absolute path to your client directory with nav data" "Cyan"
    Write-ColoredOutput "  4. Start the WoW server using the WWoW.Systems AppHost" "White"
    
    if (-not $Success) {
        Write-Host ""
        Write-ColoredOutput "If you encountered issues:" $ColorWarning
        Write-ColoredOutput "  - Ensure you have sufficient disk space" "White"
        Write-ColoredOutput "  - Check that the WoW client download is a valid 1.12 client" "White"
        Write-ColoredOutput "  - Verify file permissions in the target directories" "White"
    }
}

# Main execution
try {
    Write-Header "BloogBot WoW 1.12 Client Setup"
    
    Write-ColoredOutput "Configuration:" $ColorInfo
    Write-ColoredOutput "  Client Directory: $ClientDirectory" "White"
    Write-ColoredOutput "  Bin Directory: $BinDirectory" "White"
    Write-ColoredOutput "  Bypass Download: $BypassClientDownload" "White"
    
    if (-not $BypassClientDownload -and -not [string]::IsNullOrEmpty($ClientZipUrl)) {
        Write-ColoredOutput "  Download URL: $ClientZipUrl" "White"
    }
    
    Write-Host ""
    
    # Check if client directory exists and is valid
    $clientExists = Test-Path $ClientDirectory -PathType Container
    $clientValid = $false
    
    if ($clientExists) {
        Write-ColoredOutput "Client directory found. Validating..." $ColorInfo
        $clientValid = Test-WoWClientValid -Path $ClientDirectory
        
        if ($clientValid) {
            Write-ColoredOutput "? Valid WoW 1.12 client found!" $ColorSuccess
        }
        else {
            Write-ColoredOutput "? Client directory exists but appears incomplete" $ColorWarning
        }
    }
    else {
        Write-ColoredOutput "Client directory not found" $ColorInfo
    }
    
    # Handle client download/validation
    if (-not $BypassClientDownload) {
        if (-not $clientValid) {
            if ([string]::IsNullOrEmpty($ClientZipUrl)) {
                Write-ColoredOutput "Client download is required but no URL provided." $ColorError
                Write-ColoredOutput "Please either:" $ColorInfo
                Write-ColoredOutput "  1. Provide -ClientZipUrl parameter with a valid WoW 1.12 client download link" "White"
                Write-ColoredOutput "  2. Manually extract a WoW 1.12 client to: $ClientDirectory" "White"
                Write-ColoredOutput "  3. Use -BypassClientDownload if you want to skip this step" "White"
                exit 1
            }
            
            Download-WoWClient -Url $ClientZipUrl -DestinationPath $ClientDirectory
            
            # Validate the downloaded client
            Write-ColoredOutput "Validating downloaded client..." $ColorInfo
            $clientValid = Test-WoWClientValid -Path $ClientDirectory
            
            if (-not $clientValid) {
                Write-ColoredOutput "Downloaded client appears to be invalid or incomplete" $ColorError
                Write-ColoredOutput "Please verify the download URL points to a complete WoW 1.12 client" $ColorWarning
                exit 1
            }
        }
        else {
            Write-ColoredOutput "Skipping download - valid client already exists" $ColorSuccess
        }
    }
    else {
        if (-not $clientValid) {
            Write-ColoredOutput "Client validation bypassed, but client appears invalid" $ColorWarning
            Write-ColoredOutput "Proceeding anyway as requested..." $ColorWarning
        }
        else {
            Write-ColoredOutput "Client validation bypassed - valid client detected" $ColorSuccess
        }
    }
    
    # Copy files to bin directory
    Write-Header "Copying Game Data Files"
    $copySuccess = Copy-GameDataFiles -SourcePath $ClientDirectory -TargetPath $BinDirectory
    
    # Show summary
    Show-Summary -ClientPath (Resolve-Path $ClientDirectory -ErrorAction SilentlyContinue) -BinPath (Resolve-Path $BinDirectory -ErrorAction SilentlyContinue) -Success $copySuccess
    
    if ($copySuccess) {
        Write-ColoredOutput "Setup completed successfully!" $ColorSuccess
        exit 0
    }
    else {
        Write-ColoredOutput "Setup completed with warnings" $ColorWarning
        exit 2
    }
}
catch {
    Write-ColoredOutput "Setup failed: $($_.Exception.Message)" $ColorError
    Write-ColoredOutput "Stack trace: $($_.ScriptStackTrace)" $ColorError
    exit 1
}