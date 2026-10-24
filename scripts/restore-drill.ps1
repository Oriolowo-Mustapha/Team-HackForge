param(
    [Parameter(Mandatory)][string]$PgBin,
    [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9_]+$')][string]$SourceDatabase,
    [Parameter(Mandatory)][ValidatePattern('^farmhand_restore_[a-z0-9_]+$')][string]$TargetDatabase,
    [Parameter(Mandatory)][string]$BackupPath,
    [switch]$ConfirmIsolated
)
$ErrorActionPreference = 'Stop'
if (!$ConfirmIsolated -or $env:PGHOST -notin @('127.0.0.1', 'localhost', '::1')) {
    throw 'This drill requires -ConfirmIsolated and loopback PGHOST. Never use a production database.'
}
if ($SourceDatabase -eq $TargetDatabase) { throw 'Restore destination must be a separate, fresh database.' }
$absoluteBackup = [IO.Path]::GetFullPath($BackupPath)
if (Test-Path -LiteralPath $absoluteBackup) { throw 'Refusing to overwrite an existing backup.' }
if (!(Test-Path -LiteralPath ([IO.Path]::GetDirectoryName($absoluteBackup)))) { throw 'Backup parent must already exist.' }

function Invoke-Pg([string]$Name, [string[]]$Arguments) {
    $output = & (Join-Path $PgBin "$Name.exe") @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit $LASTEXITCODE). Existing source data was not overwritten." }
    return $output
}
$exists = Invoke-Pg 'psql' @('-X', '-d', $SourceDatabase, '-At', '-v', 'ON_ERROR_STOP=1', '-c', "SELECT 1 FROM pg_database WHERE datname = '$TargetDatabase'")
if ($exists) { throw 'Restore destination already exists; refusing to modify it.' }
$tables = @('Farms','LivestockCategories','LivestockAnimals','PoultryObservations','Incidents','VetRequests','Payments','TreatmentPlans','TreatmentScheduleItems','Users','RefreshTokens','PasswordResetTokens','AIDiagnoses','AuditEvents','__EFMigrationsHistory')
$countsSql = ($tables | ForEach-Object { "SELECT '$_' AS name, count(*) FROM `"$_`"" }) -join ' UNION ALL '
$before = @(Invoke-Pg 'psql' @('-X', '-d', $SourceDatabase, '-At', '-v', 'ON_ERROR_STOP=1', '-c', $countsSql))
Invoke-Pg 'pg_dump' @('--format=custom', '--no-owner', '--no-acl', '--dbname', $SourceDatabase, '--file', $absoluteBackup) | Out-Null
Invoke-Pg 'createdb' @('--maintenance-db', $SourceDatabase, $TargetDatabase) | Out-Null
Invoke-Pg 'pg_restore' @('--exit-on-error', '--no-owner', '--no-acl', '--dbname', $TargetDatabase, $absoluteBackup) | Out-Null
$after = @(Invoke-Pg 'psql' @('-X', '-d', $TargetDatabase, '-At', '-v', 'ON_ERROR_STOP=1', '-c', $countsSql))
if (Compare-Object $before $after) { throw 'Restored counts differ. Keep both databases for inspection; no data was deleted.' }
[pscustomobject]@{
    VerifiedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    SourceDatabase = $SourceDatabase
    RestoredDatabase = $TargetDatabase
    TablesCompared = $tables.Count
    RowCounts = $after
    BackupBytes = (Get-Item -LiteralPath $absoluteBackup).Length
    Result = 'Restore succeeded; all selected row counts match.'
    Limitation = 'Quiescent local synthetic data only; no application startup, content hash or production recovery-time guarantee.'
} | ConvertTo-Json -Depth 3
