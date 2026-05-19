-- =============================================================================
-- Migration 024: Refresh Tokens
-- =============================================================================
--
-- Idempotent — safe to run multiple times (all statements use IF NOT EXISTS).
-- Runs automatically on container startup via migrate.py.
--
-- Purpose:
--   Implements refresh token rotation for long-lived sessions without storing
--   full JWTs in the database. Improves security against session hijacking.
--
-- Security design:
--   - Raw refresh token is NEVER stored. Only the SHA-256 hex hash is persisted.
--   - Raw token travels only in httpOnly cookies; hash is what we compare.
--   - This means even with full DB read access an attacker cannot forge a token.
--   - Tokens expire after 30 days and support rotation (each refresh = new token).
--   - Revoked tokens remain in the database with revoked_at timestamp (audit trail).
--
-- Behaviour:
--   Login     → INSERT into refresh_tokens with token_hash (opaque, httpOnly cookie)
--   Refresh   → Verify token_hash exists and not revoked, emit new token, new hash
--   Logout    → SET revoked_at (don't delete, keep for audit)
--   Scheduler → DELETE WHERE revoked_at < NOW() - 90 days (retention policy)
-- =============================================================================

CREATE TABLE IF NOT EXISTS refresh_tokens (
    id          VARCHAR(36)     PRIMARY KEY,
    user_id     VARCHAR(36)     NOT NULL REFERENCES user(id) ON DELETE CASCADE,
    -- SHA-256 hash of the opaque refresh token (never store raw token)
    token_hash  VARCHAR(64)     NOT NULL UNIQUE,
    created_at  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    -- When this refresh token expires (30 days from creation)
    expires_at  TIMESTAMPTZ     NOT NULL,
    -- When this token was revoked (NULL = still active, timestamp = revoked)
    revoked_at  TIMESTAMPTZ     NULL,
    -- Optional: User-Agent snippet for debugging / device identification
    device_hint VARCHAR(255)    NULL
);

-- Fast lookup by user_id (used on logout to revoke all user tokens)
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_user_id
    ON refresh_tokens (user_id);

-- Fast lookup by token_hash (used on every refresh request)
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_token_hash
    ON refresh_tokens (token_hash);

-- Fast cleanup by expiry (used by the scheduler job to delete old revoked tokens)
CREATE INDEX IF NOT EXISTS idx_refresh_tokens_expires_at
    ON refresh_tokens (expires_at);