ALTER TABLE file_sessions ADD COLUMN storage_backend TEXT NOT NULL DEFAULT 'd1';
ALTER TABLE file_sessions ADD COLUMN storage_key TEXT NOT NULL DEFAULT '';
ALTER TABLE file_sessions ADD COLUMN version_id TEXT NOT NULL DEFAULT '';

CREATE TABLE IF NOT EXISTS file_session_history (
  version_id TEXT PRIMARY KEY,
  company TEXT NOT NULL,
  file_kind TEXT NOT NULL,
  source_name TEXT NOT NULL DEFAULT '',
  payload_bytes INTEGER NOT NULL DEFAULT 0,
  storage_backend TEXT NOT NULL DEFAULT 'd1',
  storage_key TEXT NOT NULL DEFAULT '',
  chunk_count INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  restored_at TEXT
);

CREATE TABLE IF NOT EXISTS file_session_history_chunks (
  version_id TEXT NOT NULL,
  chunk_index INTEGER NOT NULL,
  payload_chunk TEXT NOT NULL,
  PRIMARY KEY(version_id, chunk_index)
);

CREATE INDEX IF NOT EXISTS idx_file_session_history_lookup
  ON file_session_history(company, file_kind, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_file_session_history_chunks_lookup
  ON file_session_history_chunks(version_id, chunk_index);
