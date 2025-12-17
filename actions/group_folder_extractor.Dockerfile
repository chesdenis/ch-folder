# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution/project files first to leverage Docker layer caching
COPY shared-csharp/shared-csharp.csproj actions/shared-csharp/
COPY group_folder_extractor/group_folder_extractor.csproj actions/group_folder_extractor/

# Restore
RUN dotnet restore actions/shared-csharp/shared-csharp.csproj
RUN dotnet restore actions/group_folder_extractor/group_folder_extractor.csproj

# Copy the remaining source
COPY shared-csharp/ actions/shared-csharp/
COPY group_folder_extractor/ actions/group_folder_extractor/

# Publish (trim optional; remove if you prefer full publish)
RUN dotnet publish actions/group_folder_extractor/group_folder_extractor.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Default entrypoint
ENTRYPOINT ["dotnet", "group_folder_extractor.dll"]