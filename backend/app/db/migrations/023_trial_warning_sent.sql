-- =============================================================================
-- Migration 023: Trial Warning Sent Flag
-- =============================================================================
--
-- Idempotent -> safe to run multiple times (uses IF NOT EXISTS).
-- Runs automatically on container startup via migrate.py.
--
-- Purpose:
--   Tracks whether a trial-ending warning email has been sent for each subscription.
--   Prevents duplicate emails when Stripe retries the trial_will_end event.
--
-- Behaviour:
--   1. subscription created with trial_warning_sent = FALSE
--   2. When trial_will_end fires, check: IF trial_warning_sent THEN skip
--   3. After email sent successfully, UPDATE trial_warning_sent = TRUE
--   4. Future retries skip because flag is already set
-- =============================================================================

ALTER TABLE trainer_subscriptions
    ADD COLUMN IF NOT EXISTS trial_warning_sent BOOLEAN NOT NULL DEFAULT FALSE;

-- Index for queries checking if warning has been sent
CREATE INDEX IF NOT EXISTS idx_trainer_subs_trial_warning_sent
    ON trainer_subscriptions (trial_warning_sent)
    WHERE trial_warning_sent = TRUE;