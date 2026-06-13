function Get-WizHashString([string]$value) {
    $input = [Text.Encoding]::ASCII.GetBytes($value)
    $hash = [uint32]0
    if ($input.Length -eq 0) { return $hash }
    $iVar3 = 0
    $iVar4 = 0
    foreach ($b in $input) {
        $cVar2 = $b - 32
        $hash = $hash -bxor ([uint32]($cVar2 -shl ($iVar3 -band 0x1f)))
        if ($iVar3 -gt 0x18) {
            $hash = $hash -bxor ([uint32]($cVar2 -shr ($iVar4 -band 0x1f)))
            if ($iVar3 -gt 0x1a) {
                $iVar3 -= 32
                $iVar4 += 32
            }
        }
        $iVar3 += 5
        $iVar4 -= 5
    }
    if ([int32]$hash -lt 0) { $hash = [uint32](-[int32]$hash) }
    return $hash
}

$blobStart = 0x116E8F4C
Write-Host ("blob offset2 LE = {0:X8}" -f $blobStart)
foreach ($name in @('Galen Sparkleglen','GALEN SPARKLEGLEN','Galen SparkleGlen','WizardZone_Ravenwood','oz')) {
    $h = Get-WizHashString $name
    Write-Host ("{0} => {1:X8}" -f $name, $h)
}
