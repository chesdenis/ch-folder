## to test unit
```bash
cd ../tests/unit
```

## to build docker image
```bash
docker build -f duplicate_marker.Dockerfile -t duplicate_marker .
```

## to run docker image
```bash
docker run --rm -v .:/in:rw duplicate_marker /in
```