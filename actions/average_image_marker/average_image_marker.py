import os.path
import sys
from PIL import Image
import imagehash
import logging
import re
from typing import Iterable

logging.basicConfig(
    level=logging.INFO,
    stream=sys.stdout,  # send to console
    format="%(asctime)s %(levelname)s %(name)s: %(message)s",
)

_MD5_RE = re.compile(r"^[a-fA-F0-9]{32}$")

def _split_name(name_wo_ext: str):
    parts = name_wo_ext.split('_') if name_wo_ext else []
    return parts

def _has_trailing_md5(parts: list[str]) -> bool:
    return bool(parts) and _MD5_RE.fullmatch(parts[-1] or "")


def mark_color_and_average_hash(file_path):
    try:
        base_dir = os.path.dirname(file_path)
        file_name = os.path.basename(file_path)
        name_wo_ext, _orig_ext = os.path.splitext(file_name)
        
        parts = _split_name(name_wo_ext)
    
        preview_dir = os.path.join(base_dir, "preview")
        preview_name = f"{name_wo_ext}_p512.jpg"
        preview_path = os.path.join(preview_dir, preview_name)
        
        if not os.path.exists(preview_path):
            logging.info(f"Preview is missing for: {file_path}. Skipping")
            return 

        with Image.open(preview_path) as img:
            computed_colorhash = str(imagehash.colorhash(img))
            computed_average_hash = str(imagehash.average_hash(img))

        # Extract MD5 if present (last part)
        md5_hash = parts[-1] if _has_trailing_md5(parts) else None

        new_parts = [computed_colorhash, computed_average_hash]
        if md5_hash:
            new_parts.append(md5_hash)

        expected_name = "_".join(new_parts)
        if name_wo_ext == expected_name:
            logging.info(f"Filename already correct for: {file_path}")
            return

        # Rename file
        new_path = os.path.join(base_dir, expected_name + _orig_ext)
        if new_path != file_path:
            os.rename(file_path, new_path)
            logging.info(f"Renamed: {file_path} -> {new_path}")
        else:
            logging.info(f"No changes required for: {file_path}")
    except Exception as e:
        print(e)

IMAGE_EXTENSIONS = {".jpg", ".jpeg", ".nef", ".heic"}

def is_image_file(path: str) -> bool:
    _, ext = os.path.splitext(path)
    return ext.lower() in IMAGE_EXTENSIONS

def iter_target_files(paths: Iterable[str]) -> Iterable[str]:
    for p in paths:
        if not os.path.exists(p):
            logging.warning(f"Path not found: {p}")
            continue
        if os.path.isfile(p):
            if is_image_file(p):
                yield os.path.abspath(p)
            else:
                logging.debug(f"Skipping non-image file: {p}")
        else:
            # directory: walk recursively
            try:
                for fname in os.listdir(p):
                    fpath = os.path.join(p, fname)
                    if os.path.isfile(fpath):
                        if is_image_file(fpath):
                            yield os.path.abspath(fpath)
                        else:
                            logging.debug(f"Skipping non-image file: {fpath}")
            except Exception as e:
                logging.exception(f"Failed listing {p}: {e}")


def main(argv:list[str]) -> int:
    if len(argv) < 2:
        print("Usage: python cli.py <file-or-folder> [more ...]")
        return 2

    targets = list(iter_target_files(argv[1:]))

    if not targets:
        logging.info("No image files to process.")
        return 0

    processed = 0
    errors = 0

    for path in targets:
        try:
            mark_color_and_average_hash(path)
            processed += 1
        except Exception as e:
            logging.exception(f"Failed processing {path}: {e}")
            errors += 1

    logging.info(f"Done. Processed: {processed}, Errors: {errors}")
    return 0 if errors == 0 else 1

if __name__ == "__main__":
    sys.exit(main(sys.argv))
