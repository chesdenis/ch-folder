## to test unit
```bash
cd ../tests/unit
```

## to build docker image
```bash
docker build -f mirror_service.Dockerfile -t mirror_service .
```

## to run docker image
```bash
docker run --rm -v .:/in:rw mirror_service /in
```