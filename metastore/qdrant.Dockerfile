FROM qdrant/qdrant:latest

# Install curl (try both apk and apt to support different bases)
RUN (apk add --no-cache curl || (apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*))

# Copy init script (idempotent)
COPY init/qdrant/qdrant-init.sh /docker-init/qdrant-init.sh
RUN chmod +x /docker-init/qdrant-init.sh

# Run init via container healthcheck so it executes automatically after Qdrant starts
HEALTHCHECK --interval=5s --timeout=5s --start-period=5s --retries=12 CMD sh /docker-init/qdrant-init.sh || exit 1