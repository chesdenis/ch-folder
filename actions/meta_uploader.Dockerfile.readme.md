## to test unit
```bash
cd ../tests/unit
```

## to build docker image
```bash
docker build -f meta_uploader.Dockerfile -t meta_uploader .
```

## to run docker image
```bash
docker run --rm -v .:/in:rw meta_uploader /in
```