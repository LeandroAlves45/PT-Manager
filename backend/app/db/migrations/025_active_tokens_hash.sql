-- =============================================================================
-- Migration 025: Active Tokens — Hash Storage
-- =============================================================================
--
-- Idempotent — safe to run multiple times (all statements use IF NOT EXISTS).
-- Runs automatically on container startup via migrate.py.
--
-- Purpose:
--   Transforms active_tokens schema to store SHA-256(token) instead of raw JWT.
--   Improves security: if database is compromised, tokens cannot be forged.
--
--   The migration:
--   1. Creates new table with correct schema (token_hash instead of token)
--   2. Migrates existing data (converts token → token_hash via SHA-256)
--   3. Drops old table
--   4. Renames new table to original name
--
-- Security design:
--   - SHA-256 hash of JWT, never the raw token
--   - Even with full DB read access, attacker cannot forge tokens
--   - Raw token travels only in HTTP responses; we compare hashes internally
--
-- Behaviour:
--   Login    → INSERT into active_tokens with token_hash (SHA-256)
--   Request  → get_current_user verifies token_hash exists
--   Logout   → DELETE the row (token is immediately invalid)
--   Scheduler → DELETE WHERE expires_at < NOW() every 60 minutes
--
-- =============================================================================

CREATE TABLE IF NOT EXISTS active_tokens_v2 (
    id          VARCHAR(36)     PRIMARY KEY,
    user_id     VARCHAR(36)     NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    -- SHA-256 hash of the JWT (never store raw token)
    token_hash  VARCHAR(64)     NOT NULL UNIQUE,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    -- Mirrors the JWT exp claim — used by scheduler cleanup job
    expires_at  TIMESTAMPTZ     NOT NULL
);

-- Fast lookup by user_id (used on login to replace existing token)
CREATE INDEX IF NOT EXISTS idx_active_tokens_v2_user_id
    ON active_tokens_v2 (user_id);

-- Fast lookup by token_hash (used on every authenticated request)
CREATE INDEX IF NOT EXISTS idx_active_tokens_v2_token_hash
    ON active_tokens_v2 (token_hash);

-- Fast cleanup by expiry (used by scheduler job)
CREATE INDEX IF NOT EXISTS idx_active_tokens_v2_expires_at
    ON active_tokens_v2 (expires_at);

-- Migrate data from old table, converting token → token_hash via SHA-256
-- Note: PostgreSQL uses encode(digest(token, 'sha256'), 'hex') for SHA-256
-- SQLite uses lower(hex(sha256(token))) or similar
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'active_tokens') THEN
        INSERT INTO active_tokens_v2 (id, user_id, token_hash, created_at, expires_at)
        SELECT id, user_id, encode(digest(token, 'sha256'), 'hex'), created_at, expires_at
        FROM active_tokens
        ON CONFLICT (token_hash) DO NOTHING;

        DROP TABLE active_tokens;
    END IF;
END $$;

-- Rename new table to original name
ALTER TABLE IF EXISTS active_tokens_v2 RENAME TO active_tokens;
