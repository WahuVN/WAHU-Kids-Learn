-- WAHU Kids Learn — learner-data schema V1
-- SQLite. Static curriculum/questions stay in VERIFIED content packs, not duplicated here.
-- Journal mode is runtime policy and intentionally NOT set in this migration.

PRAGMA foreign_keys = ON;

BEGIN IMMEDIATE;

CREATE TABLE IF NOT EXISTS app_meta (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS migration_history (
    version INTEGER PRIMARY KEY,
    name TEXT NOT NULL,
    checksum_sha256 TEXT NOT NULL,
    applied_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS child (
    id TEXT PRIMARY KEY,
    display_name TEXT NOT NULL,
    grade_level INTEGER NOT NULL DEFAULT 2 CHECK (grade_level BETWEEN 1 AND 12),
    birth_year INTEGER,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS child_setting (
    child_id TEXT PRIMARY KEY,
    settings_json TEXT NOT NULL DEFAULT '{}',
    updated_at_utc TEXT NOT NULL,
    FOREIGN KEY (child_id) REFERENCES child(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS child_skill (
    child_id TEXT NOT NULL,
    skill_id TEXT NOT NULL,
    subject TEXT NOT NULL CHECK (subject IN ('math', 'english')),
    mastery_score REAL NOT NULL DEFAULT 0.0 CHECK (mastery_score BETWEEN 0.0 AND 1.0),
    confidence REAL NOT NULL DEFAULT 0.0 CHECK (confidence BETWEEN 0.0 AND 1.0),
    attempts_count INTEGER NOT NULL DEFAULT 0 CHECK (attempts_count >= 0),
    independent_success_count INTEGER NOT NULL DEFAULT 0 CHECK (independent_success_count >= 0),
    hinted_success_count INTEGER NOT NULL DEFAULT 0 CHECK (hinted_success_count >= 0),
    transfer_success_count INTEGER NOT NULL DEFAULT 0 CHECK (transfer_success_count >= 0),
    last_seen_at_utc TEXT,
    last_success_at_utc TEXT,
    next_review_at_utc TEXT,
    learning_state TEXT NOT NULL DEFAULT 'NEW',
    mastery_engine_version TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    PRIMARY KEY (child_id, skill_id),
    FOREIGN KEY (child_id) REFERENCES child(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS session (
    id TEXT PRIMARY KEY,
    child_id TEXT NOT NULL,
    started_at_utc TEXT NOT NULL,
    ended_at_utc TEXT,
    state TEXT NOT NULL CHECK (state IN ('started', 'active', 'completed', 'aborted', 'recovered')),
    planned_subject TEXT CHECK (planned_subject IS NULL OR planned_subject IN ('math', 'english', 'mixed')),
    performance_profile TEXT CHECK (performance_profile IS NULL OR performance_profile IN ('LOW', 'NORMAL')),
    summary_json TEXT,
    behavior_summary_json TEXT,
    FOREIGN KEY (child_id) REFERENCES child(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS attempt (
    id TEXT PRIMARY KEY,
    session_id TEXT NOT NULL,
    child_id TEXT NOT NULL,
    pack_id TEXT NOT NULL,
    pack_version TEXT NOT NULL,
    question_id TEXT NOT NULL,
    skill_id TEXT NOT NULL,
    subject TEXT NOT NULL CHECK (subject IN ('math', 'english')),
    started_at_utc TEXT NOT NULL,
    answered_at_utc TEXT,
    answer_json TEXT,
    is_correct INTEGER CHECK (is_correct IS NULL OR is_correct IN (0, 1)),
    response_ms INTEGER CHECK (response_ms IS NULL OR response_ms >= 0),
    hint_level INTEGER NOT NULL DEFAULT 0 CHECK (hint_level >= 0),
    representation TEXT,
    input_method TEXT,
    attempt_index INTEGER NOT NULL DEFAULT 1 CHECK (attempt_index >= 1),
    listen_count INTEGER NOT NULL DEFAULT 0 CHECK (listen_count >= 0),
    FOREIGN KEY (session_id) REFERENCES session(id) ON DELETE CASCADE,
    FOREIGN KEY (child_id) REFERENCES child(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS attempt_correction_event (
    id TEXT PRIMARY KEY,
    attempt_id TEXT NOT NULL,
    correction_type TEXT NOT NULL,
    before_json TEXT,
    after_json TEXT,
    reason TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY (attempt_id) REFERENCES attempt(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS error_event (
    id TEXT PRIMARY KEY,
    attempt_id TEXT NOT NULL,
    error_type TEXT NOT NULL,
    confidence REAL NOT NULL CHECK (confidence BETWEEN 0.0 AND 1.0),
    evidence_json TEXT NOT NULL,
    classifier_version TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY (attempt_id) REFERENCES attempt(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS mastery_event (
    id TEXT PRIMARY KEY,
    child_id TEXT NOT NULL,
    skill_id TEXT NOT NULL,
    attempt_id TEXT,
    event_type TEXT NOT NULL,
    delta REAL NOT NULL,
    score_before REAL NOT NULL CHECK (score_before BETWEEN 0.0 AND 1.0),
    score_after REAL NOT NULL CHECK (score_after BETWEEN 0.0 AND 1.0),
    confidence_after REAL CHECK (confidence_after IS NULL OR confidence_after BETWEEN 0.0 AND 1.0),
    reason_json TEXT NOT NULL,
    mastery_engine_version TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY (child_id) REFERENCES child(id) ON DELETE CASCADE,
    FOREIGN KEY (attempt_id) REFERENCES attempt(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS behavior_state_event (
    id TEXT PRIMARY KEY,
    session_id TEXT NOT NULL,
    child_id TEXT NOT NULL,
    attempt_id TEXT,
    state_label TEXT NOT NULL CHECK (state_label IN (
        'READY', 'FLOW_LIKELY', 'BORED_OR_UNDERCHALLENGED',
        'STRAINED', 'FRUSTRATED_LIKELY', 'FATIGUED_LIKELY')),
    confidence REAL NOT NULL CHECK (confidence BETWEEN 0.0 AND 1.0),
    evidence_json TEXT NOT NULL,
    action_taken TEXT,
    controller_version TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY (session_id) REFERENCES session(id) ON DELETE CASCADE,
    FOREIGN KEY (child_id) REFERENCES child(id) ON DELETE CASCADE,
    FOREIGN KEY (attempt_id) REFERENCES attempt(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS adaptive_decision_event (
    id TEXT PRIMARY KEY,
    session_id TEXT NOT NULL,
    child_id TEXT NOT NULL,
    chosen_pack_id TEXT NOT NULL,
    chosen_pack_version TEXT NOT NULL,
    chosen_question_id TEXT NOT NULL,
    primary_skill_id TEXT NOT NULL,
    behavior_state TEXT,
    behavior_confidence REAL CHECK (behavior_confidence IS NULL OR behavior_confidence BETWEEN 0.0 AND 1.0),
    difficulty_fit REAL,
    decision_reason_json TEXT NOT NULL,
    candidate_summary_json TEXT,
    engine_version TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY (session_id) REFERENCES session(id) ON DELETE CASCADE,
    FOREIGN KEY (child_id) REFERENCES child(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS review_schedule (
    child_id TEXT NOT NULL,
    skill_id TEXT NOT NULL,
    due_at_utc TEXT NOT NULL,
    interval_days REAL NOT NULL DEFAULT 0 CHECK (interval_days >= 0),
    reason TEXT NOT NULL,
    scheduler_version TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    PRIMARY KEY (child_id, skill_id),
    FOREIGN KEY (child_id) REFERENCES child(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS reward_event (
    id TEXT PRIMARY KEY,
    child_id TEXT NOT NULL,
    reward_type TEXT NOT NULL,
    reward_id TEXT NOT NULL,
    source_event TEXT NOT NULL,
    source_ref TEXT NOT NULL,
    source_key TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    FOREIGN KEY (child_id) REFERENCES child(id) ON DELETE CASCADE,
    UNIQUE (child_id, source_key)
);

CREATE TABLE IF NOT EXISTS inventory (
    child_id TEXT NOT NULL,
    item_id TEXT NOT NULL,
    unlocked_at_utc TEXT NOT NULL,
    equipped INTEGER NOT NULL DEFAULT 0 CHECK (equipped IN (0, 1)),
    PRIMARY KEY (child_id, item_id),
    FOREIGN KEY (child_id) REFERENCES child(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS parent_note (
    id TEXT PRIMARY KEY,
    child_id TEXT NOT NULL,
    note TEXT NOT NULL,
    tags_json TEXT,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL,
    FOREIGN KEY (child_id) REFERENCES child(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS content_pack_state (
    pack_id TEXT NOT NULL,
    version TEXT NOT NULL,
    subject TEXT NOT NULL CHECK (subject IN ('math', 'english', 'mixed')),
    grade INTEGER NOT NULL DEFAULT 2,
    install_source TEXT NOT NULL CHECK (install_source IN ('builtin', 'parent_import', 'portable')),
    status TEXT NOT NULL CHECK (status IN ('VERIFIED', 'DISABLED', 'QUARANTINED', 'RETIRED')),
    active INTEGER NOT NULL DEFAULT 0 CHECK (active IN (0, 1)),
    manifest_sha256 TEXT NOT NULL,
    relative_path TEXT NOT NULL,
    installed_at_utc TEXT NOT NULL,
    last_verified_at_utc TEXT NOT NULL,
    PRIMARY KEY (pack_id, version)
);

CREATE TABLE IF NOT EXISTS backup_history (
    id TEXT PRIMARY KEY,
    backup_type TEXT NOT NULL CHECK (backup_type IN ('automatic', 'weekly', 'manual', 'pre_migration')),
    relative_or_external_path TEXT NOT NULL,
    sha256 TEXT NOT NULL,
    app_version TEXT NOT NULL,
    schema_version INTEGER NOT NULL,
    content_manifest_json TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    verified INTEGER NOT NULL DEFAULT 0 CHECK (verified IN (0, 1))
);

CREATE INDEX IF NOT EXISTS idx_child_skill_due
    ON child_skill(child_id, next_review_at_utc);

CREATE INDEX IF NOT EXISTS idx_attempt_session_time
    ON attempt(session_id, started_at_utc);

CREATE INDEX IF NOT EXISTS idx_attempt_child_skill_time
    ON attempt(child_id, skill_id, started_at_utc);

CREATE INDEX IF NOT EXISTS idx_error_attempt
    ON error_event(attempt_id);

CREATE INDEX IF NOT EXISTS idx_mastery_child_skill_time
    ON mastery_event(child_id, skill_id, created_at_utc);

CREATE INDEX IF NOT EXISTS idx_behavior_session_time
    ON behavior_state_event(session_id, created_at_utc);

CREATE INDEX IF NOT EXISTS idx_adaptive_child_time
    ON adaptive_decision_event(child_id, created_at_utc);

CREATE INDEX IF NOT EXISTS idx_review_due
    ON review_schedule(child_id, due_at_utc);

CREATE INDEX IF NOT EXISTS idx_reward_child_time
    ON reward_event(child_id, created_at_utc);

CREATE UNIQUE INDEX IF NOT EXISTS idx_one_active_pack_version
    ON content_pack_state(pack_id)
    WHERE active = 1;

INSERT OR IGNORE INTO app_meta(key, value, updated_at_utc)
VALUES ('schema_version', '1', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));

COMMIT;
