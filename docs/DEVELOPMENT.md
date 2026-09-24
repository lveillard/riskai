# Desarrollo de Riesgus

Guía corta para preparar el entorno y trabajar con los scripts del repositorio.
Todos los comandos se ejecutan desde la raíz del repositorio en PowerShell.

## Requisitos

| Herramienta | Versión | Notas |
| --- | --- | --- |
| Unity Editor | **6000.3.23f1** | La fija `RiskAI/ProjectSettings/ProjectVersion.txt`. Instálala desde Unity Hub en la carpeta estándar (`C:\Program Files\Unity\Hub\Editor\6000.3.23f1`). |
| Módulos Unity | Web Build Support (WebGL), Windows Build Support | Web es obligatorio para `BuildWeb`. |
| Unity Hub | con su CLI (`C:\Program Files\Unity Hub\resources\cli\unity.exe`) | `scripts/Unity.ps1` lo usa. Otra ruta: variable `RISKAI_UNITY_CLI`. |
| Python | 3.14 (probado con 3.14.3) | Los tests y herramientas usan la biblioteca estándar. Los `check_*.py`/`measure_web_resources.py` necesitan además `pip install playwright` y Edge. |
| Node.js | 24 (probado con 24.11) | Tests `.cjs`/`.mjs` y despliegue Cloudflare (`npx wrangler`). |
| Git LFS | 3.7+ | Solo el audio nuevo (`*.wav`, `*.ogg`, `*.mp3`) va por LFS. Ejecuta `git lfs install --local` una vez tras clonar. |

## Primera vez

1. Clona el repositorio y ejecuta `git lfs install --local`.
2. Abre el proyecto una vez para que Unity genere `RiskAI/Library/` y los `.csproj`:
   `.\scripts\Unity.ps1 -Action Open` (o **Open-Unity.cmd**). Espera a que termine la importación y cierra Unity.
   Si no aparecen los `.csproj`, actívalos en *Preferences > External Tools > Generate .csproj files*.
3. Prepara las escenas: `.\scripts\Unity.ps1 -Action Prepare`.
4. Comprueba la compilación rápida: `python scripts/quick_compile.py`.
5. Opcional, para merges de escenas y prefabs:
   `git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'`

## Regla: un solo Unity por proyecto

Solo puede haber un Unity usando `RiskAI/` a la vez (editor abierto, tests o build).
`Unity.ps1` lo detecta (lock de `RiskAI/Temp/UnityLockfile` o un `Unity.exe` con la ruta del
proyecto en su línea de comandos) y termina al momento con **código 3** y un mensaje en español
e inglés. Cierra el editor o espera a que acabe la otra ejecución. `QuickCompile` no abre Unity
y puede usarse en paralelo.

## Comandos

| Comando | Qué hace | Resultado |
| --- | --- | --- |
| `.\scripts\Unity.ps1 -Action Open` | Abre el proyecto en el editor | — |
| `.\scripts\Unity.ps1 -Action Status` | Estado del Hub/editores (JSON) | — |
| `.\scripts\Unity.ps1 -Action Prepare` | Regenera escenas y ajustes del reproductor | `RiskAI/Logs/prepare.log` |
| `.\scripts\Unity.ps1 -Action Test` | Tests EditMode | `TestResults/editmode.xml`, `RiskAI/Logs/editmode.log` |
| `.\scripts\Unity.ps1 -Action PlayTests` | Tests PlayMode (batallas reales) | `TestResults/playmode.xml`, `RiskAI/Logs/playmode.log` |
| `... -Filter 'RiskAI.Tests.Foo;RiskAI.Tests.Bar'` | Pasa `-testFilter` a Unity (regex o lista con `;`) | — |
| `... -Category 'Naval'` | Pasa `-testCategory` a Unity | — |
| `... -Results playmode-naval` | Escribe `TestResults/playmode-naval.xml` y `RiskAI/Logs/playmode-naval.log` | — |
| `... -DryRun` | Muestra el comando Unity sin ejecutarlo | — |
| `.\scripts\Unity.ps1 -Action Build` | Build Windows | `Builds/Windows-v<VERSION>/RiskAI.exe`, `RiskAI/Logs/build.log` |
| `.\scripts\Unity.ps1 -Action BuildWeb` | Build WebGL | `Builds/Web-v<VERSION>/`, `RiskAI/Logs/build-web.log` |
| `.\scripts\Unity.ps1 -Action QuickCompile [-Assemblies Core,Runtime]` | Igual que `python scripts/quick_compile.py [Core Runtime Tests PlayTests Editor]`: compila con el Roslyn del editor sin abrir Unity (~1-2 min) | Salida 0 = OK, 1 = errores, 2 = faltan editor/`.csproj`/`Library` |
| `.\scripts\Unity.ps1 -Action CleanResults [-Days 14] [-Apply]` | Igual que `python scripts/clean_test_results.py`: borra `TestResults/*.xml` antiguos salvo `editmode.xml`/`playmode.xml`. Sin `-Apply` solo simula | — |
| `python -m unittest discover -s scripts -p "test_*.py"` | Tests Python de las herramientas (no hay pytest) | — |
| `python scripts/summarize_tests.py TestResults/*.xml` | Resume uno o varios informes NUnit (total, fallidos y su primer mensaje); sale con 1 si alguno falla | — |
| `node scripts/test_browser_pen.cjs` | Puente de lápiz/rueda del reproductor Web | — |
| `node scripts/test_cloudflare_worker.mjs` | Worker de Cloudflare (también `cd deploy/cloudflare; npm test`) | — |
| `.\scripts\deploy_cloudflare.ps1 -Action Check` | Sintaxis + tests del worker y tamaño de `Builds/Web-v<VERSION>`; no publica | — |
| `python scripts/serve_web.py [--directory Builds/Web-v<VERSION>] [--bind 0.0.0.0]` | Sirve la build Web en `http://127.0.0.1:8080` | — |
| **Play-Riesgus.cmd** | Lanza `Builds/Windows-v<VERSION>/RiskAI.exe` | `RiskAI/Logs/player-v<VERSION>.log` |

Tras `Test`/`PlayTests`, el script imprime una línea con total/aprobados/fallidos/omitidos/duración,
los primeros 20 tests fallidos y las rutas del XML y del log. El código de salida es el de Unity.
`RISKAI_UNITY_CLI` cambia la ruta de la CLI de Unity Hub y `RISKAI_UNITY_EDITOR` la del editor
que usa `quick_compile.py` (carpeta `Editor`, su `Data` o `Unity.exe`).

## Logs y resultados

- Logs del editor y del reproductor: `RiskAI/Logs/` (ignorados por git).
- Informes NUnit: `TestResults/` (ignorado por git). Limpia los antiguos con `CleanResults`.
- Capturas y revisiones visuales: `Captures/` y `RiskAI/Screenshots/` (ignorados).

## Capturas visuales

Reproductor (Windows), con la build de la versión actual:

```powershell
$exe = "Builds\Windows-v$(Get-Content VERSION)\RiskAI.exe"
& $exe --riskai-capture Captures\run1 --riskai-map europe --riskai-seed 701 -logFile RiskAI\Logs\capture.log
```

- `--riskai-capture DIR`: `RuntimeVisualCapture`, recorre la partida y guarda PNG en `DIR`; con
  `--riskai-capture-menu` fotografía antes el menú inicial.
- `--riskai-ui-capture DIR`: capturas de la interfaz (`RuntimeUiCapture`).
- `--riskai-presentation-capture DIR` con `--riskai-capture-ships` o `--riskai-capture-knight`:
  primeros planos de barcos o caballero (`RuntimePresentationCapture`).
- Mapas: `--riskai-map classic|riverlands|europe|newworld`; semilla: `--riskai-seed N`.
  Sondas de rendimiento y diagnóstico: ver [OBSERVABILITY.md](OBSERVABILITY.md).

Revisión de arte en el editor, sin build (con el editor cerrado):

```powershell
& "C:\Program Files\Unity Hub\resources\cli\unity.exe" run "$PWD\RiskAI" -- -executeMethod RiskAI.Editor.RiskArtReview.Capture --riskai-art-output "$PWD\Captures\art-review" --riskai-map europe --riskai-art-tag before -logFile "$PWD\RiskAI\Logs\art-review.log"
```

También desde el menú *RiskAI > Review > Capture art review*.

## Versión y publicación

La versión vive **solo** en el archivo `VERSION` de la raíz (una línea, p. ej. `0.30.0`). La leen:
`RiskProjectSetup` (nombre del producto, `bundleVersion` y carpetas `Builds/Windows-v…`/`Web-v…`),
`Unity.ps1`, `Play-Riesgus.cmd`, `serve_web.py` y `deploy_cloudflare.ps1`.

1. Cambia `VERSION` al número nuevo.
2. `.\scripts\Unity.ps1 -Action Prepare` (actualiza `ProjectSettings.asset`: `productName` y `bundleVersion`; súbelo con el cambio).
3. `Test`, `PlayTests` y `python -m unittest discover -s scripts -p "test_*.py"`.
4. `Build` y/o `BuildWeb`; prueba con **Play-Riesgus.cmd** o `serve_web.py`.
5. `.\scripts\deploy_cloudflare.ps1 -Action Check` y después el despliegue de
   [DEPLOY-RIESGUS-CLOUDFLARE.md](DEPLOY-RIESGUS-CLOUDFLARE.md).
6. Actualiza el texto de versión del README y añade `docs/VALIDATION-RIESGUS-v<VERSION>.md`.

## Añadir una unidad

Los números de una unidad viven solo en `RiskAI/Assets/RiskAI/Resources/Config/units.json`.
No hay una tabla C# que copiar. Parity with v0.33 was verified by the golden fixture up to commit 5490e79, then removed.

1. Añade un objeto en `units` con un `id` estable (`Footman`, `Frigate`, …). El esquema
   `scripts/config/units.schema.ts` dice qué campos son obligatorios: nombres, dominio
   (`Land`, `Sea` o `Static`), edificio de producción, vida, arma o `hostWeapons`,
   adquisición y capacidades.
2. Desde `scripts/config`: `npm test`. Si el id es nuevo, `npm run build` regenera
   `UnitKind` y el contrato C#. Un id duplicado, un campo de más o un enum desconocido
   fallan aquí.
3. El modelo y el retrato se nombran en `presentation`. La casilla del edificio la sigue
   calculando `ProductionHotkeys` (coste, orden del JSON, tierra antes que mar).
4. Regenera el resumen: `python scripts/generate_unit_rules.py`
   (`docs/RISK-RULES-v0.34.md`). Lo que no venga de la fuente va en `adaptation`.
   `python scripts/generate_unit_rules.py --check` falla si el markdown commiteado no coincide
   (lo mismo que `npm test` hace con el DTO). `npm test` en `scripts/config` ya lo lanza.
5. `RiskAI/Assets/RiskAI/link.xml` conserva `Newtonsoft.Json` entero y el ensamblado
   `RiskAI.Core` (`preserve="all"`). Sin eso, el stripping de IL2CPP/WebGL puede quitar
   los setters que Newtonsoft rellena por reflexión y `UnitCatalog.Bind` no arranca. El
   player de WebGL fija `managedStrippingLevel` en Minimal (el valor por defecto de IL2CPP
   en Unity 6.3, entero 4). No se sube de nivel: `link.xml` sigue siendo la red de seguridad
   del contrato.

## Normas del repositorio

- `.gitattributes`: todo el texto en LF; imágenes, modelos y fuentes como binarios; YAML de Unity con `merge=unityyamlmerge`.
- `.editorconfig`: UTF-8, LF, 4 espacios en C#/Python, 2 en JSON/YAML/JS/PowerShell. No impone formato al C# existente.
