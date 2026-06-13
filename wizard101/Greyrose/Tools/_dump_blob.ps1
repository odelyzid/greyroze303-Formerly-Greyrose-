$hex = @"
A8 00 4C 8F 6E 11 01 00 00 00 00 00 00 A9 81 22 01 00 00 AA 02 D0 DF 33 07 E8 03 00 00 00 07 8D D0 72 38 00 80 2D 80 00 40 00 01 00 00 00 88 BE C1 04 00 00 00 02 00 00 C3 CA F5 40 04 00 00 00 44 64 73 43 84 12 00 00 00 00 00 00 00 00 00 00 00 00 44 64 73 43 C6 EF 01 00 00 00 00 00 00 00 00 00 00 00 44 64 73 43 F1 2E 01 00 61 00 00 00 00 00 00 00 00 00 44 64 73 43 CC 12 00 00 61 00 00 00 00 00 00 00 00 00 14 00 57 69 7A 61 72 64 5A 6F 6E 65 5F 52 61 76 65 6E 77 6F 6F 64 04 00 00 00 0D 5B 25 00 20 3F 3F 00
"@
$parts = ($hex -split '\s+') | Where-Object { $_ }
$b = $parts | ForEach-Object { [Convert]::ToByte($_, 16) }
for ($start = 0; $start -lt 80; $start += 16) {
    $chunk = $b[$start..([Math]::Min($start + 15, $b.Count - 1))]
    $hexPart = ($chunk | ForEach-Object { $_.ToString('X2') }) -join ' '
    $ascii = -join ($chunk | ForEach-Object { if ($_ -ge 32 -and $_ -le 126) { [char]$_ } else { '.' } })
    Write-Host ("{0:X4}: {1,-47} {2}" -f $start, $hexPart, $ascii)
}
