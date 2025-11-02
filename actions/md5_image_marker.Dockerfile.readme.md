## to test unit
```bash
cd ../tests/unit
dotnet restore actions/shared-csharp.tests/shared-csharp.tests.csproj
dotnet restore actions/md5_image_marker.tests/md5_image_marker.tests.csproj
dotnet test actions/shared-csharp.tests/shared-csharp.tests.csproj
dotnet test actions/md5_image_marker.tests/md5_image_marker.tests.csproj
```

## to build docker image
```bash
docker build -f md5_image_marker.Dockerfile -t md5_image_marker .
```

## to run docker image
```bash
docker run --rm -v .:/in:rw md5_image_marker /in
```