## to test unit
```bash
cd ../tests/unit
```

## to build docker image

### Alternative without helper script
If you prefer to inline the commands (same effect as the script), use:
```bash
docker build \
  -f ai_content_answer_builder.Dockerfile \
  --build-arg EXT_REPO_URL="https://github.com/chesdenis/ask-ai.git" \
  --build-arg EXT_REPO_REF="master" \
  --build-arg EXT_BUILD_CMD="dotnet restore /ext/repo/AskAI.sln && dotnet publish /ext/repo/AskAI/AskAI.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o /ext/out" \
  --build-arg EXT_BIN_SRC="/ext/out/AskAI" \
  --build-arg EXT_BIN_NAME="AskAI" \
  -t ai_content_answer_builder .
```

Notes about RIDs and publish options:
- For Debian/Ubuntu-based images use `linux-x64`. For Alpine use `linux-musl-x64` (not our base here).
- The helper script defaults to: `--self-contained true`, `-p:PublishSingleFile=true`. You can tweak via env vars:
  - `PUBLISH_CONFIGURATION` (Release by default)
  - `SELF_CONTAINED` (true/false)
  - `PUBLISH_SINGLE_FILE` (true/false)
  - `PUBLISH_TRIMMED` (false by default)
  - `EXTRA_PUBLISH_PROPS` for extra MSBuild properties.

## to run docker image
```bash
# Run normally (the .NET app will start with the default entrypoint)
docker run --rm -v .:/in:rw ai_content_answer_builder /in
```

Notes:
- The container uses the default entrypoint: `dotnet ai_content_answer_builder.dll`.
- Any external tool orchestration is handled inside the application code (not via a shell entrypoint).