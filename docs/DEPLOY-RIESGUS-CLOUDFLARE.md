# Despliegue Web de Riesgus en Cloudflare

Estado actual: [Riesgus v0.30.0](VALIDATION-RIESGUS-v0.30.md) está publicado (`20260923T120008Z-v030-c73fd2f`).
Las nuevas builds se actualizan en el origen Azure y se verifican con
`scripts/verify_web_release.py`; no necesitan volver a desplegar el Worker ni
crear tokens Cloudflare. Los datos de v0.25/v0.26 siguientes documentan la
migración inicial y su decisión de arquitectura.

La migración conserva el origen HTTPS de Azure y pone delante un Worker
streaming. El build WebGL inspeccionado es `Builds/Web-v0.25`: el fichero
`.data.unityweb` ocupa 45.199.416 bytes (~43,1 MiB), por encima de 25 MiB.
Por eso no se usa `assets` estático de Workers para esta release. Un Worker
con assets sería más sencillo y barato de operar si todos los ficheros de una
release futura quedan por debajo de ese límite; entonces se puede migrar el
contenido al binding estático manteniendo los mismos nombres de URL.

## Qué se ha preparado

- `deploy/cloudflare/src/worker.js` sólo acepta GET/HEAD, fija el origen a
  `https://riskai-demo.spaincentral.cloudapp.azure.com`, rechaza traversal y
  no copia bundles en memoria. Conserva `Content-Encoding: gzip`, fuerza MIME
  WASM/JS y cachea bundles versionados durante un año. HTML queda `no-store`.
- `deploy/cloudflare/wrangler.toml` usa `workers_dev` para preview. El entorno
  explícito `production` adjunta `riesgus.com/*` y `www.riesgus.com/*`; el
  Worker devuelve 301 de `www` al apex. Son rutas normales de Workers: el
  fichero no crea ni modifica DNS; el operador debe preparar los registros
  proxied y asociar las rutas.
- `scripts/test_cloudflare_worker.mjs` prueba proxy, MIME/cache, gzip,
  redirección, métodos y traversal sin red ni credenciales.

## Comandos reproducibles

Desde la raíz (el `Check` no publica ni lee tokens):

```powershell
pwsh -File scripts/deploy_cloudflare.ps1 -Action Check
```

La verificación aislada también puede ejecutarse con Node:

```powershell
Push-Location deploy/cloudflare
npm ci
npm test
Pop-Location
node --check deploy/cloudflare/src/worker.js
node scripts/test_cloudflare_worker.mjs
```

Para que el operador publique primero en `workers.dev`:

```powershell
pwsh -File scripts/deploy_cloudflare.ps1 -Action DeployPreview
```

Tras verificar el build WebGL v0.26 y DNS, el despliegue productivo es una
acción separada y explícita:

```powershell
pwsh -File scripts/deploy_cloudflare.ps1 -Action DeployProduction
```

Equivalentes directos desde `deploy/cloudflare` usan Wrangler fijado a
`4.136.1`:

```powershell
npx --yes --package wrangler@4.136.1 wrangler deploy --config wrangler.toml --dry-run
npx --yes --package wrangler@4.136.1 wrangler deploy --config wrangler.toml
npx --yes --package wrangler@4.136.1 wrangler deploy --config wrangler.toml --env production
```

La release v0.26 está publicada en `https://riesgus.com`. `www.riesgus.com`
redirige al apex y HTTP a HTTPS. Ambos CNAME tienen proxy activado; el entorno
de producción desactiva workers.dev. La publicación del 22 de septiembre usa
el Worker `ce61540d-c206-4979-a369-81f47c2f5374` y la release Azure
`20260922T015400Z-riesgus-v026`.

Azure prepara las releases con `scripts/deploy_azure_vm.py` y prefijos
versionados; el Worker reenvía esas rutas y deja disponible el rollback de
Azure. Los diez archivos públicos se verificaron contra sus hashes y se probó
el arranque real desde el dominio. [Resultados](VALIDATION-RIESGUS-v0.26.md).

## Permisos mínimos y operación

Usar únicamente dos credenciales temporales y revocarlas al terminar:

1. Bootstrap, proporcionada por el propietario: administración de tokens de
   usuario (`API Tokens: Read/Write`) para crear, limitar y revocar la
   credencial de trabajo. No se utiliza como credencial de Wrangler.
2. Trabajo (limitada a esta migración): en la cuenta, `Account Settings: Read`
   y `Workers Scripts: Write`; únicamente en la zona `riesgus.com`, `Zone:
   Read`, `DNS: Write` y `Workers Routes: Write` (en la interfaz de Cloudflare
   pueden aparecer como `Edit`).

El token de trabajo cubre preview, actualización del Worker, rutas apex/www y
los registros proxied necesarios. No crear tokens adicionales para cada paso;
revocar ambos tokens temporales después de verificar producción.

En esta migración ambos se borraron el 22 de septiembre a las 02:07 UTC y se
comprobó que ambos devolvían 401. No se conserva una credencial de despliegue;
una actualización futura del Worker, sus rutas o DNS deberá recibir una
credencial nueva con los permisos correspondientes.

El Worker no necesita `Workers KV`, R2, Pages ni `Account API Tokens: Edit`.
Actualizar la build del origen sí utiliza el despliegue SSH de Azure existente.
El operador debe comprobar antes de asociar las rutas que el
origin HTTPS responde, que el index v0.26 no está cacheado y que un `HEAD` de
`.data.unityweb` conserva gzip. Si se desea bloquear el acceso directo a
Azure, hacerlo después con una regla de origen que permita únicamente las
redes/endpoints de Cloudflare, tras validar el Worker.
