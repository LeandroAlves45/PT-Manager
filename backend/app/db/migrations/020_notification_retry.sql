-- 020_notification_retry.sql
--
-- Adds support for retries with exponential backoff for notifications.
--
-- Problem this solves:
--   Before this migration, a notification that failed was marked as
--   FAILED permanently with no retry. If Resend had a temporary
--   failure, the email would be silently lost.
--
-- What this migration does:
--   Adds the retry_count column to the notifications table.
--   The scheduler uses this value to decide how many send attempts have
--   already been made and to calculate the next scheduled_for using
--   exponential backoff.
--
-- Backoff logic (implemented in scheduler.py):
--   retry 1 -> +5 min
--   retry 2 -> +10 min
--   retry 3 -> +20 min -> permanently FAILED (MAX_RETRIES = 3)
--
-- DEFAULT 0: existing notifications in the database start with zero
--   attempts. If they are still PENDING, they will be processed normally
--   without side effects.

ALTER TABLE notifications
    ADD COLUMN IF NOT EXISTS retry_count INTEGER NOT NULL DEFAULT 0;
