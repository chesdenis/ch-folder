# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution/project files first to leverage Docker layer caching
COPY shared-csharp/shared-csharp.csproj actions/shared-csharp/
COPY meta_uploader/meta_uploader.csproj actions/meta_uploader/

# Restore
RUN dotnet restore actions/shared-csharp/shared-csharp.csproj
RUN dotnet restore actions/meta_uploader/meta_uploader.csproj

# Copy the remaining source
COPY shared-csharp/ actions/shared-csharp/
COPY meta_uploader/ actions/meta_uploader/

# Publish (trim optional; remove if you prefer full publish)
RUN dotnet publish actions/meta_uploader/meta_uploader.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Default entrypoint
ENTRYPOINT ["dotnet", "meta_uploader.dll"]