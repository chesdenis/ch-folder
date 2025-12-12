## to test unit
```bash
cd ../tests/unit
```

## to build docker image
```bash
docker build -f image_searcher.Dockerfile -t image_searcher .
```

## to run docker image
```bash
docker run --rm --env-file image_searcher/.env image_searcher "красная кофта"
docker run --rm --env-file image_searcher/.env image_searcher "красная кофта" --tags=portrait,night

```