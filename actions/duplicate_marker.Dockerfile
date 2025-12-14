# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution/project files first to leverage Docker layer caching
COPY shared-csharp/shared-csharp.csproj actions/shared-csharp/
COPY duplicate_marker/duplicate_marker.csproj actions/duplicate_marker/

# Restore
RUN dotnet restore actions/shared-csharp/shared-csharp.csproj
RUN dotnet restore actions/duplicate_marker/duplicate_marker.csproj

# Copy the remaining source
COPY shared-csharp/ actions/shared-csharp/
COPY duplicate_marker/ actions/duplicate_marker/

# Publish (trim optional; remove if you prefer full publish)
RUN dotnet publish actions/duplicate_marker/duplicate_marker.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Default entrypoint
ENTRYPOINT ["dotnet", "duplicate_marker.dll"]