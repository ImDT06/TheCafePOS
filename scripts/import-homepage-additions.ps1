param([string]$CheckedDate = '2026-10-12')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$directory = Join-Path $root 'artifacts/menu-source'
$headers = @{src='TCH-WEB';'X-Client-Info'='TCH-WEB';'TCH-APP-VERSION'='5.10.29';'TCH-DEVICE-ID'=[guid]::NewGuid().ToString()}
$location = @{lat=21.0285;long=105.8542;type='DELI';src='TCH-WEB'}
Invoke-WebRequest -Uri 'https://api.thecoffeehouse.com/api/v5/menu' -Method Post -ContentType 'application/json' -Headers $headers -Body ($location|ConvertTo-Json) -UseBasicParsing -OutFile (Join-Path $directory 'homepage-menu-hanoi.json')
$menu = Get-Content (Join-Path $directory 'homepage-menu-hanoi.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$source = Get-Content (Join-Path $directory 'menu-normalized.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$ids = @('6a9055c544bff30012e26827','6a9055c544bff30012e26826','6a9055c544bff30012e26825','6a09df8b38563d00135b4914','6a09df8b38563d00135b4913')
$details = @{}
foreach ($id in $ids) {
 $listed = $menu.menu.products | Where-Object id -eq $id | Select-Object -First 1
 if (-not $listed) { throw "Homepage product missing: $id" }
 $body = @{id=$id;slug=$listed.slug;type='DELI';variants_code='';lat=$location.lat;long=$location.long}
 $response = Invoke-RestMethod -Uri 'https://api.thecoffeehouse.com/api/v5/menu/product-detail' -Method Post -ContentType 'application/json' -Headers $headers -Body ($body|ConvertTo-Json)
 if ($response.error -or -not $response.product) { throw "Product detail unavailable: $id" }
 $product = $response.product
 $details[$id] = $product
 $categoryId = if ($id -eq '6a9055c544bff30012e26825') { 93 } else { 7682 }
 $category = $source.menu | Where-Object id -eq $categoryId | Select-Object -First 1
 if (-not $category) {
  $category = [pscustomobject]@{id=7682;name=($menu.menu | Where-Object id -eq 7682).name;products=@()}
  $source.menu += $category
 }
 $category.products = @($category.products | Where-Object id -ne $id) + @($product)
}
$source | ConvertTo-Json -Depth 30 | Set-Content (Join-Path $directory 'menu-homepage-details.json') -Encoding UTF8
& (Join-Path $PSScriptRoot 'import-thecoffeehouse-menu.ps1') -SourcePath 'artifacts/menu-source/menu-homepage-details.json' -CheckedDate $CheckedDate
$catalogPath = Join-Path $root 'TheCafePOS_WPF/TheCafePOS_WPF/Assets/Menu/thecoffeehouse.json'
$catalog = Get-Content $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($product in $catalog.Products | Where-Object { $_.Id.Substring(4) -in $ids }) {
 $product | Add-Member -Force NoteProperty FeaturedCategoryIds @('tch-cat-92')
}
# Retail topping names have a '(Le)' suffix; their official photos also depict
# the same add-on toppings. Match exact names after removing that suffix.
foreach ($product in $catalog.Products | Where-Object { $_.IsTopping -and -not $_.ImageUrl }) {
 $photo = ($menu.menu | Where-Object id -eq 106).products | Where-Object { ($_.name -replace '\s*\([^)]*\)$','').Trim() -eq $product.Name.Trim() } | Select-Object -First 1
 if ($photo.thumbnail) { $product.ImageUrl = 'Images/' + ($photo.slug -replace '-le$','') + '.png'; $details[$product.Id] = $photo }
}
Add-Type -AssemblyName System.Drawing
$downloaded = 0
foreach ($product in $catalog.Products | Where-Object ImageUrl) {
 $path = Join-Path $root $product.ImageUrl
 if (Test-Path -LiteralPath $path) { continue }
 $remote = if ($product.IsTopping) { $details[$product.Id] } else { $details[$product.Id.Substring(4)] }
 if (-not $remote.thumbnail) { throw "Missing official image: $($product.Name)" }
 $temporary = Join-Path $directory 'image-download.tmp'
 Invoke-WebRequest -Uri $remote.thumbnail -UseBasicParsing -OutFile $temporary
 $bitmap = [Drawing.Image]::FromFile($temporary)
 try { $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png) } finally { $bitmap.Dispose() }
 $downloaded++
 Write-Output "Downloaded: $($product.Name)"
}
$catalog | ConvertTo-Json -Depth 15 | Set-Content $catalogPath -Encoding UTF8
$catalog.Products | Where-Object { -not $_.ImageUrl } | Select-Object Id,Name,@{n='Reason';e={'No matching official photo in the checked menu'}} | Export-Csv (Join-Path (Split-Path $catalogPath) 'missing-images.csv') -NoTypeInformation -Encoding UTF8
$referencePath = Join-Path (Split-Path $catalogPath) 'menu-reference.csv'
$reference = Import-Csv $referencePath -Encoding UTF8
foreach ($row in $reference) { $row.Image = ($catalog.Products | Where-Object Name -eq $row.Name | Select-Object -First 1).ImageUrl }
$reference | Export-Csv $referencePath -NoTypeInformation -Encoding UTF8
Write-Output "Verified 5 homepage drinks; downloaded $downloaded official images."
& (Join-Path $PSScriptRoot 'complete-topping-images.ps1') -CheckedDate $CheckedDate
