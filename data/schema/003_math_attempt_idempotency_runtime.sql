-- WAHU Kids Learn — migration V3
-- Purpose:
-- 1) durable semantic idempotency for answer commits without rewriting legacy immutable attempts;
-- 2) durable Math session runtime state used by suspend/resume.
-- Transaction/app_meta/migration_history are managed by MigrationManager.

CREATE TABLE IF NOT EXISTS attempt_commit_key (
    session_id TEXT NOT NULL,
    question_id TEXT NOT NULL,
    attempt_index INTEGER NOT NULL CHECK (attempt_index >= 1),
    attempt_id TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    PRIMARY KEY (session_id, question_id, attempt_index),
    UNIQUE (attempt_id),
    FOREIGN KEY (session_id) REFERENCES session(id) ON DELETE CASCADE,
    FOREIGN KEY (attempt_id) REFERENCES attempt(id) ON DELETE CASCADE
);

-- Preserve every historical attempt. If an old runtime accidentally wrote duplicates for the
-- same semantic key, pin the key to the earliest committed attempt instead of deleting history.
INSERT OR IGNORE INTO attempt_commit_key(session_id, question_id, attempt_index, attempt_id, created_at_utc)
SELECT a.session_id, a.question_id, a.attempt_index, a.id, a.answered_at_utc
FROM attempt a
WHERE a.id = (
    SELECT a2.id
    FROM attempt a2
    WHERE a2.session_id = a.session_id
      AND a2.question_id = a.question_id
      AND a2.attempt_index = a.attempt_index
    ORDER BY a2.answered_at_utc ASC, a2.id ASC
    LIMIT 1
);

CREATE INDEX IF NOT EXISTS idx_attempt_commit_key_attempt
    ON attempt_commit_key(attempt_id);

CREATE TABLE IF NOT EXISTS math_session_runtime (
    session_id TEXT PRIMARY KEY,
    seed INTEGER NOT NULL,
    target_question_count INTEGER NOT NULL CHECK (target_question_count BETWEEN 1 AND 40),
    generated_question_count INTEGER NOT NULL DEFAULT 0 CHECK (generated_question_count >= 0),
    current_question_json TEXT,
    current_selection_json TEXT,
    question_started_at_utc TEXT,
    forced_repair_template_id TEXT,
    updated_at_utc TEXT NOT NULL,
    FOREIGN KEY (session_id) REFERENCES session(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_math_session_runtime_updated
    ON math_session_runtime(updated_at_utc);
