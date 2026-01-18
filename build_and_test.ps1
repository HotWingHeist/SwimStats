#!/usr/bin/env pwsh

Write-Host "Building SwimStats..."
cd "C:\Users\zhife\swimstats"

# Build
Write-Host "Running dotnet build..."
& dotnet build --configuration Debug 2>&1 | Out-String | Select-String -Pattern "Build succeeded|error"

# Check if build succeeded
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Build succeeded"
    
    # Run the app
    Write-Host "Starting application..."
    & dotnet run --project src/SwimStats.App/SwimStats.App.csproj &
    
    Write-Host "Application started in background"
} else {
    Write-Host "✗ Build failed"
}
