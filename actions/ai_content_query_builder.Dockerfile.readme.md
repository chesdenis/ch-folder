## to test unit
```bash
cd ../tests/unit
```

## to build docker image
```bash
docker build -f ai_content_query_builder.Dockerfile -t ai_content_query_builder .
```

## to run docker image
```bash
docker run --rm -v .:/in:rw ai_content_query_builder /in
```