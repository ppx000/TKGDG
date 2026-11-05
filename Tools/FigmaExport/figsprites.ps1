# Dedupes the rendered PNGs into named sprites, fixes each image rect to the rendered
# size and writes the final layout spec the Unity builder reads.
param(
  [string]$SpecPath,
  [string]$RawDir,
  [string]$SpriteDir,
  [string]$OutSpec
)
Add-Type -AssemblyName System.Web.Extensions
Add-Type -AssemblyName System.Drawing
$ser = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$ser.MaxJsonLength = [int]::MaxValue
$spec = $ser.DeserializeObject([System.IO.File]::ReadAllText($SpecPath, [System.Text.Encoding]::UTF8))
$scale = [double]$spec["exportScale"]

$rename = @{
  "image 39" = "banner_ad_placeholder"; "environment background" = "environment_background"
  "Rectangle 70" = "panel_header"; "Rectangle 71" = "pill_currency"; "Rectangle 72" = "stat_bar"; "Rectangle 73" = "stat_bar"
  "Rectangle 74" = "pill_ammo"; "Rectangle 77" = "pill_ammo"; "Rectangle 78" = "pill_ammo"; "Rectangle 79" = "pill_ammo"
  "Rectangle 75" = "badge_level"; "Rectangle 76" = "badge_level"; "Rectangle 80" = "tag_difficulty"
  "Rectangle 82" = "tag_price"; "Rectangle 83" = "tag_price"; "Rectangle 84" = "tag_price"
  "Group 25" = "button_close"; "Setting" = "button_settings"; "Group 19" = "icon_boss_marker"; "Group 46" = "badge_info"
  "Polygon 1" = "marker_wave"; "Polygon 2" = "marker_wave"; "Mask group" = "gear_masked"
}
function SpriteName($name) {
  if ($rename.ContainsKey($name)) { return $rename[$name] }
  $s = ($name.ToLowerInvariant() -replace '[^a-z0-9]+', '_').Trim('_')
  if (-not $s) { $s = "sprite" }
  return $s
}

New-Item -ItemType Directory -Force $SpriteDir | Out-Null
$md5 = [System.Security.Cryptography.MD5]::Create()
$byHash = @{}
$usedNames = @{}
$adjusted = 0
foreach ($n in $spec["nodes"]) {
  if ($n["kind"] -ne "image") { continue }
  $file = Join-Path $RawDir (($n["id"] -replace '[:;]', '_') + ".png")
  $bytes = [System.IO.File]::ReadAllBytes($file)
  $hash = [BitConverter]::ToString($md5.ComputeHash($bytes))
  $img = [System.Drawing.Image]::FromFile($file)
  $pw = $img.Width / $scale; $ph = $img.Height / $scale
  $img.Dispose()

  # Render bounds from the API are clipped by the frame; the render itself is not.
  $w = [double]$n["w"]; $h = [double]$n["h"]
  if ([math]::Abs($pw - $w) -le 1 -and [math]::Abs($ph - $h) -le 1) {
    $cx = [double]$n["x"] + $w / 2; $cy = [double]$n["y"] + $h / 2
  } else {
    $cx = [double]$n["bx"] + [double]$n["bw"] / 2; $cy = [double]$n["by"] + [double]$n["bh"] / 2
    "resized {0} '{1}': rb {2}x{3} -> png {4}x{5}" -f $n["id"], $n["name"], $w, $h, [math]::Round($pw, 2), [math]::Round($ph, 2)
    $adjusted++
  }
  $n["w"] = [math]::Round($pw, 3); $n["h"] = [math]::Round($ph, 3)
  $n["x"] = [math]::Round($cx - $pw / 2, 3); $n["y"] = [math]::Round($cy - $ph / 2, 3)

  if (-not $byHash.ContainsKey($hash)) {
    $base = SpriteName $n["name"]
    $name = $base; $i = 2
    while ($usedNames.ContainsKey($name)) { $name = "${base}_$i"; $i++ }
    $usedNames[$name] = $true
    $byHash[$hash] = $name
    [System.IO.File]::WriteAllBytes((Join-Path $SpriteDir "$name.png"), $bytes)
  }
  $n["sprite"] = $byHash[$hash]
}
[System.IO.File]::WriteAllText($OutSpec, $ser.Serialize($spec), (New-Object System.Text.UTF8Encoding($false)))
"unique sprites: $($byHash.Count), adjusted rects: $adjusted"
