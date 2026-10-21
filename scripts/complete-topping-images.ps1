param([string]$CheckedDate = '2026-10-12')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$menuDirectory = Join-Path $root 'TheCafePOS_WPF/TheCafePOS_WPF/Assets/Menu'
$sourceDirectory = Join-Path $root 'artifacts/menu-source'
$catalogPath = Join-Path $menuDirectory 'thecoffeehouse.json'
$catalog = Get-Content $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
$headers = @{src='TCH-WEB';'X-Client-Info'='TCH-WEB';'TCH-APP-VERSION'='5.10.29';'TCH-DEVICE-ID'=[guid]::NewGuid().ToString()}
Add-Type -AssemblyName System.Drawing
$report = @()
foreach ($product in $catalog.Products | Where-Object { $_.IsTopping }) {
 $code = $product.Id.Replace('tch-topping-','')
 $body = @{variants_code=$code;type='DELI';lat=21.0285;long=105.8542} | ConvertTo-Json
 $sourcePath = Join-Path $sourceDirectory ('topping-detail-'+$code+'.json')
 Invoke-WebRequest -Uri 'https://api.thecoffeehouse.com/api/v5/menu/product-detail' -Method Post -ContentType 'application/json' -Headers $headers -Body $body -UseBasicParsing -OutFile $sourcePath
 $detail = Get-Content $sourcePath -Raw -Encoding UTF8 | ConvertFrom-Json
 if ($detail.error -or -not $detail.product -or $detail.product.name.Trim() -ne $product.Name.Trim()) { throw "Topping detail does not match: $($product.Name)" }
 $photo = $detail.product.thumbnail
 if (-not $photo) { $photo = $detail.product.images | Where-Object { $_ } | Select-Object -First 1 }
 $status = 'Existing official image retained'
 if (-not $product.ImageUrl -and $photo) {
  if ($detail.product.slug -notmatch '^[a-z0-9-]+$') { throw 'Invalid image filename' }
  $product.ImageUrl = 'Images/'+$detail.product.slug+'.png'
  $temporary = Join-Path $sourceDirectory ('topping-'+$code+'.download')
  Invoke-WebRequest -Uri $photo -UseBasicParsing -OutFile $temporary
  $image = [Drawing.Image]::FromFile($temporary)
  try { $image.Save((Join-Path $root $product.ImageUrl),[Drawing.Imaging.ImageFormat]::Png) } finally { $image.Dispose() }
  $status = 'Downloaded official detail image'
  Write-Output ($status + ': ' + $product.Name)
 }
 if (-not $product.ImageUrl) { $status = 'Official detail has no thumbnail or images' }
 $report += [pscustomobject]@{Id=$product.Id;Name=$product.Name;Image=$product.ImageUrl;DetailProductId=$detail.product.id;VariantCode=$code;Source=$photo;Status=$status;CheckedDate=$CheckedDate}
}
$catalog | ConvertTo-Json -Depth 15 | Set-Content $catalogPath -Encoding UTF8
$report | Export-Csv (Join-Path $menuDirectory 'topping-image-sources.csv') -NoTypeInformation -Encoding UTF8
$report | Where-Object { -not $_.Image } | Select-Object Id,Name,@{n='Reason';e={$_.Status}} | Export-Csv (Join-Path $menuDirectory 'missing-images.csv') -NoTypeInformation -Encoding UTF8
$referencePath = Join-Path $menuDirectory 'menu-reference.csv'
$reference = Import-Csv $referencePath -Encoding UTF8
foreach ($row in $reference) { $row.Image = ($catalog.Products | Where-Object Name -eq $row.Name | Select-Object -First 1).ImageUrl }
$reference | Export-Csv $referencePath -NoTypeInformation -Encoding UTF8
$report | Group-Object Status | Select-Object Count,Name | Format-Table
