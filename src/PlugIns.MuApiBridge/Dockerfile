# Builds only the mu-api bridge plugin DLL - not the whole OpenMU server.
# Lets you keep running the official prebuilt `munique/openmu` image and
# just drop this DLL into its mounted `plugins/` folder, instead of having
# to build and maintain your own full server image.
#
# Build from the repo's `src/` directory (matches src/Startup/Dockerfile's
# convention, so project-relative paths resolve the same way):
#
#   DOCKER_BUILDKIT=1 docker build \
#     -f PlugIns.MuApiBridge/Dockerfile \
#     --output type=local,dest=../plugins \
#     .
#
# (run from inside `src/`) - this writes the built DLL straight to
# `../plugins/MUnique.OpenMU.PlugIns.MuApiBridge.dll` on the host, no
# container to create or clean up.

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY . .
WORKDIR /src/PlugIns.MuApiBridge
RUN dotnet build MUnique.OpenMU.PlugIns.MuApiBridge.csproj -c Release -o /out

FROM scratch AS export
COPY --from=build /out/MUnique.OpenMU.PlugIns.MuApiBridge.dll /
