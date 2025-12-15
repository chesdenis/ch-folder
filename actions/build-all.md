docker build -f duplicate_marker.Dockerfile -t duplicate_marker .
docker build -f face_hash_builder.Dockerfile -t face_hash_builder .
docker build -f image_searcher.Dockerfile -t image_searcher .
docker build -f md5_image_marker.Dockerfile -t md5_image_marker .
docker build -f meta_uploader.Dockerfile -t meta_uploader .
docker build -f average_image_marker.Dockerfile -t average_image_marker .
docker build -f ai_content_query_builder.Dockerfile -t ai_content_query_builder .
docker build \
-f ai_content_answer_builder.Dockerfile \
--build-arg EXT_REPO_URL="https://github.com/chesdenis/ask-ai.git" \
--build-arg EXT_REPO_REF="master" \
--build-arg EXT_BUILD_CMD="dotnet restore /ext/repo/AskAI.sln && dotnet publish /ext/repo/AskAI/AskAI.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o /ext/out" \
--build-arg EXT_BIN_SRC="/ext/out/AskAI" \
--build-arg EXT_BIN_NAME="AskAI" \
-t ai_content_answer_builder .