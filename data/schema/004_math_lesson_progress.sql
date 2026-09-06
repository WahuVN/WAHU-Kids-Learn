-- WAHU Kids Learn — migration V4
-- Purpose:
-- 1) persist Math session mode + targeted lesson for exact resume;
-- 2) persist lesson-level started/completed/score progress without conflating navigation with mastery.
-- Transaction/app_meta/migration_history are managed by MigrationManager.

ALTER TABLE math_session_runtime
ADD COLUMN session_mode TEXT NOT NULL DEFAULT 'adaptive'
CHECK (session_mode IN ('adaptive','lesson'));

ALTER TABLE math_session_runtime
ADD COLUMN target_lesson_id TEXT;

CREATE TABLE IF NOT EXISTS math_lesson_progress (
    child_id TEXT NOT NULL,
    lesson_id TEXT NOT NULL,
    skill_id TEXT NOT NULL,
    started_count INTEGER NOT NULL DEFAULT 0 CHECK (started_count >= 0),
    completed_count INTEGER NOT NULL DEFAULT 0 CHECK (completed_count >= 0),
    last_score_percent REAL CHECK (last_score_percent IS NULL OR (last_score_percent >= 0 AND last_score_percent <= 100)),
    best_score_percent REAL CHECK (best_score_percent IS NULL OR (best_score_percent >= 0 AND best_score_percent <= 100)),
    last_started_at_utc TEXT,
    last_completed_at_utc TEXT,
    updated_at_utc TEXT NOT NULL,
    PRIMARY KEY (child_id, lesson_id),
    FOREIGN KEY (child_id) REFERENCES child(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_math_lesson_progress_child_completed
    ON math_lesson_progress(child_id, completed_count, updated_at_utc);
