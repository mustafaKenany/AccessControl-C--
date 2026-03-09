$bytes = [System.IO.File]::ReadAllBytes('D:\AccessControlPro\src\AccessControlPro.WPF\bin\Debug\net8.0-windows\CareaIfc.dll')
$text = [System.Text.Encoding]::ASCII.GetString($bytes)

# Find all exported function names between the export markers
# Look for sequences of printable chars that look like C function names
$matches = [regex]::Matches($text, '(?<=\x00)([a-zA-Z_][a-zA-Z0-9_]{3,50})(?=\x00)')
$seen = @{}
foreach ($m in $matches) {
    $name = $m.Value
    # Filter: skip common section names and C++ mangled names
    if ($name -match '^(text|data|rdata|reloc|rsrc|bss|idata|edata|CRT|__|___)') { continue }
    if ($name.Length -gt 40) { continue }
    if (-not $seen.ContainsKey($name)) {
        $seen[$name] = $true
    }
}

Write-Output "=== Likely exported function names from CareaIfc.dll ==="
$seen.Keys | Sort-Object | ForEach-Object { Write-Output "  $_" }
