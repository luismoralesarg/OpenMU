#!/usr/bin/env bash
# Redespliega mu-api y mu-web con el código más reciente: pull de los 3
# repos, reconstruye solo mu-api/mu-web (openmu-startup usa la imagen
# prebuilt munique/openmu, no hay nada que reconstruir ahí), aplica las
# migraciones de mu-api contra el Postgres de Docker, y reinicia los
# contenedores con las imágenes nuevas.
#
# Asume el mismo layout de carpetas hermanas que ya da por sentado
# docker-compose.yml (contexts ../../../mu-api y ../../../mu-web):
#   <root>/OpenMU/deploy/all-in-one   <- este script
#   <root>/mu-api
#   <root>/mu-web
#
# Uso (desde cualquier lado, aunque lo más natural es pararse en esta
# carpeta):
#   ./deploy.sh
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
ROOT_DIR=$(cd "$SCRIPT_DIR/../../.." && pwd)

OPENMU_DIR="$ROOT_DIR/OpenMU"
MU_API_DIR="$ROOT_DIR/mu-api"
MU_WEB_DIR="$ROOT_DIR/mu-web"

compose() {
    (cd "$SCRIPT_DIR" && docker compose -f docker-compose.yml -f docker-compose.prod.yml "$@")
}

for dir in "$OPENMU_DIR" "$MU_API_DIR" "$MU_WEB_DIR"; do
    if [ ! -d "$dir" ]; then
        echo "error: no encuentro '$dir' - este script asume que OpenMU, mu-api y mu-web son carpetas hermanas" >&2
        exit 1
    fi
done

echo "== 1/4: git pull =="
for dir in "$OPENMU_DIR" "$MU_API_DIR" "$MU_WEB_DIR"; do
    echo "-> $dir"
    git -C "$dir" pull --ff-only
done

echo "== 2/4: reconstruyendo imágenes de mu-api y mu-web =="
compose build mu-api mu-web

echo "== 3/4: aplicando migraciones de mu-api =="
"$MU_API_DIR/scripts/run-migrations.sh"

echo "== 4/4: reiniciando contenedores =="
compose up -d mu-api mu-web

echo
echo "Listo. Verificá con:"
echo "  docker compose -f docker-compose.yml -f docker-compose.prod.yml logs -f mu-api"
echo "  curl -s https://api.mupalmira.com.ar/healthz"
echo "  curl -s https://api.mupalmira.com.ar/public/client-manifest"
