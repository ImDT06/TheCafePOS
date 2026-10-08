param([string]$SourcePath = 'artifacts/menu-source/menu.json')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root $SourcePath) -Raw -Encoding UTF8 | ConvertFrom-Json
$localImages = @{
 'Americano Classic'='americano-classic.png'; 'Americano Nóng'='americano-nong.png'
 'Cappuccino Đá'='cappuccino-da.png'; 'Cappuccino Nóng'='cappuccino-nong.png'
 'Caramel Macchiato Đá'='caramel-macchiato-da.png'; 'Caramel Macchiato Nóng'='caramel-macchiato-nong.png'
 'Espresso Đá'='espresso-da.png'; 'Espresso Nóng'='espresso-nong.png'
 'Trà Đào Cam Sả - Đá'='tra-dao-cam-sa-da.png'; 'Trà Đào Cam Sả - Nóng'='tra-dao-cam-sa-nong.png'
 'Đào Miếng'='dao-mieng.png'; 'Trân châu trắng'='tran-chau-trang.png'
}
$categories = @(); $products = @(); $downloads = @(); $seen = @{}; $toppings = @{}
$all = @($source.menu.products | Sort-Object id -Unique)
foreach ($p in $all) {
 foreach ($group in @($p.options | Where-Object type -eq 2)) {
  foreach ($item in $group.items) {
   if ($item.price -gt 0 -and -not $toppings.ContainsKey([string]$item.code)) {
    $id = 'tch-topping-' + $item.code
    $photo = if ($localImages.ContainsKey($item.name.Trim())) { 'Images/' + $localImages[$item.name.Trim()] } else { '' }
    $toppings[[string]$item.code] = [ordered]@{ Id=$id; Name=$item.name.Trim(); CategoryId='tch-toppings'; BasePrice=[decimal]$item.price; IsTopping=$true; SoldSeparately=$false; AllowSugar=$false; AllowIce=$false; Sizes=@(); ImageUrl=$photo }
   }
  }
 }
}
foreach ($cat in $source.menu) {
 if ($cat.id -eq 92) { continue } # Featured items are duplicates; retain their real categories.
 $catId = 'tch-cat-' + $cat.id
 $categories += [ordered]@{Id=$catId;Name=$cat.name;DisplayOrder=$categories.Count;IsActive=$true}
 $retail = $cat.id -in @(102,101,7373,7676)
 foreach ($p in $cat.products) {
  if ($seen.ContainsKey($p.id)) { continue }; $seen[$p.id]=$true
  $sizeGroup = $p.options | Where-Object name -eq 'Size' | Select-Object -First 1
  $basePrice = [decimal](($sizeGroup.items | Measure-Object price -Minimum).Minimum)
  $sizeNames = @{ 'Nhỏ'='S';'Vừa'='M';'Lớn'='L' }
  $sizes = @()
  if (-not $retail) {
   foreach ($item in $sizeGroup.items) {
    $size = $sizeNames[$item.name]; if (-not $size) { throw ('Unknown size: ' + $item.name) }
    $sizes += [ordered]@{Name=$size;ExtraPrice=([decimal]$item.price-$basePrice);PackagingId=$(if($size -eq 'L'){'pack-2'}else{'pack-1'})}
   }
   $sizes = @($sizes | Sort-Object @{e={@('S','M','L').IndexOf($_.Name)}})
  }
  $defaultSize = $sizeNames[$sizeGroup.items[[int]$sizeGroup.default_index].name]
  $photo = 'Images/' + $p.slug + '.png'
  if ($localImages.ContainsKey($p.name)) { $photo = 'Images/' + $localImages[$p.name] }
  else { $downloads += [ordered]@{Name=$p.name;Url=$p.thumbnail;Path=$photo} }
  $allowed = @($p.options | Where-Object type -eq 2 | ForEach-Object { $_.items } | Where-Object {$toppings.ContainsKey([string]$_.code)} | ForEach-Object {'tch-topping-' + $_.code} | Sort-Object -Unique)
  $hot = $p.name -match 'Nóng'
  $products += [ordered]@{
   Id=('tch-' + $p.id);CategoryId=$catId;Name=$p.name;BasePrice=$(if($retail){[decimal]$p.price}else{$basePrice});
   DefaultSize=$defaultSize;Sizes=$sizes;ImageUrl=$photo;IsRetailItem=$retail;IsTopping=$false;SoldSeparately=$true;
   AllowSugar=((-not $retail) -and @($p.options | Where-Object {$_.name.Trim() -eq 'Độ ngọt'}).Count -gt 0);
   AllowIce=((-not $retail) -and (-not $hot) -and @($p.options | Where-Object {$_.name.Trim() -eq 'Lượng đá'}).Count -gt 0);
   DefaultIce=$(if($hot -or $retail){0}else{100});DefaultSugar=100;AllowedToppingIds=$allowed;IsActive=$true
  }
 }
}
$categories += [ordered]@{Id='tch-toppings';Name='Topping';DisplayOrder=$categories.Count;IsActive=$true}
$products += @($toppings.Values | Sort-Object Name)
$catalog = [ordered]@{Source='https://order.thecoffeehouse.com/order';ApiSource='https://api.thecoffeehouse.com/api/v5/menu';CheckedDate='2026-10-08';Categories=$categories;Products=$products}
$destination = Join-Path $root 'TheCafePOS_WPF/TheCafePOS_WPF/Assets/Menu'
New-Item -ItemType Directory -Force $destination | Out-Null
$catalog | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 (Join-Path $destination 'thecoffeehouse.json')
$downloads | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 (Join-Path $root 'artifacts/menu-source/image-downloads.json')
Write-Output ("Imported {0} categories; {1} sale products; {2} toppings; {3} remote images" -f $categories.Count,$seen.Count,$toppings.Count,$downloads.Count)
& (Join-Path $PSScriptRoot 'import-coffeehouse-options.ps1') -SourcePath $SourcePath
