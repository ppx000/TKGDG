# Re-exports the Flower Escape gameplay UI from Figma into the Unity project:
#   1. downloads the file JSON,
#   2. builds the layout spec (figexport.ps1),
#   3. renders every image layer at 3x through the Figma images API,
#   4. dedupes the renders into named sprites (figsprites.ps1),
#   5. copies sprites + layout into Assets/FlowerEscape.
# Then run Tools > Flower Escape > Build Gameplay Scene in Unity.
#
# Usage (PowerShell):
#   $env:FIGMA_TOKEN = "figd_..."      # Figma > Settings > Security > Personal access tokens (File content: read)
#   .\Tools\FigmaExport\Export-FigmaLayout.ps1
param(
  [string]$FileKey = "VDMqqWO6kpjDnejJEiA4BH",
  [string]$Token = $env:FIGMA_TOKEN,
  [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot\..\.."),
  [string]$WorkDir = (Join-Path ([System.IO.Path]::GetTempPath()) "flower-escape-figma")
)
$ErrorActionPreference = "Stop"
if (-not $Token) { throw "Set FIGMA_TOKEN or pass -Token." }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$headers = @{ "X-Figma-Token" = $Token }

New-Item -ItemType Directory -Force $WorkDir, "$WorkDir\raw" | Out-Null
Remove-Item "$WorkDir\raw\*", "$WorkDir\sprites" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Downloading file $FileKey..."
Invoke-WebRequest -UseBasicParsing -Uri "https://api.figma.com/v1/files/$FileKey" -Headers $headers -OutFile "$WorkDir\file.json"

Write-Host "Building layout spec..."
& "$PSScriptRoot\figexport.ps1" -JsonPath "$WorkDir\file.json" -OutSpec "$WorkDir\layout.json" -OutRenderList "$WorkDir\render_ids.txt" | Select-Object -Last 1

Write-Host "Rendering images..."
$ids = Get-Content "$WorkDir\render_ids.txt" | Where-Object { $_ }
$wc = New-Object System.Net.WebClient
for ($i = 0; $i -lt $ids.Count; $i += 50) {
  $batch = $ids[$i..([Math]::Min($i + 49, $ids.Count - 1))]
  $q = [Uri]::EscapeDataString(($batch -join ","))
  $resp = Invoke-RestMethod -Uri "https://api.figma.com/v1/images/$FileKey`?ids=$q&scale=3&format=png" -Headers $headers -TimeoutSec 300
  if ($resp.err) { throw "Figma render error: $($resp.err)" }
  foreach ($p in $resp.images.PSObject.Properties) {
    if (-not $p.Value) { throw "Figma returned no image for $($p.Name)" }
    $wc.DownloadFile($p.Value, (Join-Path "$WorkDir\raw" (($p.Name -replace '[:;]', '_') + ".png")))
  }
}

Write-Host "Writing sprites..."
& "$PSScriptRoot\figsprites.ps1" -SpecPath "$WorkDir\layout.json" -RawDir "$WorkDir\raw" -SpriteDir "$WorkDir\sprites" -OutSpec "$WorkDir\layout_final.json" | Select-Object -Last 1

$art = Join-Path $ProjectRoot "Assets\FlowerEscape\Art\Figma"
New-Item -ItemType Directory -Force $art, (Join-Path $ProjectRoot "Assets\FlowerEscape\Layout") | Out-Null
Copy-Item "$WorkDir\sprites\*.png" $art -Force
Copy-Item "$WorkDir\layout_final.json" (Join-Path $ProjectRoot "Assets\FlowerEscape\Layout\gameplay_layout.json") -Force
Write-Host "Done. In Unity: Tools > Flower Escape > Build Gameplay Scene"
