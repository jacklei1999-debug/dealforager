# DealForEager Project Rules & Instructions

## Deployment Workflow ("Deploy" Command)

Whenever the user instructs to **"Deploy"** (or asks to deploy the project):

### 1. Destination Confirmation
Always prompt the user to select the deployment destination using the `ask_question` tool with:
- **First choice (Default)**: `(Recommended) smb://10.250.1.250/DealWebServer` (mounted locally at `/Volumes/DealWebServer`)
- **Second choice**: `smb://192.168.200.180/DealWebServer` (credentials: `wg:Wg123123`, mounted locally at `/Volumes/DealWebServer-1` or `/Volumes/DealWebServer`)
- Option for user to specify an alternative path.

### 2. Backup Policy (Last Version Only)
Before deploying any new code to the target folder:
- Create a backup of the current target directory contents to `<TargetParent>/<TargetFolder>_Backup` (or `<Target>/_backup_last_version`).
- **Rule**: Maintain only **one backup** (the immediate previous version). Overwrite/replace any older backup so disk space does not grow unbounded.

### 3. Production Database Policy
- **CRITICAL**: **NEVER overwrite the production database file** (`DealA.db`, `DealA.db-wal`, `DealA.db-shm`) with a local or development database file during copy/rsync.
- **Instructed Database Updates**: When new database changes are instructed (e.g., schema migrations, new columns/tables, performance indexes, or data scripts), apply these changes **directly to the production database** using safe SQL migrations (`ALTER TABLE`, `CREATE INDEX IF NOT EXISTS`) or EF Core migration pipelines.

### 4. Incremental Code Changes Only
- Build and publish the self-contained package (`dotnet publish WebUIMVC.csproj -c Release -r win-x64 --self-contained true`).
- Sync only **new or updated files** using `rsync -av --update` (or timestamp/checksum comparison), excluding database files.
- Do not re-transfer unchanged runtime binaries.

---

## Architectural Guidelines

- Target framework: `net8.0`.
- All database operations must be asynchronous (`async`/`await`) with `CancellationToken`.
- Maintain decoupled `IProductService` pattern and memory caching for feeds and wishlist counts.
- Background ingestion must maintain batch lookups and `SemaphoreSlim` database write synchronization.
