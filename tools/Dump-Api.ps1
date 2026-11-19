# 在装有 Captain of Industry 的机器上运行（Windows PowerShell 5.1）：
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Dump-Api.ps1
# 结果写入 api-dump.txt，用来校准 TopsoilTool 的地形/输入 API。

param(
    [string]$ManagedPath = "C:\Program Files (x86)\Steam\steamapps\common\Captain of Industry\Captain of Industry_Data\Managed",
    [string]$OutFile = "$PSScriptRoot\api-dump.txt"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path (Join-Path $ManagedPath "Mafi.Core.dll"))) {
    throw "找不到 Mafi.Core.dll，请把 -ManagedPath 指到游戏的 Managed 目录：$ManagedPath"
}

# 让依赖程序集从 Managed 目录解析
$handler = [ResolveEventHandler] {
    param($sender, $args)
    $name = (New-Object System.Reflection.AssemblyName($args.Name)).Name
    $candidate = Join-Path $ManagedPath "$name.dll"
    if (Test-Path $candidate) { return [Reflection.Assembly]::LoadFrom($candidate) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($handler)

foreach ($dll in "Mafi.dll", "Mafi.Base.dll", "Mafi.Core.dll", "Mafi.Unity.dll") {
    $path = Join-Path $ManagedPath $dll
    if (Test-Path $path) { [Reflection.Assembly]::LoadFrom($path) | Out-Null }
}

$keywords = "TerrainTile|TerrainManager|TerrainMaterial|RectangleTerrainArea2i|AreaSelectionTool|IUnityInputMgr|IUnityInputController|IToolbarItemInputController|KbHandleBuilder|ShortcutsManager|Tile2i"

function Get-AssemblyTypes($asm) {
    try { return $asm.GetTypes() }
    catch [System.Reflection.ReflectionTypeLoadException] {
        return $_.Exception.Types | Where-Object { $_ -ne $null }
    }
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("Captain of Industry API dump")
[void]$sb.AppendLine("ManagedPath: $ManagedPath")
[void]$sb.AppendLine("Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$sb.AppendLine("")

$all = @()
foreach ($asm in [AppDomain]::CurrentDomain.GetAssemblies()) {
    if ($asm.FullName -match "Mafi|UnityEngine") {
        $all += Get-AssemblyTypes $asm
    }
}

$matched = $all | Where-Object { $_.FullName -and ($_.FullName -match $keywords) } | Sort-Object FullName -Unique

foreach ($t in $matched) {
    [void]$sb.AppendLine("=== $($t.FullName)  (public=$($t.IsPublic), base=$($t.BaseType))")

    foreach ($ctor in $t.GetConstructors()) {
        [void]$sb.AppendLine("  ctor($(($ctor.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '))")
    }

    foreach ($m in ($t.GetMethods([System.Reflection.BindingFlags]"Public,Instance,Static,DeclaredOnly") | Sort-Object Name)) {
        $ps = ($m.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
        [void]$sb.AppendLine("  method $($m.ReturnType.Name) $($m.Name)($ps)")
    }

    foreach ($p in ($t.GetProperties() | Sort-Object Name)) {
        [void]$sb.AppendLine("  prop $($p.PropertyType.Name) $($p.Name)")
    }

    foreach ($f in ($t.GetFields() | Sort-Object Name)) {
        [void]$sb.AppendLine("  field $($f.FieldType.Name) $($f.Name)")
    }

    [void]$sb.AppendLine("")
}

$sb.ToString() | Set-Content -Path $OutFile -Encoding UTF8
Write-Host "已写入 $OutFile（$($matched.Count) 个类型）"
