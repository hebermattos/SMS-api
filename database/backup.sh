#!/bin/sh
set -eu

: "${PGHOST:=postgres}"
: "${PGPORT:=5432}"
: "${BACKUP_DIR:=/backups}"
: "${BACKUP_INTERVAL_SECONDS:=86400}"
: "${BACKUP_RETENTION_DAYS:=7}"

BASE_BACKUP_DIR="$BACKUP_DIR/base"
WAL_ARCHIVE_DIR="$BACKUP_DIR/wal"

mkdir -p "$BASE_BACKUP_DIR" "$WAL_ARCHIVE_DIR"

backup() {
  timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
  target="$BASE_BACKUP_DIR/$timestamp"
  temporary="$BASE_BACKUP_DIR/.$timestamp.tmp"

  rm -rf "$temporary"
  mkdir -p "$temporary"

  echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Creating PostgreSQL physical base backup."
  if pg_basebackup \
    --host="$PGHOST" \
    --port="$PGPORT" \
    --username="$PGUSER" \
    --pgdata="$temporary" \
    --format=tar \
    --gzip \
    --wal-method=stream \
    --checkpoint=fast \
    --no-password; then
    mv "$temporary" "$target"
    echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Base backup created: $(basename "$target")."
  else
    rm -rf "$temporary"
    echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Base backup failed." >&2
    return 1
  fi

  find "$BASE_BACKUP_DIR" -mindepth 1 -maxdepth 1 -type d -mtime "+$BACKUP_RETENTION_DAYS" -exec rm -rf {} \;
  find "$WAL_ARCHIVE_DIR" -type f -mtime "+$BACKUP_RETENTION_DAYS" -delete
}

while true; do
  if pg_isready -q -h "$PGHOST" -p "$PGPORT"; then
    backup || true
  else
    echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] PostgreSQL is not ready; backup skipped." >&2
  fi
  sleep "$BACKUP_INTERVAL_SECONDS"
done
