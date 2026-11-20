CREATE TABLE IF NOT EXISTS order_state (
  company TEXT NOT NULL,
  period_key TEXT NOT NULL,
  row_key TEXT NOT NULL,
  ordered INTEGER NOT NULL DEFAULT 0,
  selected_pallet_keys TEXT NOT NULL DEFAULT '[]',
  updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY(company, period_key, row_key)
);

CREATE TABLE IF NOT EXISTS comparison_comments (
  company TEXT NOT NULL,
  article TEXT NOT NULL,
  comment TEXT NOT NULL,
  updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY(company, article)
);
