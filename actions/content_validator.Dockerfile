# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution/project files first to leverage Docker layer caching
COPY shared-csharp/shared-csharp.csproj actions/shared-csharp/
COPY content_validator/content_validator.csproj actions/content_validator/

# Restore
RUN dotnet restore actions/shared-csharp/shared-csharp.csproj
RUN dotnet restore actions/content_validator/content_validator.csproj

# Copy the remaining source
COPY shared-csharp/ actions/shared-csharp/
COPY content_validator/ actions/content_validator/

# Publish
RUN dotnet publish actions/content_validator/content_validator.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Default entrypoint
ENTRYPOINT ["dotnet", "content_validator.dll"]
