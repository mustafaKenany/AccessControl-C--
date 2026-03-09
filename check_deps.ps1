function Get-PEImports {
    param([string]$Path)
    $imports = @()
    try {
        $bytes = [System.IO.File]::ReadAllBytes($Path)
        $peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
        $optionalHeaderOffset = $peOffset + 24
        $magic = [BitConverter]::ToUInt16($bytes, $optionalHeaderOffset)
        if ($magic -eq 0x10b) {
            $importRVA = [BitConverter]::ToUInt32($bytes, $optionalHeaderOffset + 104)
            if ($importRVA -eq 0) { return $imports }
            $numSections = [BitConverter]::ToUInt16($bytes, $peOffset + 6)
            $sectionHeaderOffset = $optionalHeaderOffset + 224
            for ($i = 0; $i -lt $numSections; $i++) {
                $sOff = $sectionHeaderOffset + $i * 40
                $vAddr = [BitConverter]::ToUInt32($bytes, $sOff + 12)
                $vSize = [BitConverter]::ToUInt32($bytes, $sOff + 8)
                $rawOff = [BitConverter]::ToUInt32($bytes, $sOff + 20)
                if ($importRVA -ge $vAddr -and $importRVA -lt ($vAddr + $vSize)) {
                    $delta = $rawOff - $vAddr
                    $pos = $importRVA + $delta
                    while ($true) {
                        $nameRVA = [BitConverter]::ToUInt32($bytes, $pos + 12)
                        if ($nameRVA -eq 0) { break }
                        $nameOff = $nameRVA + $delta
                        $name = ''
                        while ($bytes[$nameOff] -ne 0) { $name += [char]$bytes[$nameOff]; $nameOff++ }
                        $imports += $name
                        $pos += 20
                    }
                    break
                }
            }
        }
    } catch {}
    return $imports
}

$systemDlls = @('KERNEL32.dll','USER32.dll','GDI32.dll','ADVAPI32.dll','SHELL32.dll',
    'OLEAUT32.dll','OLE32.dll','WS2_32.dll','SHLWAPI.dll','WINMM.dll',
    'COMCTL32.dll','COMDLG32.dll','VERSION.dll','RPCRT4.dll','IPHLPAPI.DLL',
    'ntdll.dll','api-ms-win-crt-string-l1-1-0.dll','WLDAP32.dll','Secur32.dll','Crypt32.dll')

$outputDir = 'd:\AccessControl（C#）\src\AccessControlPro.WPF\bin\Debug\net8.0-windows'

$checked = @{}
$queue = [System.Collections.ArrayList]@('CareaIfc.dll')
$allMissing = @()

while ($queue.Count -gt 0) {
    $current = $queue[0]
    $queue.RemoveAt(0)
    $key = $current.ToLower()
    if ($checked.ContainsKey($key)) { continue }
    $checked[$key] = $true

    $fullPath = Join-Path $outputDir $current
    if (!(Test-Path $fullPath)) {
        $isSystem = $false
        foreach ($s in $systemDlls) { if ($s.ToLower() -eq $key) { $isSystem = $true; break } }
        if (!$isSystem) {
            $allMissing += $current
        }
        continue
    }

    $deps = Get-PEImports $fullPath
    foreach ($dep in $deps) {
        $depKey = $dep.ToLower()
        $isSystem = $false
        foreach ($s in $systemDlls) { if ($s.ToLower() -eq $depKey) { $isSystem = $true; break } }
        if (!$isSystem -and !$checked.ContainsKey($depKey)) {
            $depPath = Join-Path $outputDir $dep
            if (!(Test-Path $depPath)) {
                Write-Output "MISSING: $dep (needed by $current)"
                $allMissing += $dep
            }
            $queue.Add($dep) | Out-Null
        }
    }
}

if ($allMissing.Count -eq 0) {
    Write-Output "All dependencies are satisfied!"
} else {
    Write-Output "--- MISSING DLLS ---"
    foreach ($m in $allMissing) {
        Write-Output "  $m"
    }
    Write-Output "Total missing: $($allMissing.Count)"
}
