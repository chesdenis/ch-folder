# Use slim Python base
FROM python:3.11-slim AS runtime

# Install system libs required by Pillow (JPEG, zlib, etc.)
RUN apt-get update && apt-get install -y --no-install-recommends \
    libjpeg62-turbo libpng16-16 libtiff6 libopenjp2-7 libwebp7 libxcb1 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy only what we need
COPY average_image_marker/average_image_marker.py /app/average_image_marker.py

# Install Python deps
# Pillow and ImageHash are needed by the script
RUN pip install --no-cache-dir pillow imagehash

# Use a non-root user
RUN useradd -m app && chown -R app /app
USER app

# Default command; pass files/dirs as args
ENTRYPOINT ["python", "/app/average_image_marker.py"]