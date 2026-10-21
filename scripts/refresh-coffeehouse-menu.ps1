param([string]$CheckedDate = '2026-10-12')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$directory = Join-Path $root 'artifacts/menu-source'
New-Item -ItemType Directory -Force $directory | Out-Null
$currentPath = Join-Path $directory 'menu-current.json'
Invoke-WebRequest -Uri 'https://api.thecoffeehouse.com/api/v5/menu' -Method Post -ContentType 'application/json' -Headers @{src='TCH-WEB'} -Body '{"type":"DELI"}' -UseBasicParsing -OutFile $currentPath
$current = Get-Content $currentPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $current.menu -or @($current.menu.products).Count -lt 1) { throw 'Menu response is empty.' }
$previous = Get-Content (Join-Path $directory 'menu.json') -Raw -Encoding UTF8 | ConvertFrom-Json
# List responses contain size surcharges and omit sugar/ice groups. Preserve the
# last verified detail options; never interpret missing list fields as removals.
foreach ($product in $current.menu.products) {
 $size = $product.options | Where-Object name -eq 'Size' | Select-Object -First 1
 if (-not $size) { throw "Missing size data: $($product.name)" }
 foreach ($item in $size.items) { $item.price = [decimal]$product.price + [decimal]$item.price }
 $old = $previous.menu.products | Where-Object id -eq $product.id | Select-Object -First 1
 if ($old) { $product.options = @($size) + @($old.options | Where-Object name -ne 'Size') }
}
$current | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $directory 'menu-normalized.json') -Encoding UTF8
& (Join-Path $PSScriptRoot 'import-thecoffeehouse-menu.ps1') -SourcePath 'artifacts/menu-source/menu-normalized.json' -CheckedDate $CheckedDate
$catalog = Get-Content (Join-Path $root 'TheCafePOS_WPF/TheCafePOS_WPF/Assets/Menu/thecoffeehouse.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Add-Type -AssemblyName System.Drawing
$catalog.Products | Where-Object { -not $_.ImageUrl } | Select-Object Id,Name,@{n='Reason';e={'Official menu provides no standalone topping image'}} | Export-Csv (Join-Path $root 'TheCafePOS_WPF/TheCafePOS_WPF/Assets/Menu/missing-images.csv') -NoTypeInformation -Encoding UTF8
$downloaded = 0
foreach ($product in $catalog.Products | Where-Object { $_.ImageUrl }) {
 $path = Join-Path $root $product.ImageUrl
 if (Test-Path -LiteralPath $path) { continue }
 $remote = $current.menu.products | Where-Object { ('tch-' + $_.id) -eq $product.Id } | Select-Object -First 1
 if (-not $remote.thumbnail) { throw "No official image available: $($product.Name)" }
 $temporary = Join-Path $directory 'image-download.tmp'
 Invoke-WebRequest -Uri $remote.thumbnail -UseBasicParsing -OutFile $temporary
 $bitmap = [Drawing.Image]::FromFile($temporary)
 try { $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png) } finally { $bitmap.Dispose() }
 $downloaded++
}
Write-Output "Downloaded $downloaded missing official images. Detail options retained from previous verified snapshot."
& (Join-Path $PSScriptRoot 'import-homepage-additions.ps1') -CheckedDate $CheckedDate
