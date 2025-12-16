# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution/project files first to leverage Docker layer caching
COPY shared-csharp/shared-csharp.csproj actions/shared-csharp/
COPY embedding_downloader/embedding_downloader.csproj actions/embedding_downloader/

# Restore
RUN dotnet restore actions/shared-csharp/shared-csharp.csproj
RUN dotnet restore actions/embedding_downloader/embedding_downloader.csproj

# Copy the remaining source
COPY shared-csharp/ actions/shared-csharp/
COPY embedding_downloader/ actions/embedding_downloader/

# Publish (trim optional; remove if you prefer full publish)
RUN dotnet publish actions/embedding_downloader/embedding_downloader.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Default entrypoint
ENTRYPOINT ["dotnet", "embedding_downloader.dll"]