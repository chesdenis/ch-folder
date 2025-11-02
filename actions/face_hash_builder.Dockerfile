# Use slim Python base
FROM python:3.11-slim AS runtime

# Install system libs required by Pillow and face_recognition (dlib)
# face_recognition needs cmake, build tools, and dlib dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    libjpeg62-turbo libpng16-16 libtiff6 libopenjp2-7 libwebp7 libxcb1 \
    cmake build-essential \
    && pip install --no-cache-dir pillow numpy face_recognition \
    && apt-get purge -y --auto-remove cmake build-essential \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy only what we need
COPY face_hash_builder/face_hash_builder.py /app/face_hash_builder.py

# Install Python deps
# Pillow, numpy, and face_recognition are needed by the script
RUN pip install --no-cache-dir pillow numpy face_recognition

# Use a non-root user
RUN useradd -m app && chown -R app /app
USER app

# Default command; pass files/dirs as args
ENTRYPOINT ["python", "/app/face_hash_builder.py"]