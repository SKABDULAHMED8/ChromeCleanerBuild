$ErrorActionPreference = "Stop"

dotnet publish .\ChromeCacheCleaner.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:EnableCompressionInSingleFile=true `
  -o .\publish

Write-Host ""
Write-Host "Build succeeded." -ForegroundColor Green
Write-Host "EXE: $PWD\publish\ChromeCacheCleaner.exe" -ForegroundColor Cyan
