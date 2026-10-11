# Repository-level checks that do NOT need the game installed.
# ASCII only: Windows PowerShell 5.1 reads .ps1 as ANSI, non-ASCII comments can
# swallow the following line.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$fail = 0

function Fail([string]$msg) {
    Write-Output ("FAIL: " + $msg)
    $script:fail++
}

# ---------- 1. version consistency ----------
$csproj = Join-Path $root 'src\ResupplyNetwork\ResupplyNetwork.csproj'
$verFile = Join-Path $root 'GameData\ResupplyNetwork\ResupplyNetwork.version'

# Always read as UTF-8: Windows PowerShell 5.1 defaults to the ANSI codepage,
# which mangles non-ASCII comments badly enough to swallow newlines.
$csprojText = Get-Content -LiteralPath $csproj -Raw -Encoding UTF8
$m = [regex]::Match($csprojText, '<Version>\s*([0-9]+)\.([0-9]+)\.([0-9]+)\s*</Version>')
if (-not $m.Success) { Fail 'csproj has no <Version>x.y.z</Version>' } else {
    $cv = "$($m.Groups[1].Value).$($m.Groups[2].Value).$($m.Groups[3].Value)"
}

$vText = Get-Content -LiteralPath $verFile -Raw -Encoding UTF8
$mm = [regex]::Match($vText, '"VERSION"\s*:\s*\{\s*"MAJOR"\s*:\s*([0-9]+)\s*,\s*"MINOR"\s*:\s*([0-9]+)\s*,\s*"PATCH"\s*:\s*([0-9]+)')
if (-not $mm.Success) { Fail 'version file has no VERSION block' } else {
    $fv = "$($mm.Groups[1].Value).$($mm.Groups[2].Value).$($mm.Groups[3].Value)"
}

if ($cv -ne $fv) { Fail ("version mismatch: csproj=" + $cv + "  version file=" + $fv) }
else { Write-Output ("OK  version " + $cv) }

# ---------- 2. localization key coverage ----------
$codeKeys = New-Object System.Collections.Generic.HashSet[string]
Get-ChildItem -LiteralPath (Join-Path $root 'src') -Recurse -Filter '*.cs' | ForEach-Object {
    $t = Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
    foreach ($k in [regex]::Matches($t, '"#LOC_RN_[A-Za-z0-9_]+"')) {
        [void]$codeKeys.Add($k.Value.Trim('"'))
    }
}

$langs = @{}
Get-ChildItem -LiteralPath (Join-Path $root 'GameData\ResupplyNetwork\Localization') -Filter '*.cfg' | ForEach-Object {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
    $set = New-Object System.Collections.Generic.HashSet[string]
    $t = Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
    foreach ($k in [regex]::Matches($t, '(#LOC_RN_[A-Za-z0-9_]+)\s*=')) {
        [void]$set.Add($k.Groups[1].Value)
    }
    $langs[$name] = $set
    Write-Output ("OK  " + $name + ": " + $set.Count + " keys")
}

foreach ($lang in $langs.Keys) {
    $set = $langs[$lang]
    foreach ($k in $codeKeys) {
        if (-not $set.Contains($k)) { Fail ($lang + " is missing key used in code: " + $k) }
    }
    foreach ($k in $set) {
        if (-not $codeKeys.Contains($k)) { Write-Output ("note " + $lang + " defines unused key: " + $k) }
    }
}

# every language must define the same key set
$names = @($langs.Keys)
if ($names.Count -gt 1) {
    for ($i = 1; $i -lt $names.Count; $i++) {
        foreach ($k in $langs[$names[0]]) {
            if (-not $langs[$names[$i]].Contains($k)) { Fail ($names[$i] + " is missing key defined in " + $names[0] + ": " + $k) }
        }
    }
}
if ($codeKeys.Count -eq 0) { Fail 'no localization keys found in code' }
else { Write-Output ("OK  code uses " + $codeKeys.Count + " localization keys") }

# ---------- 3. config sanity ----------
$cfg = Join-Path $root 'GameData\ResupplyNetwork\Config\ResupplySettings.cfg'
$cfgText = Get-Content -LiteralPath $cfg -Raw -Encoding UTF8
if ($cfgText -notmatch 'RESUPPLY_CONFIG') { Fail 'config has no RESUPPLY_CONFIG node' }
foreach ($key in 'K_mass', 'dv_ref', 'K_time', 'B_trip', 'S_trip', 'S_min', 'T_base', 'autoThreshold', 'autoTarget') {
    if (-not [regex]::IsMatch($cfgText, '(?m)^[ \t]*' + $key + '[ \t]*=')) { Fail ('config is missing key: ' + $key) }
}
# balanced braces
$open = ([regex]::Matches($cfgText, '\{')).Count
$close = ([regex]::Matches($cfgText, '\}')).Count
if ($open -ne $close) { Fail ("config braces unbalanced: " + $open + " vs " + $close) }
else { Write-Output ("OK  config (" + $open + " nodes)") }

Write-Output ""
if ($fail -gt 0) { Write-Output ("FAILED: " + $fail + " problem(s)"); exit 1 }
Write-Output "ALL CHECKS PASSED"
exit 0
