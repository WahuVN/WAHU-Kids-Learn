-- WAHU Kids Learn — migration V6
-- Purpose: semantic answer commit keys are append-only authority and cannot be re-pointed.
-- Direct key deletion remains allowed so parent-row ON DELETE CASCADE cleanup keeps working.
-- Transaction/app_meta/migration_history are managed by MigrationManager.

CREATE TRIGGER IF NOT EXISTS trg_attempt_commit_key_immutable_update
BEFORE UPDATE ON attempt_commit_key
FOR EACH ROW
BEGIN
    SELECT RAISE(ABORT, 'ATTEMPT_COMMIT_KEY_IMMUTABLE');
END;
