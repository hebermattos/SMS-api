#!/bin/sh
set -eu

: "${PGHOST:=postgres}"
: "${PGPORT:=5432}"
: "${PGDATABASE:=sms_api}"
: "${BACKUP_DIR:=/backups}"
: "${BACKUP_INTERVAL_SECONDS:=86400}"
: "${BACKUP_RETENTION_DAYS:=7}"

mkdir -p "$BACKUP_DIR"

backup() {
  timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
  target="$BACKUP_DIR/${PGDATABASE}_${timestamp}.dump"
  temporary="${target}.tmp"

  echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Backing up ${PGDATABASE}."
  pg_dump --format=custom --compress=6 --no-owner --no-acl --file="$temporary" "$PGDATABASE"
  mv "$temporary" "$target"
  echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Backup created: $(basename "$target")."

  find "$BACKUP_DIR" -type f -name "${PGDATABASE}_*.dump" -mtime "+$BACKUP_RETENTION_DAYS" -delete
}

while true; do
  if pg_isready -q; then
    backup || echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Backup failed." >&2
  else
    echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] PostgreSQL is not ready; backup skipped." >&2
  fi
  sleep "$BACKUP_INTERVAL_SECONDS"
done
