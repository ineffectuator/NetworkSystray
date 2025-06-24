# PowerShell script to download and install WiX Toolset

# Define the WiX Toolset version and download URL
$wixVersion = "3.11.2"
$wixZipUrl = "https://github.com/wixtoolset/wix3/releases/download/wix3112rtm/wix311-binaries.zip"
$zipFileName = "wix311-binaries.zip"
# Use a path accessible by the user, e.g., in AppData
$baseInstallDir = Join-Path $env:LOCALAPPDATA "WiXToolset"
$installDir = Join-Path $baseInstallDir "v$wixVersion"

# Create installation directory if it doesn't exist
If (-Not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Force -Path $installDir
}

# Download WiX Toolset
Write-Host "Downloading WiX Toolset v$wixVersion..."
# Check if curl is available for downloading, otherwise use Invoke-WebRequest
if (Get-Command curl -ErrorAction SilentlyContinue) {
    curl.exe -L $wixZipUrl -o $zipFileName
} else {
    Invoke-WebRequest -Uri $wixZipUrl -OutFile $zipFileName
}

# Extract WiX Toolset
Write-Host "Extracting WiX Toolset to $installDir..."
Expand-Archive -Path $zipFileName -DestinationPath $installDir -Force

# Add WiX Toolset to PATH (for the current session and user profile)
$currentUserPath = [System.Environment]::GetEnvironmentVariable("PATH", "User")
if ($currentUserPath -notlike "*$installDir*") {
    [System.Environment]::SetEnvironmentVariable("PATH", "$installDir;$currentUserPath", "User")
    $env:PATH = "$installDir;" + $env:PATH # Update for current session
    Write-Host "WiX Toolset added to User PATH and current session PATH."
} else {
    # Ensure it's in current session PATH even if already in User PATH
    if ($env:PATH -notlike "*$installDir*") {
        $env:PATH = "$installDir;" + $env:PATH
    }
    Write-Host "WiX Toolset already in User PATH. Ensured it is in current session PATH."
}

# Clean up downloaded zip file
Remove-Item $zipFileName -Force

Write-Host "WiX Toolset v$wixVersion installation attempt complete."
Write-Host "Install directory: $installDir"
Write-Host "Current session PATH: $env:PATH"

# Verify installation by checking heat.exe version (part of WiX Toolset)
$heatExePath = Join-Path $installDir "heat.exe"
if (Test-Path $heatExePath) {
    Write-Host "Verifying WiX installation by checking heat.exe..."
    & $heatExePath -? | Select-Object -First 5
} else {
    Write-Warning "heat.exe not found at $heatExePath. WiX Toolset might not be installed correctly or is not in the expected PATH."
    Write-Host "Listing contents of install directory:"
    Get-ChildItem -Path $installDir
}
