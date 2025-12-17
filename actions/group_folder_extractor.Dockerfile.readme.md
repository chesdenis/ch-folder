## to test unit
```bash
cd ../tests/unit
```

## to build docker image
```bash
docker build -f group_folder_extractor.Dockerfile -t group_folder_extractor .
```

## to run docker image
```bash
docker run --rm --env-file group_folder_extractor/.env group_folder_extractor 
```