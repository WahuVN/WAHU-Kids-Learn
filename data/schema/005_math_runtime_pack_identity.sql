-- WAHU Kids Learn — migration V5
-- Purpose:
-- 1) pin every resumable Math runtime to the exact content-pack identity it started with;
-- 2) backfill legacy runtimes only when committed Math attempts prove one unambiguous, non-empty pack identity;
-- 3) enforce the runtime pack identity as either NULL/NULL (legacy-unbound) or a complete non-empty pair.
-- Transaction/app_meta/migration_history are managed by MigrationManager.

ALTER TABLE math_session_runtime
ADD COLUMN pack_id TEXT;

ALTER TABLE math_session_runtime
ADD COLUMN pack_version TEXT;

-- Backfill only sessions whose committed Math attempts all use the same non-empty identity.
-- Zero-attempt runtimes, blank historical identities, and already-mixed histories stay NULL/NULL;
-- the runtime service will bind a safe legacy session or quarantine an incompatible/corrupt one.
UPDATE math_session_runtime
SET pack_id = (
        SELECT MIN(a.pack_id)
        FROM attempt a
        WHERE a.session_id = math_session_runtime.session_id
          AND a.subject = 'math'
          AND a.answered_at_utc IS NOT NULL
    ),
    pack_version = (
        SELECT MIN(a.pack_version)
        FROM attempt a
        WHERE a.session_id = math_session_runtime.session_id
          AND a.subject = 'math'
          AND a.answered_at_utc IS NOT NULL
    )
WHERE session_id IN (
    SELECT a.session_id
    FROM attempt a
    WHERE a.subject = 'math'
      AND a.answered_at_utc IS NOT NULL
    GROUP BY a.session_id
    HAVING COUNT(*) > 0
       AND MIN(a.pack_id) = MAX(a.pack_id)
       AND MIN(a.pack_version) = MAX(a.pack_version)
       AND MIN(length(trim(a.pack_id))) > 0
       AND MIN(length(trim(a.pack_version))) > 0
);

CREATE TRIGGER IF NOT EXISTS trg_math_runtime_pack_identity_insert
BEFORE INSERT ON math_session_runtime
WHEN ((NEW.pack_id IS NULL) <> (NEW.pack_version IS NULL))
  OR (NEW.pack_id IS NOT NULL AND (
      length(trim(NEW.pack_id)) = 0 OR length(trim(NEW.pack_version)) = 0
  ))
BEGIN
    SELECT RAISE(ABORT, 'math runtime pack identity must be a complete non-empty pair');
END;

CREATE TRIGGER IF NOT EXISTS trg_math_runtime_pack_identity_update
BEFORE UPDATE OF pack_id, pack_version ON math_session_runtime
WHEN ((NEW.pack_id IS NULL) <> (NEW.pack_version IS NULL))
  OR (NEW.pack_id IS NOT NULL AND (
      length(trim(NEW.pack_id)) = 0 OR length(trim(NEW.pack_version)) = 0
  ))
BEGIN
    SELECT RAISE(ABORT, 'math runtime pack identity must be a complete non-empty pair');
END;

CREATE INDEX IF NOT EXISTS idx_math_session_runtime_pack_identity
    ON math_session_runtime(pack_id, pack_version);
