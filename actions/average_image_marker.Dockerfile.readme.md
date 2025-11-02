## to test unit
```bash
```

## to build docker image
```bash
docker build -f average_image_marker.Dockerfile -t average_image_marker .
```

## to run docker image
```bash
docker run --rm -v .:/in:rw average_image_marker /in
```