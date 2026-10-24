# Local backup and restore drill

At **2026-10-24 11:56 UTC**, PostgreSQL 18.6 successfully dumped the quiescent local synthetic database and restored it into a **fresh, separate** database named `farmhand_restore_20261024_buildathon`. No source records or existing destination database were overwritten. No production system was involved.

The custom-format backup was 79,638 bytes. Row counts matched for all 15 selected tables, including farms, users, observations, assessments, cases, vet requests, payments, treatment plans/steps, audit records, tokens and the migration history. The source had 13 applied migrations. See [aggregate result](results/restore.json); the private dump remains outside Git because even synthetic dumps contain credential hashes and session records.

The executable [restore-drill.ps1](../../scripts/restore-drill.ps1) rejects non-loopback hosts, requires explicit isolated-environment acknowledgement, refuses an existing destination/backup, uses `pg_restore --exit-on-error`, and leaves databases intact for inspection. With PostgreSQL tools available and credentials provided privately through standard `PG*` environment variables:

```powershell
./scripts/restore-drill.ps1 -PgBin '/path/to/postgres/bin' `
  -SourceDatabase farmhand_demo `
  -TargetDatabase farmhand_restore_unique_suffix `
  -BackupPath '/private/existing/directory/fresh-backup.dump' -ConfirmIsolated
```

The script is for PowerShell with Windows PostgreSQL executables. On Linux use the corresponding `pg_dump`, fresh `createdb` and `pg_restore` commands in [DEPLOYMENT.md](../../DEPLOYMENT.md). Keep the source quiescent when comparing row counts. Do not expose passwords in command arguments or console output.

This proves local schema/data restoration and count equality, not byte-for-byte content equality, application startup against the restored database, off-host durability, production recovery time or point-in-time recovery. The database, dump and restored database shared one disk. RPO/RTO, encrypted off-host retention and alerts are not measured.

For production, back up PostgreSQL **and** the persistent upload volume and field-encryption key. Restrict backup access, encrypt off-host copies, define retention/RPO/RTO, and rehearse a release-specific restore on an isolated host. Check the restored application's readiness and representative role workflows before changing traffic. Never restore over production as a rehearsal.
