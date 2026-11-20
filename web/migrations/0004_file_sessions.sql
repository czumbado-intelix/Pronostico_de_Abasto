CREATE TABLE IF NOT EXISTS file_sessions (
  company TEXT NOT NULL,
  file_kind TEXT NOT NULL,
  source_name TEXT NOT NULL DEFAULT '',
  payload_bytes INTEGER NOT NULL DEFAULT 0,
  chunk_count INTEGER NOT NULL DEFAULT 0,
  updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY(company, file_kind)
);

CREATE TABLE IF NOT EXISTS file_session_chunks (
  company TEXT NOT NULL,
  file_kind TEXT NOT NULL,
  chunk_index INTEGER NOT NULL,
  payload_chunk TEXT NOT NULL,
  PRIMARY KEY(company, file_kind, chunk_index)
);

CREATE INDEX IF NOT EXISTS idx_file_session_chunks_lookup
  ON file_session_chunks(company, file_kind, chunk_index);
