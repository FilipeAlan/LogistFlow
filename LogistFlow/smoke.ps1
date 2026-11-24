$ErrorActionPreference = 'Stop'
$assembly = Join-Path $PSScriptRoot 'bin/Release/net10.0/LogistFlow.dll'
if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) {
    throw 'Build the project in Release mode before running the smoke tests.'
}

$obj = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'obj')).Path
$sandbox = Join-Path $obj ('smoke-' + [guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($sandbox)
$dataFile = Join-Path $sandbox 'data/collections.json'
# Reserve an unused loopback port without listening. Each child gets an offline
# HTTPS proxy, so registration exercises the manual fallback without contacting ViaCEP.
$offlineProxy = New-Object Net.Sockets.Socket('InterNetwork', 'Stream', 'Tcp')
$offlineProxy.Bind([Net.IPEndPoint]::new([Net.IPAddress]::Loopback, 0))

function Invoke-Menu([string[]]$Answers, [switch]$ExpectFailure) {
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo.FileName = 'dotnet'
    $process.StartInfo.Arguments = '"' + $assembly + '"'
    $process.StartInfo.WorkingDirectory = $sandbox
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.RedirectStandardInput = $true
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    $process.StartInfo.EnvironmentVariables['HTTPS_PROXY'] = "http://127.0.0.1:$($offlineProxy.LocalEndPoint.Port)"
    $process.StartInfo.EnvironmentVariables['NO_PROXY'] = ''
    try {
        $null = $process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        foreach ($answer in $Answers) { $process.StandardInput.WriteLine($answer) }
        $process.StandardInput.Close()
        if (-not $process.WaitForExit(10000)) {
            $process.Kill()
            throw 'The application did not exit within ten seconds.'
        }
        $output = $stdout.Result + $stderr.Result
        if (($process.ExitCode -ne 0) -ne $ExpectFailure.IsPresent) {
            throw "Unexpected application exit code $($process.ExitCode). Output: $output"
        }
        return $output
    }
    finally { $process.Dispose() }
}

function Assert-Contains([string]$Output, [string[]]$Expected) {
    foreach ($value in $Expected) {
        if (-not $Output.Contains($value)) { throw "Expected '$value' in: $Output" }
    }
}

function Assert-Excludes([string]$Output, [string[]]$Excluded) {
    foreach ($value in $Excluded) {
        if ($Output.Contains($value)) { throw "Unexpected '$value' in: $Output" }
    }
}

try {
    $empty = Invoke-Menu @('42', '2', '6', '7')
    Assert-Contains $empty @('LogistFlow', 'Invalid menu option', 'No collections found.', 'Total collections: 0',
        'Total packages: 0', 'Medium: 0', 'Completed: 0')
    $registration = Invoke-Menu @('1', ' ', 'Ada', 'ABCDE', '01001000', '', 'West Road', 'x', '0', '2', '9', '3',
                                 '1', 'Ben', '20040-020', 'East Road', '5', '1', '7')
    Assert-Contains $registration @('A value is required.', 'Enter a positive whole number.',
        'Enter a whole number from 1 to 3.', 'Enter an eight-digit postal code', 'Enter the collection address manually.')
    $saved = Get-Content -LiteralPath $dataFile -Raw | ConvertFrom-Json
    if ($saved.Count -ne 2 -or $saved[0].Status -ne 'Pending' -or $saved[1].Status -ne 'Pending') {
        throw 'Both registrations must be persisted with Pending status.'
    }
    Write-Output 'PASS: missing file, empty summary, invalid input, registration and exit.'

    $first = '#1 | Ada | West Road, CEP 01001-000 | Packages: 2 | Priority: High | Status: Pending'
    $second = '#2 | Ben | East Road, CEP 20040-020 | Packages: 5 | Priority: Low | Status: Pending'
    Assert-Contains (Invoke-Menu @('2', '7')) @($first, $second)
    Assert-Contains (Invoke-Menu @('3', '3', '7')) @('No collections found.')
    $change = Invoke-Menu @('5', '-1', '82', '5', '1', 'bad', '2', '5', '2', '3',
                            '1', 'Cal', '30140071', 'Hill Road', '4', '3', '7')
    Assert-Contains $change @('Collection not found.')
    $inRoute = $first.Replace('Pending', 'InRoute')
    $completed = $second.Replace('Pending', 'Completed')
    $third = '#3 | Cal | Hill Road, CEP 30140-071 | Packages: 4 | Priority: High | Status: Pending'
    $list = Invoke-Menu @('2', '6', '7')
    Assert-Contains $list @($inRoute, $completed, $third, 'Total collections: 3', 'Total packages: 11',
        'Low: 1', 'Medium: 0', 'High: 2', 'Pending: 1', 'InRoute: 1', 'Completed: 1')
    $status = Invoke-Menu @('3', '2', '7')
    Assert-Contains $status @($inRoute)
    Assert-Excludes $status @($completed, $third)
    $priority = Invoke-Menu @('4', '3', '7')
    Assert-Contains $priority @($inRoute, $third)
    Assert-Excludes $priority @($completed)
    Write-Output 'PASS: list, both filters, status updates, unknown ID, summary and unique IDs after restart.'

    $before = (Get-FileHash -LiteralPath $dataFile).Hash
    Assert-Contains (Invoke-Menu @('1', 'Interrupted customer')) @('Input ended.')
    $null = [IO.Directory]::CreateDirectory("$dataFile.tmp")
    $failed = Invoke-Menu @('1', 'Unsaved', '01001000', 'Nowhere', '8', '2', '5', '1', '3', '2', '6', '7')
    Assert-Contains $failed @('Could not save collections.', $inRoute, 'Total collections: 3', 'Total packages: 11')
    Assert-Excludes $failed @('#4 |', $first.Replace('Pending', 'Completed'))
    if ((Get-FileHash -LiteralPath $dataFile).Hash -ne $before) { throw 'A failed edit changed the data file.' }
    [IO.Directory]::Delete("$dataFile.tmp")
    Write-Output 'PASS: interrupted input and failed saves preserve the last committed data.'

    $lockPath = Join-Path $sandbox 'data/collections.lock'
    $lock = [IO.File]::Open($lockPath, 'Open', 'ReadWrite', 'None')
    try {
        Assert-Contains (Invoke-Menu -Answers @() -ExpectFailure) @('Cannot access collection data.')
    }
    finally { $lock.Dispose() }
    Write-Output 'PASS: a concurrent writer cannot acquire the active data directory.'

    $saved[0].Id = 29
    $saved[1].Id = 3
    $saved[0].PackageCount = [int]::MaxValue
    $saved[0].CollectionAddress = 'Legacy address'
    [IO.File]::WriteAllText($dataFile, (ConvertTo-Json -InputObject @($saved[0], $saved[1])))
    $null = Invoke-Menu @('1', 'Dee', '01001000', 'South Road', '2', '2', '7')
    $gapped = Invoke-Menu @('2', '6', '7')
    Assert-Contains $gapped @('#29 | Ada | Legacy address |', '#30 | Dee | South Road, CEP 01001-000 | Packages: 2 | Priority: Medium | Status: Pending',
        'Total collections: 3', 'Total packages: 2147483654', 'Medium: 1')
    Write-Output 'PASS: new ID follows the highest stored ID, and large package totals use 64 bits.'

    $duplicateIds = ConvertTo-Json -InputObject @($saved[0], $saved[0])
    foreach ($badData in @('{broken', '[{}]', $duplicateIds)) {
        [IO.File]::WriteAllText($dataFile, $badData)
        Assert-Contains (Invoke-Menu -Answers @() -ExpectFailure) @('Invalid data in data/collections.json.')
        if ([IO.File]::ReadAllText($dataFile) -cne $badData) { throw 'Startup overwrote invalid data.' }
    }
    Write-Output 'PASS: malformed JSON, incomplete records and duplicate IDs are preserved and reported at startup.'
}
finally {
    $offlineProxy.Dispose()
    $testPath = (Resolve-Path -LiteralPath $sandbox).Path
    if ((Split-Path -Parent $testPath) -eq $obj -and (Split-Path -Leaf $testPath) -match '^smoke-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $testPath -Recurse -Force
    }
}
