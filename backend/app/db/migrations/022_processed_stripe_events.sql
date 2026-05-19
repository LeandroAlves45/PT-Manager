-- ==============================================
-- Migration 022: Processed Stripe Events
-- ==============================================
--
-- Idempotent -> safe to run multiple times (all statements use IF NOT EXISTS).
-- Runs automatically on container startup via migrate.py.
--
-- Purpose:
--   Deduplicates Stripe webhook retries by storing events IDs.
--   whem Stripe retries a event, we check if it already exists and skip reprocessing.
--
-- Behaviour:
--  1. webhook endpoint receives event with event["id"]
--  2. Check: SELECT * FROM processed_stripe_events WHERE event_id = X
--  3. If exists → log "duplicate event", skip handler
--  4. If not exists → execute handler, INSERT into processed_stripe_events
-- =============================================================================

CREATE TABLE IF NOT EXISTS processed_stripe_events (
    event_id   VARCHAR   PRIMARY KEY,
    event_type VARCHAR   NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Fast cleanup for old events (optional, retention policy)
CREATE INDEX IF NOT EXISTS idx_processed_stripe_events_processed_at
    ON processed_stripe_events (processed_at);