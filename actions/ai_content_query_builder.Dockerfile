# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution/project files first to leverage Docker layer caching
COPY shared-csharp/shared-csharp.csproj actions/shared-csharp/
COPY ai_content_query_builder/ai_content_query_builder.csproj actions/ai_content_query_builder/

# Restore
RUN dotnet restore actions/shared-csharp/shared-csharp.csproj
RUN dotnet restore actions/ai_content_query_builder/ai_content_query_builder.csproj

# Copy the remaining source
COPY shared-csharp/ actions/shared-csharp/
COPY ai_content_query_builder/ actions/ai_content_query_builder/

# Publish (trim optional; remove if you prefer full publish)
RUN dotnet publish actions/ai_content_query_builder/ai_content_query_builder.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Default entrypoint
ENTRYPOINT ["dotnet", "ai_content_query_builder.dll"]