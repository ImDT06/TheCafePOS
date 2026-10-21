param([switch]$UseCachedDownloads)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$audit = Join-Path $root 'artifacts/menu-source/image-audit'
New-Item -ItemType Directory -Force $audit | Out-Null
$menu = Get-Content (Join-Path $root 'artifacts/menu-source/image-audit-menu.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$previous = Get-Content (Join-Path $root 'artifacts/menu-source/menu-current.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$catalog = Get-Content (Join-Path $root 'TheCafePOS_WPF/TheCafePOS_WPF/Assets/Menu/thecoffeehouse.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$items = @($catalog.Products)
$items += [pscustomobject]@{Id='tch-6a38a22bcaa1a6406835e994';Name='Combo Pizza & Nuoc';ImageUrl='Images/combo-pizza-nuoc.png'}
$items += [pscustomobject]@{Id='tch-6a38a4f1e0588a323f137ad5';Name='Combo Pasta & Nuoc';ImageUrl='Images/combo-pasta-nuoc.png'}
Add-Type -AssemblyName System.Drawing
function PixelHash([string]$path) {
 $image = [Drawing.Image]::FromFile($path)
 $normalized = New-Object Drawing.Bitmap $image.Width,$image.Height
 $graphics = [Drawing.Graphics]::FromImage($normalized)
 $stream = New-Object IO.MemoryStream
 $sha = [Security.Cryptography.SHA256]::Create()
 try { $graphics.DrawImageUnscaled($image,0,0); $normalized.Save($stream,[Drawing.Imaging.ImageFormat]::Bmp); return [BitConverter]::ToString($sha.ComputeHash($stream.ToArray())) }
 finally { $graphics.Dispose(); $normalized.Dispose(); $image.Dispose(); $stream.Dispose(); $sha.Dispose() }
}
$report = @()
foreach ($product in $items) {
 $remote = $menu.menu.products | Where-Object { ('tch-'+$_.id) -eq $product.Id } | Select-Object -First 1
 $sourceScope = 'homepage Hanoi'
 if (-not $remote) { $remote = $previous.menu.products | Where-Object { ('tch-'+$_.id) -eq $product.Id } | Select-Object -First 1; $sourceScope = 'default menu' }
 if ($product.IsTopping) {
  $remote = ($menu.menu | Where-Object id -eq 106).products | Where-Object { ($_.name -replace '\s*\([^)]*\)$','').Trim() -eq $product.Name.Trim() } | Select-Object -First 1
  $sourceScope = 'homepage retail topping matched by name'
  if (-not $remote) {
   $detailPath = Join-Path $root ('artifacts/menu-source/topping-detail-'+$product.Id.Replace('tch-topping-','')+'.json')
   if (Test-Path $detailPath) { $remote = (Get-Content $detailPath -Raw -Encoding UTF8 | ConvertFrom-Json).product; $sourceScope = 'official topping detail by variant code' }
  }
 }
 $status = 'No official match'
 if ($remote.thumbnail -and $product.ImageUrl) {
  $download = Join-Path $audit ($product.Id + '.download')
  if (-not $UseCachedDownloads -or -not (Test-Path $download)) { Invoke-WebRequest -Uri $remote.thumbnail -UseBasicParsing -OutFile $download }
  $path = Join-Path $root $product.ImageUrl
  if ((Test-Path $path) -and (PixelHash $path) -eq (PixelHash $download)) { $status = 'Verified identical' }
  else {
   if (Test-Path $path) { Copy-Item -LiteralPath $path -Destination (Join-Path $audit ($product.Id+'.before.png')); $status = 'Updated from official image' } else { $status = 'Downloaded missing image' }
   $image = [Drawing.Image]::FromFile($download)
   try { $image.Save($path,[Drawing.Imaging.ImageFormat]::Png) } finally { $image.Dispose() }
   Write-Output ($status + ': ' + $product.Name)
  }
 }
 $report += [pscustomobject]@{Id=$product.Id;Name=$product.Name;Image=$product.ImageUrl;Status=$status;Source=$remote.thumbnail;MenuScope=$sourceScope}
}
$report | Export-Csv (Join-Path $root 'TheCafePOS_WPF/TheCafePOS_WPF/Assets/Menu/image-audit.csv') -NoTypeInformation -Encoding UTF8
$report | Group-Object Status | Select-Object Count,Name | Format-Table
