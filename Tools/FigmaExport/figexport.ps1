# Builds the Unity layout spec from the Figma file JSON:
# assigns every top-level layer of the two gameplay frames to a UI group, nests
# layers into containers (buttons, pills, panels) and lists the nodes to render.
param(
  [string]$JsonPath,
  [string]$OutSpec,
  [string]$OutRenderList
)
Add-Type -AssemblyName System.Web.Extensions
$ser = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$ser.MaxJsonLength = [int]::MaxValue
$ser.RecursionLimit = 1000
$doc = $ser.DeserializeObject([System.IO.File]::ReadAllText($JsonPath, [System.Text.Encoding]::UTF8))

function Find-Node($n, $id) {
  if ($n["id"] -eq $id) { return $n }
  if ($n.ContainsKey("children")) { foreach ($c in $n["children"]) { $r = Find-Node $c $id; if ($r) { return $r } } }
  return $null
}
function Hex($c) {
  if (-not $c) { return "" }
  "#{0:X2}{1:X2}{2:X2}" -f [int][math]::Round([double]$c["r"] * 255), [int][math]::Round([double]$c["g"] * 255), [int][math]::Round([double]$c["b"] * 255)
}
function Rect($b, $ox, $oy) {
  if (-not $b) { return $null }
  @{ x = [math]::Round([double]$b["x"] - $ox, 2); y = [math]::Round([double]$b["y"] - $oy, 2); w = [math]::Round([double]$b["width"], 2); h = [math]::Round([double]$b["height"], 2) }
}
function HasPaint($n) {
  foreach ($k in @("fills", "strokes", "effects")) {
    if ($n[$k]) { foreach ($p in $n[$k]) { if ($p["visible"] -ne $false) { return $true } } }
  }
  return ($n.ContainsKey("children") -and $n["children"].Count -gt 0)
}

$frames = @(
  @{ id = "1:29"; state = "Combat" },
  @{ id = "1:496"; state = "BuyGear" }
)
# Groups expanded one level so each lily pad becomes its own image.
$expand = @("1:36", "1:500")
# Layers that own the layers drawn on top of them (buttons, pills, panels, slots).
$containerIds = @(
  "1:32", "1:33", "1:83", "1:159", "1:135", "1:179", "1:144", "1:84", "1:85",
  "1:103", "1:104", "1:105", "1:106", "1:107", "1:108", "1:157", "1:158",
  "1:112", "1:113", "1:114", "1:115", "1:86", "1:87", "1:88", "1:89",
  "1:146", "1:147", "1:148", "1:149", "1:150", "1:175", "1:31",
  "1:498", "1:524", "1:619", "1:540", "1:541", "1:551", "1:552", "1:553", "1:554", "1:555", "1:556",
  "1:499", "1:569", "1:570", "1:571", "1:572", "1:573", "1:580", "1:581", "1:582", "1:592", "1:593"
)
$buttonIds = @("1:195", "1:187", "1:179", "1:112", "1:113", "1:114", "1:115", "1:619", "1:570", "1:571", "1:572", "1:573", "1:592", "1:593")
$buttonNames = @{
  "1:195" = "Btn_Settings"; "1:187" = "Btn_Close"; "1:179" = "Btn_Speed";
  "1:112" = "Btn_Booster_1"; "1:113" = "Btn_Booster_2"; "1:114" = "Btn_Booster_3"; "1:115" = "Btn_Booster_4";
  "1:619" = "Btn_WatchAd"; "1:570" = "Btn_BuyGear_1"; "1:571" = "Btn_BuyGear_2"; "1:572" = "Btn_BuyGear_3"; "1:573" = "Btn_BuyGear_4";
  "1:592" = "Btn_Start"; "1:593" = "Btn_Refresh"
}
# Layers that stretch with the screen width (9-slice, left/right borders in Figma pt).
$stretchX = @{ "1:499" = 24 }

function Group-For($state, $id, $name, $r) {
  $top = $r.y; $bottom = $r.y + $r.h; $left = $r.x; $cx = $r.x + $r.w / 2
  if ($state -eq "Combat") {
    if ($id -eq "1:30") { return "Background" }
    if ($id -eq "1:35") { return "Banner" }
    if ($name -like "slider gameplay*" -and $top -ge 120) { return "Battlefield" }  # enemy HP bars
    if ($bottom -le 145) {
      if ($cx -lt 100) { return "HUD_TopLeft" }
      if ($cx -gt 355) { return "HUD_TopRight" }
      return "HUD_TopCenter"
    }
    if ($left -ge 355 -and $top -ge 300 -and $top -le 420) { return "SideResources" }
    if ($left -ge 330 -and $top -ge 480) { return "Boosters" }
    if ($top -ge 460) { return "Board" }
    return "Battlefield"
  } else {
    if ($id -eq "1:497" -or $id -eq "1:525" -or $bottom -le 145) { return $null }  # shared with Combat
    if ($top -ge 140 -and $bottom -le 210) { return "WatchAd" }
    if ($top -ge 577) { return "ShopBar" }
    return "Board"
  }
}

$nodes = New-Object System.Collections.ArrayList
$z = 0
foreach ($f in $frames) {
  $frame = Find-Node $doc["document"] $f.id
  $ox = [double]$frame["absoluteBoundingBox"]["x"]; $oy = [double]$frame["absoluteBoundingBox"]["y"]
  $flat = New-Object System.Collections.ArrayList
  foreach ($c in $frame["children"]) {
    if ($expand -contains $c["id"]) { foreach ($cc in $c["children"]) { [void]$flat.Add($cc) } } else { [void]$flat.Add($c) }
  }
  foreach ($n in $flat) {
    if ($n["visible"] -eq $false) { continue }
    if (-not (HasPaint $n)) { continue }
    $id = $n["id"]
    $bb = Rect $n["absoluteBoundingBox"] $ox $oy
    $rb = Rect $n["absoluteRenderBounds"] $ox $oy
    if (-not $rb) { continue }
    $group = [string](Group-For $f.state $id $n["name"] $bb)
    if (-not $group) { continue }
    $z++
    $o = [ordered]@{
      id = $id; name = $n["name"]; state = $f.state; group = $group; z = $z; parent = ""
      kind = "image"; sprite = ""; button = ($buttonIds -contains $id); uiName = ""
      x = $rb.x; y = $rb.y; w = $rb.w; h = $rb.h
      bx = $bb.x; by = $bb.y; bw = $bb.w; bh = $bb.h
      stretchX = $stretchX.ContainsKey($id); sliceX = 0
      text = ""; fontSize = 0; color = ""; strokeColor = ""; strokeWidth = 0; alignH = ""; alignV = ""
    }
    if ($buttonNames.ContainsKey($id)) { $o.uiName = $buttonNames[$id] }
    if ($o.stretchX) { $o.sliceX = $stretchX[$id] }
    if ($n["type"] -eq "TEXT") {
      $st = $n["style"]
      $o.kind = "text"; $o.x = $bb.x; $o.y = $bb.y; $o.w = $bb.w; $o.h = $bb.h
      $o.text = $n["characters"]; $o.fontSize = [double]$st["fontSize"]
      $o.color = [string](Hex $n["fills"][0]["color"])
      if ($n["strokes"] -and $n["strokes"].Count -gt 0) { $o.strokeColor = [string](Hex $n["strokes"][0]["color"]); $o.strokeWidth = [math]::Round([double]$n["strokeWeight"], 3) }
      $o.alignH = $st["textAlignHorizontal"]; $o.alignV = $st["textAlignVertical"]
    }
    [void]$nodes.Add($o)
  }
}

# Nesting: a layer goes under the smallest earlier container in its group that holds its
# centre, unless that would change what draws on top of it.
function Overlaps($a, $b) { ($a.bx -lt $b.bx + $b.bw) -and ($b.bx -lt $a.bx + $a.bw) -and ($a.by -lt $b.by + $b.bh) -and ($b.by -lt $a.by + $a.bh) }
function IsDescendant($n, $ancestorId, $byId) {
  $p = $n.parent
  while ($p) { if ($p -eq $ancestorId) { return $true }; $p = $byId[$p].parent }
  return $false
}
$byId = @{}
foreach ($n in $nodes) { $byId[$n.id] = $n }
for ($i = 0; $i -lt $nodes.Count; $i++) {
  $c = $nodes[$i]
  $ccx = $c.bx + $c.bw / 2; $ccy = $c.by + $c.bh / 2
  $best = $null; $bestArea = [double]::MaxValue
  for ($j = 0; $j -lt $i; $j++) {
    $a = $nodes[$j]
    if ($a.group -ne $c.group -or $a.state -ne $c.state) { continue }
    $isContainer = ($containerIds -contains $a.id) -or ($a.kind -eq "image" -and $a.name -match '^Gear \d+$')
    if (-not $isContainer) { continue }
    if ($ccx -lt $a.bx -or $ccx -gt $a.bx + $a.bw -or $ccy -lt $a.by -or $ccy -gt $a.by + $a.bh) { continue }
    $area = $a.bw * $a.bh
    if ($area -le $c.bw * $c.bh -or $area -ge $bestArea) { continue }
    $safe = $true
    for ($k = $j + 1; $k -lt $i; $k++) {
      $d = $nodes[$k]
      if ($d.group -ne $c.group -or $d.state -ne $c.state) { continue }
      if ((IsDescendant $d $a.id $byId)) { continue }
      if (Overlaps $d $c) { $safe = $false; break }
    }
    if ($safe) { $best = $a; $bestArea = $area }
  }
  if ($best) { $c.parent = $best.id }
}

$imageIds = @($nodes | Where-Object { $_.kind -eq "image" } | ForEach-Object { $_.id })
[System.IO.File]::WriteAllText($OutRenderList, ($imageIds -join "`n"))
$json = '{"fileKey":"VDMqqWO6kpjDnejJEiA4BH","frameWidth":402,"frameHeight":874,"exportScale":3,"nodes":' + $ser.Serialize($nodes) + '}'
[System.IO.File]::WriteAllText($OutSpec, $json, (New-Object System.Text.UTF8Encoding($false)))

# Readable tree for checking the result.
function Print($parentId, $depth, $state, $group) {
  foreach ($n in $nodes) {
    if ($n.parent -ne $parentId -or $n.group -ne $group -or $n.state -ne $state) { continue }
    $label = if ($n.kind -eq "text") { "TEXT '" + $n.text + "'" } else { $n.name }
    if ($n.uiName) { $label = $n.uiName + " <" + $label + ">" }
    ("  " * $depth) + "$label  ($($n.id)) [$($n.bx),$($n.by) $($n.bw)x$($n.bh)]"
    Print $n.id ($depth + 1) $state $group
  }
}
$groups = $nodes | ForEach-Object { $_.state + "|" + $_.group } | Select-Object -Unique
foreach ($g in $groups) {
  $s, $gn = $g.Split("|")
  "== $s / $gn"
  Print "" 1 $s $gn
}
"images: $($imageIds.Count), nodes: $($nodes.Count)"
