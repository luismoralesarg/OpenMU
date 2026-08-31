# External plugins

Drop compiled plugin DLLs here (e.g.
`MUnique.OpenMU.PlugIns.MuApiBridge.dll`, built from
`src/PlugIns.MuApiBridge/Dockerfile`). This folder is mounted read-only
into the `openmu-startup` container at `/app/plugins`.

Dropping a DLL here does **not** activate it by itself - go to the
AdminPanel's `Plugins` page and register it by its assembly file name so
`PlugInManager` picks it up on the next restart.
