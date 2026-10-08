param([string]$SourcePath = 'artifacts/menu-source/menu.json')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root $SourcePath) -Raw -Encoding UTF8 | ConvertFrom-Json
$path = Join-Path $root 'TheCafePOS_WPF/TheCafePOS_WPF/Assets/Menu/thecoffeehouse.json'
$catalog = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
$reference = @()
foreach ($product in $catalog.Products) {
 $original = $source.menu.products | Where-Object { ('tch-' + $_.id) -eq $product.Id } | Select-Object -First 1
 foreach ($pair in @(@('Sugar','Độ ngọt'), @('Ice','Lượng đá'))) {
  $group = $original.options | Where-Object { $_.name -and $_.name.Trim() -eq $pair[1] } | Select-Object -First 1
  $choices = @(); $default = ''
  if (-not $product.IsRetailItem -and -not $product.IsTopping -and $null -ne $group) {
   $choices = @($group.items | ForEach-Object { $_.name.Trim() })
   if ($group.default_index -lt 0 -or $group.default_index -ge $choices.Count) { throw "Invalid default: $($product.Name)" }
   $default = $choices[$group.default_index]
  }
  $product | Add-Member -Force NoteProperty ($pair[0]+'Choices') $choices
  $product | Add-Member -Force NoteProperty ('Default'+$pair[0]+'Choice') $default
  $product.('Allow'+$pair[0]) = $choices.Count -gt 0
 }
 if ($product.IsTopping) { continue }
 if ($null -eq $original) { throw "Source missing: $($product.Name)" }
 $sizeGroup = $original.options | Where-Object name -eq 'Size' | Select-Object -First 1
 $names = @{ 'Nhỏ'='S'; 'Vừa'='M'; 'Lớn'='L' }
 if (-not $product.IsRetailItem) {
  if ($product.Sizes.Count -ne $sizeGroup.items.Count) { throw "Size count differs: $($product.Name)" }
  foreach ($size in $sizeGroup.items) {
   $stored = $product.Sizes | Where-Object Name -eq $names[$size.name]
   if ($null -eq $stored -or ($product.BasePrice + $stored.ExtraPrice) -ne $size.price) { throw "Size/price differs: $($product.Name)" }
  }
 }
 $reference += [pscustomobject]@{Name=$product.Name; Sizes=($product.Sizes.Name -join ' / '); DefaultSize=$product.DefaultSize; Sugar=($product.SugarChoices -join ' / '); DefaultSugar=$product.DefaultSugarChoice; Ice=($product.IceChoices -join ' / '); DefaultIce=$product.DefaultIceChoice; Source='https://order.thecoffeehouse.com/order'}
}
$catalog | ConvertTo-Json -Depth 15 | Set-Content $path -Encoding UTF8
$reference | Export-Csv (Join-Path (Split-Path $path) 'options-reference.csv') -NoTypeInformation -Encoding UTF8
Write-Output "Verified sizes/prices and imported named options for $($reference.Count) sale products."
