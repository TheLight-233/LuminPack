param(
    [Parameter(Mandatory = $true)]
    [string] $Path,

    [switch] $ShowCalls
)

$lines = Get-Content -LiteralPath $Path
$tier1Start = [Array]::FindLastIndex(
    $lines,
    [Predicate[string]] { param($line) $line -eq '; Tier1 code' })

if ($tier1Start -lt 0) {
    throw "Tier1 assembly was not found in '$Path'."
}

$tier1 = $lines[$tier1Start..($lines.Length - 1)]
$instructionPattern = '^\s*(?:[A-Za-z][A-Za-z0-9]*)\s+'
$conditionalBranchPattern = '^\s*j(?:a|ae|b|be|c|e|g|ge|l|le|na|nae|nb|nbe|nc|ne|ng|nge|nl|nle|no|np|ns|nz|o|p|pe|po|s|z)\s+'

$codeSize = ($tier1 | Select-String -Pattern '^; Total bytes of code (\d+)$').Matches.Groups[1].Value
$stackFrameLine = $tier1 | Select-String -Pattern '^\s*sub\s+rsp,\s+(.+)$' | Select-Object -First 1
$stackFrame = if ($stackFrameLine) { $stackFrameLine.Matches[0].Groups[1].Value } else { '' }
$calls = ($tier1 | Select-String -Pattern '^\s*call\s+').Count
$conditionalBranches = ($tier1 | Select-String -Pattern $conditionalBranchPattern).Count
$jumps = ($tier1 | Select-String -Pattern '^\s*jmp\s+').Count
$stackReferences = ($tier1 | Select-String -Pattern '\[rsp(?:\+[^\]]+)?\]' -AllMatches | ForEach-Object Matches).Count
$instructions = ($tier1 | Select-String -Pattern $instructionPattern).Count

[pscustomobject]@{
    CodeSize = [int] $codeSize
    StackFrame = $stackFrame
    Instructions = $instructions
    Calls = $calls
    ConditionalBranches = $conditionalBranches
    Jumps = $jumps
    StackReferences = $stackReferences
}

if ($ShowCalls) {
    $tier1 |
        Select-String -Pattern '^\s*call\s+' |
        ForEach-Object { $_.Line.Trim() -replace '^call\s+', '' } |
        Group-Object |
        Sort-Object Count -Descending |
        Select-Object Count, Name
}
