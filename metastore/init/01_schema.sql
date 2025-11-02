CREATE TABLE IF NOT EXISTS photo (
    md5_hash text PRIMARY KEY,
    file_name text NOT NULL,
    dir_name text NOT NULL,
    extension text NOT NULL,
    dir_path text NOT NULL,
    file_path text NOT NULL,
    size_bytes bigint NOT NULL CHECK (size_bytes >= 0),
    tags text[] NOT NULL DEFAULT '{}',
    short_details text NOT NULL CHECK (short_details <> ''),
    color_hash text NOT NULL,
    average_hash text NOT NULL,
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

CREATE UNIQUE INDEX IF NOT EXISTS ux_photo_file_path ON photo (file_path);
CREATE INDEX IF NOT EXISTS ix_photo_dir_path ON photo (dir_path);
CREATE INDEX IF NOT EXISTS ix_photo_created_at ON photo (created_at);
CREATE INDEX IF NOT EXISTS ix_photo_tags_gin ON photo USING GIN (tags);
CREATE INDEX IF NOT EXISTS ix_photo_extension ON photo (extension);