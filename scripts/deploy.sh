#!/usr/bin/env bash
set -euo pipefail

TARGET_INPUT="${1:-/Volumes/DealWebServer}"

# Resolve destination based on choice or path
case "$TARGET_INPUT" in
    "1"|"10.250.1.250"|*"10.250.1.250"*)
        DEST_DIR="/Volumes/DealWebServer"
        ;;
    "2"|"192.168.200.180"|*"192.168.200.180"*)
        if [ -d "/Volumes/DealWebServer-1" ]; then
            DEST_DIR="/Volumes/DealWebServer-1"
        elif [ -d "/Volumes/DealWebServer" ] && mount | grep -q "192.168.200.180"; then
            DEST_DIR="/Volumes/DealWebServer"
        else
            echo "Attempting to mount smb://wg:Wg123123@192.168.200.180/DealWebServer..."
            mkdir -p /Volumes/DealWebServer-1
            mount_smbfs "//wg:Wg123123@192.168.200.180/DealWebServer" /Volumes/DealWebServer-1
            DEST_DIR="/Volumes/DealWebServer-1"
        fi
        ;;
    *)
        DEST_DIR="$TARGET_INPUT"
        ;;
esac

BACKUP_DIR="${DEST_DIR}/_backup_last_version"
STAGING_DIR="./bin/publish-staging-win64"

echo "=== DealForEager Deployment Pipeline ==="
echo "Target Destination: $DEST_DIR"

# 1. Verify destination access
if [ ! -d "$DEST_DIR" ]; then
    echo "Error: Target directory '$DEST_DIR' is not accessible or not mounted."
    exit 1
fi

# 2. Backup existing version (Last version only)
echo "Step 1: Backing up existing version to '$BACKUP_DIR'..."
# Only backup if there are existing application files besides .DS_Store and _backup_last_version
EXISTING_FILES=$(find "$DEST_DIR" -maxdepth 1 -not -name ".*" -not -name "_backup*" | wc -l)
if [ "$EXISTING_FILES" -gt 0 ]; then
    mkdir -p "$BACKUP_DIR"
    rsync -a --delete --exclude="_backup*" "$DEST_DIR/" "$BACKUP_DIR/"
    echo "Backup complete. Retained only the last version in $BACKUP_DIR."
else
    echo "Fresh target destination detected. Skipping backup."
fi

# 3. Publish latest self-contained Windows package
echo "Step 2: Publishing self-contained win-x64 release..."
rm -rf "$STAGING_DIR"
dotnet publish WebUIMVC.csproj -c Release -r win-x64 --self-contained true -o "$STAGING_DIR"

# 4. Deploy only updated code changes (Preserve production database)
echo "Step 3: Deploying updated code files (Excluding production database)..."
rsync -av --update \
    --exclude="*.db" \
    --exclude="*.db-wal" \
    --exclude="*.db-shm" \
    --exclude="_backup*" \
    "$STAGING_DIR/" "$DEST_DIR/"

# If the target is fresh and has no database yet, initialize it from local checkpoint
if [ ! -f "$DEST_DIR/DealA.db" ]; then
    echo "Initial deployment detected: copying initial database..."
    sqlite3 DealA.db "PRAGMA wal_checkpoint(TRUNCATE);"
    cp DealA.db "$DEST_DIR/DealA.db"
fi

# Clean up temporary staging
rm -rf "$STAGING_DIR"

echo "=== Deployment Succeeded! ==="

