CREATE TABLE IF NOT EXISTS photo (
    md5_hash text PRIMARY KEY,
    extension text NOT NULL,
    size_bytes bigint NOT NULL CHECK (size_bytes >= 0),
    tags text[] NOT NULL DEFAULT '{}',
    persons text[] NOT NULL DEFAULT '{}',
    short_details text NOT NULL CHECK (short_details <> ''),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    commerce_rate integer NOT NULL DEFAULT 0 CHECK (commerce_rate >= 0 AND commerce_rate <= 5)
    );

CREATE OR REPLACE FUNCTION set_updated_at()
   RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
     IF NEW IS DISTINCT FROM OLD THEN
       NEW.updated_at := now();
END IF;
RETURN NEW;
END$$;

DROP TRIGGER IF EXISTS trg_photo_updated_at ON photo;
CREATE TRIGGER trg_photo_updated_at
    BEFORE UPDATE ON photo
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE INDEX IF NOT EXISTS ix_photo_created_at ON photo (created_at);
CREATE INDEX IF NOT EXISTS ix_photo_tags_gin ON photo USING GIN (tags);
CREATE INDEX IF NOT EXISTS ix_photo_persons_gin ON photo USING GIN (persons);
CREATE INDEX IF NOT EXISTS ix_photo_extension ON photo (extension);
CREATE INDEX IF NOT EXISTS ix_photo_commerce_rate ON photo (commerce_rate DESC);

-- =============================================================
-- Search sessions storage to persist vector search results
-- so MVC app can reuse them without re-running embeddings/search
-- =============================================================

CREATE TABLE IF NOT EXISTS search_session (
    id uuid PRIMARY KEY,
    created_at timestamptz NOT NULL DEFAULT now(),
    query_text text NOT NULL,
    embedding_model text NOT NULL,
    embedding_dim integer NOT NULL CHECK (embedding_dim > 0),
    embedding_hash text NULL,
    filter_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    collection_name text NOT NULL,
    limit_requested integer NOT NULL CHECK (limit_requested > 0),
    score_threshold real NULL,
    result_count integer NOT NULL DEFAULT 0 CHECK (result_count >= 0),
    expires_at timestamptz NULL
);

CREATE INDEX IF NOT EXISTS ix_search_session_created_at ON search_session (created_at);
CREATE INDEX IF NOT EXISTS ix_search_session_expires_at ON search_session (expires_at);
CREATE INDEX IF NOT EXISTS ix_search_session_filter_gin ON search_session USING GIN (filter_json);

CREATE TABLE IF NOT EXISTS search_session_result (
    session_id uuid NOT NULL,
    rank integer NOT NULL CHECK (rank >= 0),
    point_id text NOT NULL,
    score real NOT NULL,
    path_md5 text NULL,
    payload_snapshot jsonb NULL,
    PRIMARY KEY (session_id, rank),
    FOREIGN KEY (session_id) REFERENCES search_session(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_search_session_result_session ON search_session_result (session_id);
CREATE INDEX IF NOT EXISTS ix_search_session_result_point ON search_session_result (point_id);

-- =============================================================
-- Selected results per search session
-- Stores the list of user-selected photo md5 hashes for a session
-- Connected to search_session_result via shared field `session_id`
-- =============================================================

CREATE TABLE IF NOT EXISTS search_session_selected (
    session_id uuid NOT NULL,
    md5_hash text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (session_id, md5_hash),
    FOREIGN KEY (session_id) REFERENCES search_session(id) ON DELETE RESTRICT,
    FOREIGN KEY (md5_hash) REFERENCES photo(md5_hash) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_search_session_selected_session ON search_session_selected (session_id);
CREATE INDEX IF NOT EXISTS ix_search_session_selected_md5 ON search_session_selected (md5_hash);

-- =============================================================
-- OpenAI Embedding cache table (used by image_searcher)
-- Stores full JSON responses keyed by (model, input_hash)
-- =============================================================

CREATE TABLE IF NOT EXISTS embedding_cache (
    model text NOT NULL,
    input_hash text NOT NULL,
    response_json jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NULL,
    PRIMARY KEY (model, input_hash)
);

CREATE INDEX IF NOT EXISTS ix_embedding_cache_expires_at ON embedding_cache (expires_at);
CREATE INDEX IF NOT EXISTS ix_embedding_cache_created_at ON embedding_cache (created_at);

-- =============================================================
-- Image physical location table (updated by webapp on startup)
-- Stores mapping from md5 hash to real file system path
-- =============================================================

CREATE TABLE IF NOT EXISTS image_location (
    md5_hash text PRIMARY KEY,
    real_path text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TRIGGER trg_image_location_updated_at
    BEFORE UPDATE ON image_location
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

CREATE INDEX IF NOT EXISTS ix_image_location_created_at ON image_location (created_at);

-- =============================================================
-- Content validation results
-- Stores per-folder validation status for different test kinds
-- =============================================================

CREATE TABLE IF NOT EXISTS content_validation_result (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id uuid NOT NULL,
    folder text NOT NULL,
    test_kind text NOT NULL,
    status text NOT NULL CHECK (status IN ('Passed','Failed','Error','Running')),
    details jsonb NULL,
    started_at timestamptz NOT NULL DEFAULT now(),
    finished_at timestamptz NULL,
    UNIQUE (job_id, folder, test_kind)
);

CREATE INDEX IF NOT EXISTS ix_content_validation_result_job ON content_validation_result (job_id);
CREATE INDEX IF NOT EXISTS ix_content_validation_result_folder ON content_validation_result (folder);
CREATE INDEX IF NOT EXISTS ix_content_validation_result_kind ON content_validation_result (test_kind);