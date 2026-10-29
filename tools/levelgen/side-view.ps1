# Draws a side view of a level: the middle (physics) layer from the collision bitmaps of its blocks,
# dynamic objects as circles. Useful to check that a generated road is continuous.
# The bitmaps come from the block catalog (docs/catalog/bl/cols). Windows PowerShell (System.Drawing).
#
#   powershell -File tools/levelgen/side-view.ps1 -Level content/Levels/summit.json -Out summit.png

param(
    [Parameter(Mandatory = $true)][string]$Level,
    [Parameter(Mandatory = $true)][string]$Out,
    [float]$Scale = 2.0,   # pixels per physics unit
    [int]$Rows = 2         # the level is split into this many rows
)
Add-Type -AssemblyName System.Drawing

$bitmaps = Join-Path $PSScriptRoot "..\..\docs\catalog\bl\cols"
# Blocks that share the collision bitmap of another block
$sameAs = @{ lo = "a1"; xa1 = "a1"; st2 = "st" }
$pixel = 0.2524 * 0.25   # physics units per bitmap pixel

$data = Get-Content $Level -Raw | ConvertFrom-Json
$blocks = @($data.middle | ForEach-Object { [pscustomobject]@{ Name = $_.block; X = $_.x * 22.5; Y = $_.y * 22.5 + $_.shift * 2.5 } })
$minX = ($blocks | Measure-Object X -Minimum).Minimum - 20
$maxX = ($blocks | Measure-Object X -Maximum).Maximum + 20
$minY = ($blocks | Measure-Object Y -Minimum).Minimum - 30
$maxY = ($blocks | Measure-Object Y -Maximum).Maximum + 30

$rowWidth = ($maxX - $minX) / $Rows
$width = [int]($rowWidth * $Scale) + 20
$height = [int](($maxY - $minY) * $Scale) + 20
$image = New-Object System.Drawing.Bitmap $width, ($height * $Rows)
$g = [System.Drawing.Graphics]::FromImage($image)
$g.Clear([System.Drawing.Color]::White)
$font = New-Object System.Drawing.Font "Arial", 8
$cache = @{}

for ($row = 0; $row -lt $Rows; $row++) {
    $left = $minX + $row * $rowWidth
    $top = $row * $height
    $g.SetClip((New-Object System.Drawing.Rectangle 0, $top, $width, $height))
    $g.DrawLine([System.Drawing.Pens]::Gray, 0, $top + $height - 1, $width, $top + $height - 1)

    # Grid columns, numbered every 5
    for ($column = [math]::Ceiling($left / 22.5); $column * 22.5 -lt $left + $rowWidth + 22.5; $column++) {
        if ($column % 5 -ne 0) { continue }
        $x = 10 + ($column * 22.5 - $left) * $Scale
        $g.DrawLine([System.Drawing.Pens]::LightGray, $x, $top, $x, $top + $height)
        $g.DrawString("$column", $font, [System.Drawing.Brushes]::Gray, $x + 2, $top + 2)
    }

    foreach ($block in $blocks) {
        if ($block.X -lt $left - 30 -or $block.X -gt $left + $rowWidth + 30) { continue }
        $name = if ($sameAs.ContainsKey($block.Name)) { $sameAs[$block.Name] } else { $block.Name }
        if (-not $cache.ContainsKey($name)) { $cache[$name] = [System.Drawing.Image]::FromFile((Join-Path $bitmaps "$name.png")) }
        $bitmap = $cache[$name]
        $w = $bitmap.Width * $pixel * $Scale
        $h = $bitmap.Height * $pixel * $Scale
        $g.DrawImage($bitmap, [float](10 + ($block.X - $left) * $Scale - $w / 2), [float]($top + 10 + ($block.Y - $minY) * $Scale - $h / 2), [float]$w, [float]$h)
    }

    foreach ($object in $data.objects) {
        $x = 10 + ($object.x - $left) * $Scale
        $y = $top + 10 + ($object.y - $minY) * $Scale
        $r = 3 * $Scale
        $g.DrawEllipse([System.Drawing.Pens]::Blue, [float]($x - $r), [float]($y - $r), [float](2 * $r), [float](2 * $r))
    }
}

$g.ResetClip()
$image.Save($Out)
$g.Dispose()
$image.Dispose()
$cache.Values | ForEach-Object { $_.Dispose() }
"Saved $Out"
