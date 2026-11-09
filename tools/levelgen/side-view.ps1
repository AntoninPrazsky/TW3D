# Draws a side view of a level: the middle (physics) layer from the collision shapes of its blocks
# (content/collision.json, made by tools/CollisionGen), dynamic objects as circles. Useful to check that a
# generated road is continuous. A turned block is drawn with its shape mirrored left to right.
# Windows PowerShell (System.Drawing).
#
#   powershell -File tools/levelgen/side-view.ps1 -Level content/Levels/summit.json -Out summit.png

param(
    [Parameter(Mandatory = $true)][string]$Level,
    [Parameter(Mandatory = $true)][string]$Out,
    [float]$Scale = 2.0,   # pixels per physics unit
    [int]$Rows = 2         # the level is split into this many rows
)
Add-Type -AssemblyName System.Drawing

$shapes = (Get-Content (Join-Path $PSScriptRoot "..\..\content\collision.json") -Raw | ConvertFrom-Json).models
$unit = 2.5   # physics units per model unit; a model has +x at the left edge of a block placed unturned and y up
$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(90, 110, 160))

$data = Get-Content $Level -Raw | ConvertFrom-Json
$blocks = @($data.middle | ForEach-Object { [pscustomobject]@{ Name = $_.block; Turned = [bool]$_.turned; X = $_.x * 22.5; Y = $_.y * 22.5 + $_.shift * 2.5 } })
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
        $model = $shapes."Levels/mod/$($block.Name)"
        if ($null -eq $model) { continue }
        # A block whose body and wheels collide with different parts (the gates, the vaults): the body's
        $polygons = @(if ($model.shape) { $model.shape } else { $model.body })   # @(): one polygon stays a list of one
        $mirror = if ($block.Turned) { 1 } else { -1 }
        foreach ($polygon in $polygons) {
            $points = for ($i = 0; $i -lt $polygon.Count; $i += 2) {
                New-Object System.Drawing.PointF ([float](10 + ($block.X + $mirror * $polygon[$i] * $unit - $left) * $Scale)),
                    ([float]($top + 10 + ($block.Y - $polygon[$i + 1] * $unit - $minY) * $Scale))
            }
            $g.FillPolygon($brush, [System.Drawing.PointF[]]$points)
        }
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
$brush.Dispose()
"Saved $Out"
