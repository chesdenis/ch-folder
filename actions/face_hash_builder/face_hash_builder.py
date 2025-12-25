import os.path
import sys

from PIL import Image
import numpy as np

import face_recognition
import logging
import json
import base64
from typing import Iterable

logging.basicConfig(
    level=logging.INFO,
    stream=sys.stdout,  # send to console
    format="%(asctime)s %(levelname)s %(name)s: %(message)s",
)

def _split_name(name_wo_ext: str):
    parts = name_wo_ext.split('_') if name_wo_ext else []
    return parts

def from_b64_answer(data):
    base64_encoded_data = data.encode('utf-8')
    decoded_bytes = base64.b64decode(base64_encoded_data)
    decoded_string = decoded_bytes.decode('utf-8')
    python_object = json.loads(decoded_string)
    face_encodings = np.array(python_object["face_encodings"][0])
    return face_encodings

def get_known_faces():
    return [
        {"Name": "DENISC","Encodings": from_b64_answer(os.environ.get('DENISC'))},
        {"Name": "ANNAC","Encodings": from_b64_answer(os.environ.get('ANNAC'))}
    ]

def get_face_vectors(file_path):
    base_dir = os.path.dirname(file_path)
    file_name = os.path.basename(file_path)
    name_wo_ext, _orig_ext = os.path.splitext(file_name)
 
    preview_dir = os.path.join(base_dir, "preview")
    preview_name = f"{name_wo_ext}_p2000.jpg"
    preview_path = os.path.join(preview_dir, preview_name)
    
    known_faces = get_known_faces()

    image = Image.open(preview_path)
    for angle in [90, 180, 270]:
        # Find all face locations and their encodings in the image
        face_locations = face_recognition.face_locations(np.array(image))
        face_encodings = face_recognition.face_encodings(np.array(image), face_locations)

        if len(face_locations) > 0:
            detected_faces = []
            
            print(f"Detected {len(face_encodings)} faces. Looking for known faces:")
            
            for known_face in known_faces:
                known_face_name = known_face["Name"]
                known_face_encodings = known_face["Encodings"]
                
                for face_encoding in face_encodings:
                    results = face_recognition.compare_faces([known_face_encodings], face_encoding)
    
                    if results[0]:
                        print(f"Found known face {known_face_name}")
                        detected_faces.append(known_face_name)
            
            return {
                "detected_faces": detected_faces,
                "rotation":angle,
                "face_locations":face_locations,
                "face_encodings":[encoding.tolist() for encoding in face_encodings]
            }

        image = image.rotate(angle, expand=True)

    logging.info(f'No face detected on image {file_path}')
    return None

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
        
        base_dir = os.path.dirname(path)
        file_name = os.path.basename(path)
        name_without_ext = os.path.splitext(file_name)[0]
        face_vectors_dir = os.path.join(base_dir, "fv")
        os.makedirs(face_vectors_dir, exist_ok=True)
        # Write to file with .face_vectors extension
        output_path = os.path.join(face_vectors_dir, name_without_ext + '.fv.md.answer.md')
        
        try:
            face_vectors = get_face_vectors(path)
            if face_vectors is not None:
                face_vectors_as_json = json.dumps(face_vectors)
                logging.info(f'Serialized face vectors for {path}')
                with open(output_path, 'w') as f: f.write(face_vectors_as_json)
                logging.info(f'Written face vectors to {output_path}')

            processed += 1
        except Exception as e:
            logging.exception(f"Failed processing {path}: {e}")
            errors += 1
            
        logging.info(f"Processed {processed}/{len(targets)}")

    logging.info(f"Done. Processed: {processed}, Errors: {errors}")
    return 0 if errors == 0 else 1

if __name__ == "__main__":
    sys.exit(main(sys.argv))
