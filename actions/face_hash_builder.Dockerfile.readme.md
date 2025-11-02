## to test unit
```bash
```

## to build docker image
```bash
docker build -f face_hash_builder.Dockerfile -t face_hash_builder .
```

## to run docker image
```bash
docker run --rm -v .:/in:rw face_hash_builder /in
```