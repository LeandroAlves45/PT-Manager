-- Migration 026: Add email verification fields to users table
-- Purpose: Add email_verified and email_verified_at fields for P0.1 Email Verification

ALTER TABLE users
ADD COLUMN email_verified BOOLEAN NOT NULL DEFAULT FALSE,
ADD COLUMN email_verified_at TIMESTAMP WITH TIME ZONE;

-- Index para queries rápidas de users não verificados
CREATE INDEX idx_users_email_verified ON users(email_verified);