$asm = [System.Reflection.Assembly]::LoadFrom('D:\AccessControlPro\src\AccessControlPro.WPF\bin\Debug\net8.0-windows\FCardCDrive.dll')
$types = $asm.GetExportedTypes()
foreach ($t in $types) {
    Write-Output "=== TYPE: $($t.FullName) ==="
    $methods = $t.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::DeclaredOnly)
    foreach ($m in $methods) {
        $params = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
        Write-Output "  METHOD: $($m.ReturnType.Name) $($m.Name)($params)"
    }
    $events = $t.GetEvents()
    foreach ($e in $events) {
        Write-Output "  EVENT: $($e.Name)"
    }
    $enums = $t.GetNestedTypes() | Where-Object { $_.IsEnum }
    foreach ($en in $enums) {
        $vals = [Enum]::GetNames($en) -join ', '
        Write-Output "  ENUM: $($en.Name) = $vals"
    }
    Write-Output ''
}
