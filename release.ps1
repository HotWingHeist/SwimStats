param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0",
    [switch]$CreateTag,
    [switch]$Push,
    [switch]$CreateGithubRelease
)

Write-Host "Starting release: Version $Version (Configuration: $Configuration)"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifacts = Join-Path $root 'artifacts'
if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }
New-Item -ItemType Directory -Path $artifacts | Out-Null

# Restore, build and test
dotnet restore $root
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

dotnet build $root -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

dotnet test $root -c $Configuration --no-build
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed" }

# Pack/publish the WPF app into single-folder publish
$publishDir = Join-Path $artifacts "publish"
Publish-Project:
try {
    dotnet publish "src/SwimStats.App/SwimStats.App.csproj" -c $Configuration -r win-x64 --self-contained false -o $publishDir /p:Version=$Version
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
}
catch {
    throw $_
}

# Create ZIP artifact
$zipFile = Join-Path $artifacts "SwimStats-$Version.zip"
if (Test-Path $zipFile) { Remove-Item $zipFile -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zipFile)

Write-Host "Created artifact: $zipFile"

# Create Git tag if requested
if ($CreateTag) {
    $tagName = "v$Version"
    git tag -a $tagName -m "Release $tagName"
    if ($LASTEXITCODE -ne 0) { throw "git tag failed" }
    Write-Host "Created git tag: $tagName"
    if ($Push) {
        git push origin $tagName
        git push origin main
        Write-Host "Pushed tag and main branch to origin"
    }
}

# Optional: Create GitHub release using gh CLI
if ($CreateGithubRelease) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Warning "gh CLI not found; skipping GitHub release creation"
    }
    else {
        $tagName = "v$Version"
        gh release create $tagName $zipFile -t "Release $tagName" -n "Automated release: $tagName"
        Write-Host "Created GitHub release $tagName"
    }
}

Write-Host "Release script completed."