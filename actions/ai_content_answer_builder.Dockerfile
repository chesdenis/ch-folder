# Build stage
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution/project files first to leverage Docker layer caching
COPY shared-csharp/shared-csharp.csproj actions/shared-csharp/
COPY ai_content_answer_builder/ai_content_answer_builder.csproj actions/ai_content_answer_builder/
COPY scripts/ /scripts/

# Restore
RUN dotnet restore actions/shared-csharp/shared-csharp.csproj
RUN dotnet restore actions/ai_content_answer_builder/ai_content_answer_builder.csproj

# Copy the remaining source
COPY shared-csharp/ actions/shared-csharp/
COPY ai_content_answer_builder/ actions/ai_content_answer_builder/

# Publish (trim optional; remove if you prefer full publish)
RUN dotnet publish actions/ai_content_answer_builder/ai_content_answer_builder.csproj -c Release -o /app/publish

# ------------------------------------------------------------
# Optional: Pull and build external application from GitHub
# Controlled by build args; if EXT_REPO_URL is empty, this stage does nothing
# ------------------------------------------------------------
ARG EXT_REPO_URL=""
ARG EXT_REPO_REF=""
ARG EXT_SUBDIR=""
ARG EXT_BUILD_CMD=""
ARG EXT_BIN_SRC=""
ARG EXT_BIN_NAME="external_app"

# Always install git in build stage (we rely on git commands during build when configured)
RUN apt-get update \
    && apt-get install -y --no-install-recommends git ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Always create output folder for potential external binary to allow unconditional COPY later
RUN mkdir -p /app/ext

# Clone/build only when a repo URL is provided
RUN bash -lc 'set -euo pipefail; \
    if [ -n "${EXT_REPO_URL}" ]; then \
      mkdir -p /ext && cd /ext; \
      git clone "${EXT_REPO_URL}" repo; \
      cd repo; \
      if [ -n "${EXT_REPO_REF}" ]; then git checkout "${EXT_REPO_REF}"; fi; \
      if [ -n "${EXT_SUBDIR}" ]; then cd "${EXT_SUBDIR}"; fi; \
      if [ -n "${EXT_BUILD_CMD}" ]; then bash -lc "${EXT_BUILD_CMD}"; fi; \
      if [ -n "${EXT_BIN_SRC}" ]; then \
        cp -R ${EXT_BIN_SRC} /app/ext/${EXT_BIN_NAME}; \
        chmod +x /app/ext/${EXT_BIN_NAME} || true; \
      fi; \
    fi'

# Runtime stage
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Copy external binary if it exists
COPY --from=build /app/ext/ /usr/local/bin/

# Ensure git is available in the runtime image for runtime `git pull` usage
RUN apt-get update \
    && apt-get install -y --no-install-recommends git ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Default entrypoint — the application code will handle running any external process
ENTRYPOINT ["dotnet", "ai_content_answer_builder.dll"]