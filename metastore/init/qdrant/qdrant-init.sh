#!/usr/bin/env sh
set -e

QD_HOST="${QD_HOST:-localhost}"
QD_PORT="${QD_PORT:-6333}"
COLLECTION="${COLLECTION:-photos}"
VECTOR_SIZE="${VECTOR_SIZE:-1536}"
DISTANCE="${DISTANCE:-Cosine}"

echo "[init] Waiting for Qdrant at http://${QD_HOST}:${QD_PORT}/readyz ..."
until [ "$(curl -s -o /dev/null -w '%{http_code}' http://${QD_HOST}:${QD_PORT}/readyz)" = "200" ]; do
  sleep 2
done
echo "[init] Qdrant is ready"

STATUS=$(curl -s -o /dev/null -w '%{http_code}' http://${QD_HOST}:${QD_PORT}/collections/${COLLECTION})
if [ "$STATUS" != "200" ]; then
  echo "[init] Creating collection '${COLLECTION}' (size=${VECTOR_SIZE}, distance=${DISTANCE})"
  curl -s -X PUT http://${QD_HOST}:${QD_PORT}/collections/${COLLECTION} \
    -H 'Content-Type: application/json' \
    -d "{\"vectors\":{\"size\":${VECTOR_SIZE},\"distance\":\"${DISTANCE}\"}}"
  echo "[init] Collection '${COLLECTION}' ensured"
else
  echo "[init] Collection '${COLLECTION}' already exists"
fi

exit 0
