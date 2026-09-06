-- WAHU Kids Learn — migration V2
-- Purpose: committed attempts are immutable. Corrections must be append-only
-- records in attempt_correction_event, never UPDATEs that rewrite history.
-- Transaction/app_meta/migration_history are managed by MigrationManager.

CREATE TRIGGER IF NOT EXISTS trg_attempt_immutable_update
BEFORE UPDATE ON attempt
FOR EACH ROW
BEGIN
    SELECT RAISE(ABORT, 'ATTEMPT_IMMUTABLE_USE_CORRECTION_EVENT');
END;
