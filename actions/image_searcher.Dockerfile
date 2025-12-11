# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution/project files first to leverage Docker layer caching
COPY shared-csharp/shared-csharp.csproj actions/shared-csharp/
COPY image_searcher/image_searcher.csproj actions/image_searcher/

# Restore
RUN dotnet restore actions/shared-csharp/shared-csharp.csproj
RUN dotnet restore actions/image_searcher/image_searcher.csproj

# Copy the remaining source
COPY shared-csharp/ actions/shared-csharp/
COPY image_searcher/ actions/image_searcher/

# Publish (trim optional; remove if you prefer full publish)
RUN dotnet publish actions/image_searcher/image_searcher.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Default entrypoint
ENTRYPOINT ["dotnet", "image_searcher.dll"]