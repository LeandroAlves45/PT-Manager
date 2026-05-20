-- =============================================================================
-- Migration 027: Email Verification Token Separation
-- =============================================================================
--
-- Idempotent — safe to run multiple times (all statements use IF NOT EXISTS).
-- Runs automatically on container startup via migrate.py.
--
-- Purpose:
--   Separates email verification tokens from invite tokens into dedicated columns.
--   Fixes cross-endpoint token confusion attack: invite tokens can no longer be
--   submitted to verify-email endpoint.
--
-- Security issue (FIXED):
--   The invite_token_hash column was overloaded for TWO purposes:
--   1. Client invite tokens (from InviteService) => invite_token_hash
--   2. Trainer email verification tokens (from SignupService) => invite_token_hash
--
--   This allowed a client invite token to be accepted by /auth/verify-email,
--   since that endpoint queries invite_token_hash without role discrimination.
--
-- Solution:
--   Add dedicated email_verification_token_hash and email_verification_token_expires_at.
--   Migrate any pending verification tokens to the new columns.
--   Clean up the old invite_token fields for trainers (they're now unused for verification).
--
-- After this migration:
--   - Trainers: email verification uses new columns only
--   - Clients: invite tokens still use invite_token_hash (unchanged)
--   - No cross-endpoint confusion possible (different token columns per use case)
--
-- =============================================================================

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS email_verification_token_hash VARCHAR(64),
    ADD COLUMN IF NOT EXISTS email_verification_token_expires_at TIMESTAMPTZ;

-- Fast lookup by email verification token hash (used on every verify-email request)
CREATE INDEX IF NOT EXISTS idx_users_email_verification_token_hash
    ON users (email_verification_token_hash)
    WHERE email_verification_token_hash IS NOT NULL;

-- Migrate any existing pending email verification tokens to the new columns.
-- These were previously stored in invite_token_hash (trainers only).
UPDATE users
SET email_verification_token_hash = invite_token_hash,
    email_verification_token_expires_at = invite_token_expires_at
WHERE role = 'trainer'
  AND email_verified = FALSE
  AND invite_token_hash IS NOT NULL;

-- Clean up the old invite_token fields in trainers that haven't verified yet.
-- The verification token is now in email_verification_token_hash;
-- invite_token_hash is no longer used for verification (only for actual invites to clients).
UPDATE users
SET invite_token_hash = NULL, invite_token_expires_at = NULL
WHERE role = 'trainer'
  AND email_verified = FALSE;
