CREATE TABLE IF NOT EXISTS settings (
  company TEXT PRIMARY KEY,
  satellite_zones TEXT NOT NULL,
  excluded_zones TEXT NOT NULL,
  thresholds TEXT NOT NULL,
  updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS transit_items (
  id TEXT PRIMARY KEY,
  company TEXT NOT NULL,
  period_key TEXT NOT NULL DEFAULT '',
  article TEXT NOT NULL,
  description TEXT NOT NULL DEFAULT '',
  pallet TEXT NOT NULL,
  quantity REAL NOT NULL,
  storage_zone TEXT NOT NULL DEFAULT '',
  location TEXT NOT NULL DEFAULT '',
  validation_date TEXT,
  tarima_size TEXT NOT NULL DEFAULT '',
  origin TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  received_at TEXT,
  reconciled_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_transit_company_status
  ON transit_items(company, received_at, reconciled_at);

CREATE INDEX IF NOT EXISTS idx_transit_company_article
  ON transit_items(company, article);

CREATE UNIQUE INDEX IF NOT EXISTS idx_transit_pending_pallet
  ON transit_items(company, article, pallet)
  WHERE received_at IS NULL AND reconciled_at IS NULL;
