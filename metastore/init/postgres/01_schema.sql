CREATE TABLE IF NOT EXISTS photo (
    md5_hash text PRIMARY KEY,
    extension text NOT NULL,
    size_bytes bigint NOT NULL CHECK (size_bytes >= 0),
    tags text[] NOT NULL DEFAULT '{}',
    short_details text NOT NULL CHECK (short_details <> ''),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
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
CREATE INDEX IF NOT EXISTS ix_photo_extension ON photo (extension);

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