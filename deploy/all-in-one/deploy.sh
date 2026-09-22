#!/usr/bin/env bash
# Redespliega todo lo que puede haber cambiado: pull de los 3 repos,
# reconstruye mu-api/mu-web/openmu-startup, recompila el plugin
# PlugIns.MuApiBridge de OpenMU (el .dll que va en plugins/), aplica las
# migraciones de mu-api contra el Postgres de Docker, y reinicia los
# contenedores cuya imagen haya cambiado de verdad.
#
# openmu-startup se buildea siempre desde nuestro propio fork
# (docker-compose.prod.yml apunta su "build" a src/Startup/Dockerfile en
# vez de usar la imagen prebuilt munique/openmu) - así los fixes al core
# del server (no solo al plugin) quedan activos. El build usa la cache de
# capas de Docker como siempre, así que cuando no cambió nada relevante
# es prácticamente instantáneo. "compose up -d" ya sabe no reiniciar un
# contenedor cuya imagen no cambió, así que no hace falta un chequeo
# manual para este caso (a diferencia del plugin, ver más abajo).
#
# El paso del plugin importa tanto como los otros: es un .dll aparte
# que PlugInManager carga una sola vez al arrancar openmu-startup - un
# cambio en src/PlugIns.MuApiBridge/*.cs que no pasa por este paso queda
# pisado en silencio (el contenedor sigue corriendo el .dll viejo para
# siempre, sin ningún error visible) hasta el día que alguien se acuerde
# de recompilarlo a mano - así se nos escapó el aviso de joyas recogidas
# la primera vez. Como el plugin se monta aparte (no es parte de la
# imagen), "compose up -d" no detecta por sí solo si cambió, así que acá
# sí seguimos comparando el .dll a mano y forzando un restart si hace falta.
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
PLUGIN_NAME="MUnique.OpenMU.PlugIns.MuApiBridge.dll"
PLUGIN_BUILD_OUTPUT="$(mktemp -d)"
trap 'rm -rf "$PLUGIN_BUILD_OUTPUT"' EXIT

compose() {
    (cd "$SCRIPT_DIR" && docker compose -f docker-compose.yml -f docker-compose.prod.yml "$@")
}

for dir in "$OPENMU_DIR" "$MU_API_DIR" "$MU_WEB_DIR"; do
    if [ ! -d "$dir" ]; then
        echo "error: no encuentro '$dir' - este script asume que OpenMU, mu-api y mu-web son carpetas hermanas" >&2
        exit 1
    fi
done

echo "== 1/5: git pull =="
for dir in "$OPENMU_DIR" "$MU_API_DIR" "$MU_WEB_DIR"; do
    echo "-> $dir"
    git -C "$dir" pull --ff-only
done

echo "== 2/5: reconstruyendo imágenes de mu-api, mu-web y openmu-startup (fork) =="
compose build mu-api mu-web openmu-startup

echo "== 3/5: recompilando el plugin de OpenMU (PlugIns.MuApiBridge) =="
(
    cd "$OPENMU_DIR/src"
    DOCKER_BUILDKIT=1 docker build \
        -f PlugIns.MuApiBridge/Dockerfile \
        --output "type=local,dest=$PLUGIN_BUILD_OUTPUT" \
        .
)
PLUGIN_PATH="$SCRIPT_DIR/plugins/$PLUGIN_NAME"
PLUGIN_CHANGED=false
if ! cmp -s "$PLUGIN_BUILD_OUTPUT/$PLUGIN_NAME" "$PLUGIN_PATH" 2>/dev/null; then
    PLUGIN_CHANGED=true
    cp "$PLUGIN_BUILD_OUTPUT/$PLUGIN_NAME" "$PLUGIN_PATH"
fi

echo "== 4/5: aplicando migraciones de mu-api =="
"$MU_API_DIR/scripts/run-migrations.sh"

echo "== 5/5: reiniciando contenedores =="
# "up -d" recrea cada contenedor solo si su imagen cambió de verdad, así
# que esto ya cubre el caso normal de openmu-startup: si el build del
# paso 2/5 dio una imagen nueva (porque cambió algo en src/), se reinicia
# solo; si el build fue 100% cache hit, queda como estaba.
compose up -d mu-api mu-web openmu-startup
if [ "$PLUGIN_CHANGED" = true ]; then
    # El plugin se monta aparte (./plugins de solo lectura), no es parte
    # de la imagen - "up -d" no tiene forma de notar que cambió, así que
    # acá sí forzamos el restart a mano cuando el .dll recompilado difiere
    # del que ya estaba copiado.
    echo "El plugin cambió - reiniciando openmu-startup (esto desconecta a los jugadores conectados)..."
    compose restart openmu-startup
else
    echo "El plugin no cambió."
fi

echo
echo "Listo. Verificá con:"
echo "  docker compose -f docker-compose.yml -f docker-compose.prod.yml logs -f mu-api openmu-startup"
echo "  curl -s https://api.mupalmira.com.ar/healthz"
echo "  curl -s https://api.mupalmira.com.ar/public/client-manifest"
