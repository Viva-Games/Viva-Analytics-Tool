# Módulo Viva Remote Config — Diseño

Fecha: 2026-09-07
Repo: `Viva-Games/Viva-Analytics-Tool`, rama `develop`
Estado: aprobada e implementada el 2026-09-08 en el working tree (sin commit). Ver §12 para lo verificado y lo pendiente.

## Objetivo

Un módulo `com.vivagames.remoteconfig`, instalable desde Viva > Package Installer,
que haga por Firebase Remote Config lo que el módulo de analíticas hace por los
eventos: que cualquier dev del estudio lo integre sin errores de implementación.

Tres piezas:

1. **Runtime.** `RemoteConfigService`, envoltorio estático de Firebase Remote
   Config con la misma filosofía que `AnalyticsService`: proveedor de Firebase
   activado por un define autodetectado y proveedor local cuando no hay SDK.
2. **Ventana de parámetros.** Viva > Remote Config > Parameters: lista de
   parámetros con clave, tipo, valor por defecto validado y descripción. Se
   guarda en un JSON del proyecto, que es la fuente de verdad.
3. **Código generado.** Al guardar se regenera `RemoteConfigParameters.cs` con
   una propiedad tipada por parámetro, las claves como constantes y el
   diccionario de defaults. El juego escribe `RemoteConfigParameters.Lives` y
   no vuelve a teclear ni claves ni valores por defecto.

Punto de partida: `Assets/VivaAnalytics/Scripts/RemoteConfigManager.cs` de
Arrows (spec `2026-05-05-firebase-remote-config-manager-design.md`), en
producción y funcionando. Lo que se cambia respecto a él está justificado en
§2 con la documentación consultada.

## Fuera de alcance (1.0)

- Actualizaciones en tiempo real (`OnConfigUpdateListener`, SDK Unity ≥ 11.0).
  El servicio deja el hueco (`OnValuesUpdated`) para añadirlas en 1.1.
- Exportar la plantilla al formato de la consola de Firebase. La consola admite
  "Publish from a file", pero publicar una plantilla **sustituye la plantilla
  entera** (condiciones incluidas); merece un diseño aparte con merge sobre la
  plantilla descargada.
- Valores de prueba por desarrollador para Play Mode en el editor.
- Re-fetch al volver de segundo plano, overrides por plataforma.
- Instalar el SDK de Firebase: como en analíticas, lo importa el proyecto.

## 1. Hechos comprobados en la documentación

Base de las decisiones de §2. Fuentes: guía de Unity de Remote Config, guía de
estrategias de carga, límites de parámetros, fuente C# del SDK de Unity en
GitHub (`FirebaseRemoteConfig.cs`, `ConfigValue.cs`, `ConfigSettings.cs`,
`app.i`) y header C++ `remote_config.h`. La API se ha contrastado además con
`Firebase.RemoteConfig.dll` 13.10.0 de Arrows: todos los miembros usados existen.

| Hecho | Fuente |
|---|---|
| `FetchAsync(TimeSpan.Zero)` fuerza siempre un fetch. `FetchAndActivateAsync()` respeta el intervalo mínimo, cuyo valor por defecto es 12 h. Timeout de fetch por defecto: 60 s. | `FirebaseRemoteConfig.cs`, `ConfigSettings.cs` |
| Fetches demasiado frecuentes se rechazan con `FetchFailureReason.Throttled`: "you are sending too many fetch requests in too short a time". La doc recomienda intervalos bajos solo en desarrollo. | `remote_config.h`, guías Android/iOS |
| Llamar a `ActivateAsync()` al arrancar "aplica cualquier valor descargado del servidor en una sesión anterior, y es casi instantáneo". | Guía de estrategias de carga, estrategia 3 |
| `ConfigValue.LongValue`, `DoubleValue` y `BooleanValue` **lanzan `FormatException`** si la cadena no convierte. `BooleanValue` acepta `1/true/t/yes/y/on` y `0/false/f/no/n/off/""` sin distinguir mayúsculas. Conversión numérica con `InvariantCulture`. | `ConfigValue.cs` |
| `ValueSource.StaticValue`: la clave no existe ni en remoto ni en defaults. `DefaultValue`: viene de `SetDefaultsAsync`. `RemoteValue`: viene de la última activación. | `remote_config.h` |
| `SetDefaultsAsync(IDictionary<string, object>)` sustituye todos los defaults anteriores. El ejemplo oficial usa `int`, `double`, `bool` y `string`. | `FirebaseRemoteConfig.cs`, guía de Unity |
| Claves: hasta 256 caracteres, empiezan por letra ASCII o `_`, pueden llevar dígitos. Tipos de la consola: String, Number, Boolean, JSON. Máximo 3000 parámetros; la suma de valores no puede superar 1 000 000 caracteres. | Doc de parámetros |
| **Dos `CheckAndFixDependenciesAsync` concurrentes no son seguros.** El SDK guarda el hilo que está comprobando y cualquier llamada a Firebase desde otro hilo mientras tanto lanza `InvalidOperationException` ("Don't call Firebase functions before CheckDependencies has finished"). | `app.i` del SDK de Unity |

## 2. Qué se conserva de Arrows y qué cambia

| Aspecto | Arrows hoy | Módulo | Por qué |
|---|---|---|---|
| Momento de inicialización | `RemoteConfigManager.Initialize()` desde `OnFirebaseReady()` de `AnalyticsInit` | Cada módulo se inicializa solo, con su propio `RemoteConfigInit`. La comprobación de dependencias de Firebase se hace **una sola vez** a través de `VivaFirebase`, en el core, y la comparten todos los módulos (§6.6). Llamar a `Initialize` desde `OnFirebaseReady()` sigue funcionando: es lo que hará el envoltorio de Arrows (§7). | Los módulos tienen que funcionar solos y combinados, en cualquier orden. Dos comprobaciones concurrentes no son seguras (§1, última fila). |
| Fetch | `FetchAsync(TimeSpan.Zero)` una vez por arranque en frío | Igual por defecto. El intervalo es una opción (`MinimumFetchInterval`, 0 por defecto) | Un fetch por arranque es el patrón del quickstart de Unity y funciona en Arrows. Se deja configurable porque la doc recomienda intervalos bajos solo en desarrollo. |
| Fetch fallido o throttled | `IsReady` queda `false` para siempre, `OnReady` no se dispara, todo devuelve el default del llamador. Los valores cacheados de la sesión anterior se ignoran. | Antes del fetch se activa la caché (`ActivateAsync`). `OnReady` se dispara **una vez** cuando el intento de fetch termina, con éxito o sin él. `Source` dice qué hay: `Remote`, `Cache` o `Defaults`. | Un jugador sin red recibe los últimos valores remotos que vio, no los defaults. Todos los suscriptores de Arrows toleran defaults, y el menú de `TitleAnimatorController` deja de esperar 5 s cuando no hay red. **Cambio de comportamiento: confirmar en revisión (§9).** |
| Getters antes de `IsReady` | Default del llamador | Mejor valor disponible: activado en caché, si no el default registrado, si no el del llamador | Mismo motivo. **Confirmar en revisión (§9).** |
| Clave inexistente (`StaticValue`) | Default del llamador | Igual | |
| Conversión con formato inválido | `LongValue` lanza `FormatException` y la excepción sale por el código del juego | `try/catch`: aviso en consola una vez por clave y default | Un valor mal tecleado en la consola no puede tirar el juego. |
| Tipos | int, string, bool, JSON | + long, float, double | Number en la consola admite decimales. |
| JSON | `JsonUtility.FromJson<T>` | Igual, documentando que no admite arrays ni diccionarios de primer nivel | Mismo parser que el resto del proyecto; sin dependencias nuevas. |
| Defaults | Cada llamada pasa el suyo; sin `SetDefaultsAsync` | Registrados en la ventana, generados en `RemoteConfigParameters.Defaults()` y pasados a `SetDefaultsAsync`. Los getters con default explícito siguen existiendo. | Una sola fuente de verdad; Firebase conoce los defaults (`ValueSource.DefaultValue`). Los defaults que no son literales (`config.livesPerLevel`) siguen usando el getter con default. |
| Suscripción a `OnReady` | `if (IsReady) X(); else OnReady += X;` en cada consumidor | `WhenReady(Action)`: ejecuta ya si está listo, si no una vez al estar | Quita el patrón repetido y su carrera. |
| Namespace | `Viva.Services.Analytics` | `Viva.Services.RemoteConfig` | Módulo independiente. Compatibilidad con Arrows en §7. |

## 3. Estructura del paquete

```
Packages/com.vivagames.remoteconfig/
  package.json                      com.vivagames.remoteconfig 1.0.0, "Viva Remote Config", unity 2021.3
  README.md, CHANGELOG.md
  Runtime/
    VivaGames.RemoteConfig.asmdef   rootNamespace Viva.Services.RemoteConfig, sin referencias
    RemoteConfigService.cs
    RemoteConfigOptions.cs
    RemoteConfigSource.cs
    IRemoteConfigProvider.cs
    LocalRemoteConfigProvider.cs
    RemoteConfigConverter.cs        conversiones string -> tipo, compartidas por los proveedores
  Runtime/Firebase/
    VivaGames.RemoteConfig.Firebase.asmdef   defineConstraints VIVA_FIREBASE_REMOTE_CONFIG
    FirebaseRemoteConfigProvider.cs
  Editor/
    VivaGames.RemoteConfig.Editor.asmdef     referencias: VivaGames.RemoteConfig, VivaGames.Core.Editor
    RemoteConfigPackage.cs          versión y Templates~ (copia de AnalyticsPackage)
    RemoteConfigEditorSettings.cs   ProjectSettings/VivaRemoteConfigSettings.json
    RemoteConfigSdkDetector.cs      Firebase.RemoteConfig.dll -> VIVA_FIREBASE_REMOTE_CONFIG
    RemoteConfigParameterFile.cs    modelo + lectura/escritura del JSON
    RemoteConfigValidator.cs        claves, valores por tipo, identificadores C#
    JsonSyntaxValidator.cs          validador sintáctico sin modelo de objetos
    RemoteConfigCodeGenerator.cs
    RemoteConfigParametersWindow.cs Viva > Remote Config > Parameters
    RemoteConfigSetupWindow.cs      Viva > Remote Config > Setup
    LegacyRemoteConfigImporter.cs   importar claves desde código y migrar el manager antiguo
  Templates~/
    RemoteConfigInit.cs.txt         MonoBehaviour de inicialización (§6.5)
    RemoteConfigManager.compat.cs.txt   envoltorio de compatibilidad (§7)
  Tests/Editor/
    VivaGames.RemoteConfig.Tests.asmdef
    RemoteConfigValidatorTests.cs, JsonSyntaxValidatorTests.cs,
    RemoteConfigCodeGeneratorTests.cs, RemoteConfigConverterTests.cs,
    RemoteConfigServiceTests.cs, LegacyRemoteConfigImporterTests.cs
```

Cambios fuera del paquete:

- `com.vivagames.core` → 2.1.0: (a) assembly nuevo de runtime
  `VivaGames.Core.Firebase` con `VivaFirebase`, la comprobación compartida de
  dependencias (§6.6); (b) `FirebaseAppDetector` en el editor, que mantiene el
  define `VIVA_FIREBASE` cuando `Firebase.App.dll` está en el proyecto; (c) la
  línea de `VivaModuleCatalog` (ya está como comentario) con
  `tagPrefix: "remoteconfig"` y sin `legacyFolders`; (d) `VivaModule.RequiredModules`
  para que el instalador avise si un módulo instalado es demasiado antiguo
  (§6.6). `ScriptingDefines` y `VivaPackageUtility` no cambian.
- `com.vivagames.analytics` → 2.1.0: `FirebaseAnalyticsTracker` deja de llamar
  a `CheckAndFixDependenciesAsync` directamente y pasa por `VivaFirebase`
  (§6.6). API pública intacta (`FirebaseReady`, `IsFirebaseReady`), así que
  los `AnalyticsInit.cs` de los proyectos no cambian. Es el único cambio.
  El módulo de Remote Config no referencia el assembly de analíticas:
  `StringUtils` vive allí, así que el editor de Remote Config lleva su propia
  conversión clave → identificador (§5.2).
- README raíz: fila nueva en la tabla de módulos.

## 4. Fichero de parámetros (fuente de verdad)

`Assets/VivaRemoteConfig/RemoteConfigParameters.json`, ruta configurable en
Setup. Formato compatible con `JsonUtility` (objeto raíz con array):

```json
{
  "parameters": [
    { "key": "lives", "type": "int", "defaultValue": "5", "description": "Max lives per level" },
    { "key": "startInterstitialsLevel", "type": "int", "defaultValue": "15", "description": "" },
    { "key": "nicknameAllowlist", "type": "string", "defaultValue": "", "description": "Comma separated" },
    { "key": "idfa_list", "type": "json", "defaultValue": "{\"entries\":[]}", "description": "" }
  ]
}
```

- `defaultValue` se guarda siempre como cadena, igual que lo hace Firebase, y
  se valida según `type` al guardar.
- `type` ∈ `int | long | float | double | bool | string | json`. En la consola
  de Firebase corresponden a NUMBER (los cuatro numéricos), BOOLEAN, STRING y
  JSON. El tipo C# decide qué conversión se aplica al leer.
- Orden de la lista = orden en el fichero generado. La ventana permite
  reordenar.

### 4.1 Validación (bloquea el guardado)

| Campo | Regla |
|---|---|
| `key` | `^[A-Za-z_][A-Za-z0-9_]*$`, ≤ 256 caracteres, única (distingue mayúsculas). **No se normaliza**: Arrows usa camelCase y las claves deben coincidir con la consola. |
| `int` | `int.TryParse` invariante |
| `long` | `long.TryParse` invariante |
| `float`, `double` | `double.TryParse` invariante; ni NaN ni infinito |
| `bool` | `true` o `false` (se guarda en minúsculas) |
| `string` | cualquier cosa |
| `json` | `JsonSyntaxValidator` (gramática RFC 8259: objeto, array, cadena con escapes, número, `true/false/null`); indica posición del error |
| identificador | el nombre de propiedad derivado de la clave (§5.2) debe ser un identificador C# válido y único; `lives` y `Lives` chocan y se avisa |
| tamaño | aviso, no bloqueo, al pasar de 3000 parámetros o 1 000 000 caracteres en valores |

## 5. Código generado

`Assets/VivaRemoteConfig/RemoteConfigParameters.cs`, ruta configurable. Cabecera
`<auto-generated>` que remite a la ventana. Se regenera entero en cada guardado.

```csharp
// <auto-generated>
// Generated by Viva Remote Config from Assets/VivaRemoteConfig/RemoteConfigParameters.json.
// Edit the parameters in Viva > Remote Config > Parameters; do not edit this file.
// </auto-generated>
using System.Collections.Generic;

namespace Viva.Services.RemoteConfig
{
    public static class RemoteConfigParameters
    {
        /// <summary>Max lives per level.</summary>
        public static int Lives => RemoteConfigService.GetInt(Keys.Lives, 5);

        public static int StartInterstitialsLevel => RemoteConfigService.GetInt(Keys.StartInterstitialsLevel, 15);

        /// <summary>Comma separated.</summary>
        public static string NicknameAllowlist => RemoteConfigService.GetString(Keys.NicknameAllowlist, "");

        /// <summary>Raw JSON. Use IdfaList&lt;T&gt;() to parse it.</summary>
        public static string IdfaListJson => RemoteConfigService.GetString(Keys.IdfaList, "{\"entries\":[]}");
        public static T IdfaList<T>(T defaultValue = default) => RemoteConfigService.GetJson(Keys.IdfaList, defaultValue);

        public static class Keys
        {
            public const string Lives = "lives";
            public const string StartInterstitialsLevel = "startInterstitialsLevel";
            public const string NicknameAllowlist = "nicknameAllowlist";
            public const string IdfaList = "idfa_list";
        }

        /// <summary>In-app defaults, registered in Firebase by RemoteConfigService.Initialize.</summary>
        public static Dictionary<string, object> Defaults() => new Dictionary<string, object>
        {
            { Keys.Lives, 5 },
            { Keys.StartInterstitialsLevel, 15 },
            { Keys.NicknameAllowlist, "" },
            { Keys.IdfaList, "{\"entries\":[]}" },
        };
    }
}
```

### 5.1 Reglas de generación

- Accesor por tipo: `int → GetInt`, `long → GetLong`, `float → GetFloat`,
  `double → GetDouble`, `bool → GetBool`, `string → GetString`. `json` genera
  el accesor `XxxJson` (string) y el genérico `Xxx<T>()`.
- Literales C# con `InvariantCulture` (`2.5`, `2.5f`, `5L`). Cadenas y JSON
  escapados como literal C# normal (`\"`, `\\`, `\n`).
- `Defaults()` usa los tipos que admite `SetDefaultsAsync`: `int`, `long`,
  `double` (también para `float`), `bool`, `string`.
- La descripción va al `<summary>`; vacía, no se emite.
- Sin parámetros: se genera la clase con `Keys` vacío y `Defaults()` vacío para
  que el init compile igual.

### 5.2 Clave → identificador

Partición por `_` y por cambio minúscula → mayúscula, cada trozo con inicial
mayúscula: `startInterstitialsLevel → StartInterstitialsLevel`,
`idfa_list → IdfaList`, `MAX_LIVES → MaxLives`, `lives → Lives`. Si empieza
por dígito o coincide con una palabra reservada se antepone `_`. Helper propio
en el assembly de editor (`RemoteConfigNaming`), sin depender de `StringUtils`.

## 6. Runtime

### 6.1 API pública

```csharp
namespace Viva.Services.RemoteConfig
{
    public enum RemoteConfigSource { None, Defaults, Cache, Remote }

    public sealed class RemoteConfigOptions
    {
        /// <summary>Se pasa a FetchAsync(TimeSpan). Zero fuerza el fetch en cada arranque (como Arrows). Firebase usa 12 h por defecto.</summary>
        public TimeSpan MinimumFetchInterval = TimeSpan.Zero;
        /// <summary>null = timeout del SDK (60 s).</summary>
        public TimeSpan? FetchTimeout = null;
        /// <summary>Activa al arrancar lo descargado en la sesión anterior.</summary>
        public bool ActivateCachedValuesOnStart = true;
        public bool LogToConsole = true;
    }

    public interface IRemoteConfigProvider
    {
        void Initialize(IDictionary<string, object> defaults, RemoteConfigOptions options,
                        Action<RemoteConfigSource> onValuesAvailable, Action<RemoteConfigSource> onFetchConcluded);
        /// <summary>false si la clave no existe ni en remoto ni en defaults (StaticValue).</summary>
        bool TryGetRaw(string key, out string raw, out RemoteConfigSource source);
    }

    public static class RemoteConfigService
    {
        public static bool IsReady { get; }              // true cuando el primer intento de fetch ha terminado
        public static RemoteConfigSource Source { get; } // de dónde salen los valores ahora mismo
        public static event Action OnReady;              // una sola vez, tras el primer intento de fetch
        public static event Action OnValuesUpdated;      // activaciones posteriores (reservado: tiempo real en 1.1)

        public static void Initialize(IDictionary<string, object> defaults, RemoteConfigOptions options = null);
        public static void Initialize(IRemoteConfigProvider provider, IDictionary<string, object> defaults, RemoteConfigOptions options = null);
        public static void WhenReady(Action callback);

        public static int    GetInt   (string key, int    defaultValue = 0);
        public static long   GetLong  (string key, long   defaultValue = 0);
        public static float  GetFloat (string key, float  defaultValue = 0f);
        public static double GetDouble(string key, double defaultValue = 0d);
        public static bool   GetBool  (string key, bool   defaultValue = false);
        public static string GetString(string key, string defaultValue = "");
        public static T      GetJson<T>(string key, T defaultValue = default);
        public static bool   HasValue (string key);      // true si hay valor remoto o default registrado
    }
}
```

`Initialize(defaults, options)` elige el proveedor: `FirebaseRemoteConfigProvider`
si `VIVA_FIREBASE_REMOTE_CONFIG` está definido, `LocalRemoteConfigProvider` si
no. La sobrecarga con proveedor explícito es para tests y proyectos especiales.
Una segunda llamada a `Initialize` es no-op con aviso.

### 6.2 Reglas de los getters

1. Sin `Initialize` → default del llamador.
2. `TryGetRaw` devuelve `false` (clave inexistente) → default del llamador.
3. Conversión con `RemoteConfigConverter`, que replica la semántica del SDK:
   bool con la misma expresión regular que `ConfigValue.BooleanValue`; `long` y
   `double` con `TryParse` invariante; `int` y `float` por estrechamiento con
   comprobación de rango. Fallo → `Debug.LogWarning` una vez por clave y default
   del llamador. Nunca lanza.
4. `GetJson<T>`: `JsonUtility.FromJson<T>`; cadena vacía o excepción → aviso y
   default. Igual que Arrows.

Las conversiones las hace el servicio sobre la cadena cruda, no el
`ConfigValue` de Firebase, para que Firebase y el proveedor local se comporten
igual y sea testeable sin SDK.

### 6.3 Proveedor de Firebase

Todo con `ContinueWithOnMainThread` (`Firebase.TaskExtension.dll`, incluido en
cualquier paquete de Firebase; Arrows ya lo usa). Flujo de `Initialize`:

0. `VivaFirebase.EnsureInitialized()` y `VivaFirebase.WhenReady(...)` (§6.6):
   lo que sigue corre cuando Firebase está listo. Si la comprobación falla se
   salta al paso 5 con `Source = Defaults`.
1. `rc = FirebaseRemoteConfig.DefaultInstance`. Si `options.FetchTimeout` tiene
   valor: `SetConfigSettingsAsync` con ese timeout. No se toca el intervalo
   mínimo en los settings: se pasa por llamada a `FetchAsync(TimeSpan)`, como
   hace Arrows.
2. `SetDefaultsAsync(defaults)`.
3. Si `ActivateCachedValuesOnStart`: `ActivateAsync()`. Termine como termine,
   se recorre `AllValues`: si alguna clave tiene `ValueSource.RemoteValue`,
   `Source = Cache`; si no, `Defaults`. Se avisa `onValuesAvailable(Source)`.
4. `FetchAsync(options.MinimumFetchInterval)`. Al terminar:
   - `Info.LastFetchStatus == Success` → `ActivateAsync()` → `Source = Remote`.
   - Si no → `Debug.LogWarning` con `LastFetchStatus` y `LastFetchFailureReason`
     (`Throttled` se explica en el mensaje). `Source` se queda como estaba.
   - En ambos casos `onFetchConcluded(Source)`: el servicio pone `IsReady = true`
     y dispara `OnReady` una vez.
5. Excepción en cualquier paso: se registra y se garantiza `onFetchConcluded`.

`TryGetRaw`: `rc.GetValue(key)`; `Source == StaticValue` → `false`; si no,
`raw = StringValue`, `source` según `ValueSource`.

**El proveedor nunca llama a `CheckAndFixDependenciesAsync` directamente**:
solo `VivaFirebase` lo hace, y una sola vez (§6.6).

### 6.4 Proveedor local

Sin SDK, o en el editor cuando no está el define. Valores = defaults.
`onValuesAvailable(Defaults)` y `onFetchConcluded(Defaults)` se llaman
síncronamente dentro de `Initialize`; `WhenReady` cubre a quien se suscriba
después. `Debug.LogWarning` al inicializar: "Firebase Remote Config SDK not
found. Using in-app defaults." Es también el punto donde en 1.1 entrarían los
valores de prueba del editor.

### 6.5 Inicialización en el proyecto

Cada módulo trae su propio componente de inicialización y ninguno depende de
otro. Setup genera `Assets/VivaRemoteConfig/RemoteConfigInit.cs` desde
`Templates~/RemoteConfigInit.cs.txt`, un MonoBehaviour para la primera escena
igual que `AnalyticsInit`. El fichero es del usuario y no se sobrescribe:

```csharp
public class RemoteConfigInit : MonoBehaviour
{
    private void Awake()
    {
        // Options: fetch interval, timeout... See the README.
        RemoteConfigService.Initialize(RemoteConfigParameters.Defaults());
    }
}
```

Da igual el orden de los `Awake` de `AnalyticsInit` y `RemoteConfigInit`: el
primero que llega arranca la comprobación de Firebase en `VivaFirebase` y el
otro se pone a la cola (§6.6). Las tres combinaciones funcionan sin tocar
nada: solo analíticas, solo Remote Config, o las dos.

Llamar a `RemoteConfigService.Initialize` desde `AnalyticsInit.OnFirebaseReady()`
también funciona: Firebase ya está listo y el fetch arranca en el acto. Es lo
que hace Arrows a través del envoltorio (§7) y no hace falta cambiarlo.

### 6.6 Inicialización compartida de Firebase (core 2.1.0)

**Implementado y verificado el 2026-09-07** (tests EditMode y PlayMode en
verde, ver §10). Dos assemblies de runtime en el core:

- `VivaGames.Core` (`Runtime/`, sin constraints ni SDK): `ReadyGate`, la puerta
  genérica de "arranca una vez, esperan todos" con tests unitarios.
- `VivaGames.Core.Firebase` (`Runtime/Firebase/`, `defineConstraints: VIVA_FIREBASE`):
  `VivaFirebase`, envoltorio estático de `ReadyGate` que hace la llamada real a
  `CheckAndFixDependenciesAsync`. El define lo mantiene `FirebaseAppDetector`
  (editor del core) cuando `Firebase.App.dll` está entre los assemblies
  precompilados, con `SdkDetection.SyncDefine`, que también usarán los
  detectores de los módulos.

Namespace `Viva.Core` (no `Viva.Core.Firebase`: dentro de un namespace llamado
`Firebase` el identificador `Firebase` deja de resolver al SDK).

```csharp
namespace Viva.Core
{
    public enum ReadyState { NotStarted, Running, Ready, Failed }

    public static class VivaFirebase
    {
        public static ReadyState State { get; }
        public static bool IsReady { get; }
        public static bool HasFailed { get; }
        public static string FailureReason { get; }

        /// <summary>Arranca CheckAndFixDependenciesAsync la primera vez; las siguientes llamadas no hacen nada.
        /// requestedBy solo sale en el log: "[Viva] Checking Firebase dependencies (requested by Viva Analytics)...".</summary>
        public static void EnsureInitialized(string requestedBy = null);

        /// <summary>onReady corre en el hilo principal cuando Firebase está listo (en el acto si ya lo estaba);
        /// onFailed cuando la comprobación falla. Cada callback se invoca como mucho una vez, en orden de suscripción.</summary>
        public static void WhenReady(Action onReady, Action<string> onFailed = null);
    }
}
```

- `EnsureInitialized` es idempotente y se usa desde el hilo principal. La
  continuación va por `ContinueWithOnMainThread`, como hacía el tracker.
  `DependencyStatus.Available` → `Ready`; cualquier otro estado, excepción o
  cancelación → `Failed` con el motivo y `Debug.LogError`.
- No decide cuándo se inicializa Firebase: lo hace el primer módulo que llama.
  En Arrows sigue siendo el `Awake` de `AnalyticsInit`, como ahora.
- `FirebaseAnalyticsTracker` (analytics 2.1.0) sustituye su bloque de
  `CheckAndFixDependenciesAsync` por `EnsureInitialized` + `WhenReady` bajo
  `#if VIVA_FIREBASE`, y en los callbacks hace exactamente lo que hacía: vaciar
  la cola y disparar `FirebaseReady`, o registrar el error y descartar la cola.
  Bajo `#if !VIVA_FIREBASE` conserva su comprobación propia de la 2.0.0.
- `FirebaseRemoteConfigProvider` usa `VivaFirebase` sin condicional: su
  assembly exige `VIVA_FIREBASE_REMOTE_CONFIG` y `VIVA_FIREBASE` a la vez, y
  nadie del proyecto referencia sus tipos (el servicio lo descubre por
  registro), así que nunca provoca errores de compilación.

**Por qué el `#if` en el tracker.** Detectado al verificar: en un proyecto que ya
tiene `VIVA_FIREBASE_ANALYTICS` (todos los existentes, Arrows incluido) y
actualiza a core 2.1 + analytics 2.1, la primera compilación ocurre antes de
que `FirebaseAppDetector` haya definido `VIVA_FIREBASE`. Si el tracker exigiera
`VivaFirebase`, esa compilación fallaría y Unity no cargaría el detector nuevo:
bloqueo permanente hasta añadir el define a mano. Con el `#if` todo compila en
cualquier combinación, el detector corre, define el símbolo y la siguiente
compilación ya usa el gate. Confirmado en batchmode: primera pasada sin el
define, `[Viva] Firebase.App.dll detected. VIVA_FIREBASE enabled.`, segunda
pasada con el gate. Unity ignora en silencio la referencia de asmdef a un
assembly excluido por constraint.

Compatibilidad de versiones:

| Combinación | Resultado |
|---|---|
| core 2.1 + analytics 2.1 | Una comprobación. |
| core 2.1 + remoteconfig 1.0 | Una comprobación. |
| core 2.1 + analytics 2.1 + remoteconfig 1.0 | Una comprobación, en cualquier orden de `Awake`. |
| core 2.1 + analytics 2.0 + remoteconfig 1.0 | Dos comprobaciones: **no soportado**. El instalador lo impide. |
| core 2.0 + analytics 2.1 | Compila; el tracker comprueba por su cuenta como en 2.0.0 y lo dice en el log. Sin otros módulos Firebase, funciona. |

`VivaModule` gana `RequiredModules`: pares (paquete, versión mínima) que solo
se comprueban si ese paquete está instalado. Remote Config declara
`core >= 2.1.0` y `analytics >= 2.1.0`; analytics declara `core >= 2.1.0`. El
instalador deshabilita Install, Update y las acciones de rama con el aviso
"X needs Y 2.1.0 or newer (installed: 2.0.0). Update Y first." Nunca
deshabilita Remove. Es una comparación con `VivaVersion`, que ya existe.

Verificación hecha (§10): tests de `ReadyGate` (arranque único, orden, ejecución
inmediata, fallo, excepción en un callback, suscripción desde un callback,
escenario de dos módulos) y test PlayMode de integración con el SDK real en el
editor: dos módulos piden la inicialización, una sola línea
`[Viva] Checking Firebase dependencies (requested by Module A)...`, los dos
avisados en el hilo principal, un suscriptor tardío ejecutado en el acto.

## 7. Migración desde el `RemoteConfigManager` antiguo (Arrows)

Setup detecta un fichero en `Assets` (fuera de `Packages`) que declare
`static class RemoteConfigManager` en `Viva.Services.Analytics`. Dos pasos,
ambos manuales y en este orden:

**1. Import from code** (botón de la ventana de parámetros). Recorre los `*.cs`
de `Assets` buscando `RemoteConfigManager.Get(Int|String|Bool|Json)` y saca
clave, tipo y default:

- Clave literal, o identificador resuelto contra `const string NOMBRE = "..."`
  del mismo fichero (Arrows usa `RC_SLOTS` y similares).
- Default literal → rellenado. Default no literal (`config.livesPerLevel`) →
  vacío y marcado "needs a value", con el fichero y línea de la llamada.
- Misma clave con tipos distintos → conflicto marcado.
- `GetJson<T>` → tipo `json`.

No escribe nada: rellena la ventana y el usuario completa y guarda. En Arrows
saldrían 10 claves, 4 de ellas sin default literal (`lives` y las tres de
`DailyChallengeController`).

**2. Migrate legacy manager** (botón de Setup, requiere que
`RemoteConfigParameters.cs` exista). Copia el manager antiguo a
`Assets/VivaAnalytics/Legacy/RemoteConfigManager.legacy.txt` y sustituye su
contenido por `Templates~/RemoteConfigManager.compat.cs.txt`: misma clase,
mismo namespace, misma API pública, cuerpo que reenvía al servicio nuevo.

```csharp
namespace Viva.Services.Analytics
{
    // Compatibility wrapper generated by Viva Remote Config.
    // Migrate call sites to RemoteConfigParameters / RemoteConfigService and delete this file.
    public static class RemoteConfigManager
    {
        public static bool IsReady => RemoteConfigService.IsReady;
        public static event Action OnReady { add => RemoteConfigService.OnReady += value; remove => RemoteConfigService.OnReady -= value; }
        public static void Initialize() => RemoteConfigService.Initialize(RemoteConfigParameters.Defaults());
        public static int GetInt(string key, int defaultValue = 0) => RemoteConfigService.GetInt(key, defaultValue);
        public static string GetString(string key, string defaultValue = "") => RemoteConfigService.GetString(key, defaultValue);
        public static bool GetBool(string key, bool defaultValue = false) => RemoteConfigService.GetBool(key, defaultValue);
        public static T GetJson<T>(string key, T defaultValue = default) => RemoteConfigService.GetJson(key, defaultValue);
    }
}
```

Resultado en Arrows: los 7 scripts y 18 llamadas compilan sin tocarlos, y
`AnalyticsInit.OnFirebaseReady()` sigue llamando a `RemoteConfigManager.Initialize()`.
La migración de cada llamada a `RemoteConfigParameters.X` se hace cuando se
quiera, y al terminar se borra el envoltorio. Sin `[Obsolete]`: evitaría
18 avisos en la consola de Arrows. Si se prefiere el aviso, es un cambio de una
línea en la plantilla.

Diferencias de comportamiento que verá Arrows tras migrar, todas de §2:
valores de caché antes del fetch, `OnReady` también cuando el fetch falla, y
conversiones que ya no lanzan.

## 8. Editor

### 8.1 Viva > Remote Config > Parameters

- Barra: **Save**, **Revert**, **Import from code**, **Setup**. Debajo, la
  ruta del JSON y el estado del fichero generado: "up to date", "outdated"
  o "missing", con botón **Regenerate**.
- Lista (`ReorderableList`) con una fila por parámetro: clave, tipo (popup),
  valor por defecto con el campo del tipo (`IntField`, `LongField`,
  `FloatField`, `DoubleField`, `Toggle`, `TextField`; para `json` un `TextArea`
  de varias líneas con "valid" o el error y su posición debajo) y descripción.
- Las filas inválidas se marcan en rojo y **Save** queda deshabilitado con el
  motivo. Al cambiar el tipo se conserva el texto del default y se revalida.
- **Save**: escribe el JSON, regenera el `.cs`, `AssetDatabase.Refresh()` y
  `RequestScriptCompilation()`. Aviso de cambios sin guardar al cerrar, como en
  `AnalyticsEventEditor`.

### 8.2 Viva > Remote Config > Setup

Calcada de `AnalyticsSetupWindow`:

- Filas de estado con OK/!: fichero de parámetros, fichero generado, SDK de
  Firebase Remote Config (`VIVA_FIREBASE_REMOTE_CONFIG`, botón Re-check),
  inicialización (§6.5), manager antiguo (§7).
- Ajustes: ruta del JSON, ruta del `.cs` generado, ruta del init. Guardados en
  `ProjectSettings/VivaRemoteConfigSettings.json`, dentro de `Assets/`.
- Primer arranque (`setupShown`): diálogo que ofrece crear el JSON vacío,
  generar la clase y enlazar la inicialización, o migrar si hay manager antiguo.

### 8.3 Detección del SDK

`RemoteConfigSdkDetector`, copia de `FirebaseSdkDetector` buscando
`Firebase.RemoteConfig.dll` entre los assemblies precompilados y manteniendo
`VIVA_FIREBASE_REMOTE_CONFIG` con `ScriptingDefines` del core.

## 9. Decisiones de la revisión (2026-09-07)

1. `OnReady` se dispara siempre que termina el primer intento de fetch, con
   éxito o sin él. Sin opción para el comportamiento antiguo: `Source` y
   `WhenReady` cubren los casos.
2. Los getters devuelven la caché de la sesión anterior antes del fetch. La
   documentación de estrategias de carga lo respalda (§1), y si no hubiera
   nada cacheado el resultado es `Defaults`, nunca peor que hoy. La prueba en
   dispositivo de §10 lo verifica sin red.
3. Envoltorio de compatibilidad sin `[Obsolete]`.
4. Menús: Viva > Remote Config > Parameters y Setup.

5. Inicialización compartida en el core (§6.6): aprobada el 2026-09-08 con la
   condición de verificar que cada módulo se inicializa bien y cuando toca.
   Implica publicar core 2.1.0 y analytics 2.1.0 junto con Remote Config 1.0.0.

## 10. Tests (EditMode)

- `RemoteConfigValidatorTests`: claves válidas e inválidas, cada tipo, unicidad
  de claves e identificadores.
- `JsonSyntaxValidatorTests`: casos válidos (anidado, escapes, unicode,
  exponentes) e inválidos (coma final, comillas simples, `NaN`, sin cerrar).
- `RemoteConfigCodeGeneratorTests`: fichero esperado para un conjunto de
  ejemplo; escapado de comillas, barras y saltos de línea; lista vacía;
  claves → identificadores.
- `RemoteConfigConverterTests`: los strings de bool del SDK, números con
  cultura, rango de `int`, fallos → default.
- `RemoteConfigServiceTests` con un proveedor falso: getters antes de
  `Initialize`, `StaticValue`, `WhenReady` antes y después, `OnReady` una vez,
  `Initialize` doble.
- `LegacyRemoteConfigImporterTests`: fixture con las formas de llamada de
  Arrows (literal, constante, default no literal, `GetJson<T>`).

Compilación fuera de Unity con `compile-check.sh`, y prueba real en Arrows:
instalar, importar desde código, guardar, migrar el manager, compilar, y en
dispositivo comprobar en el log `Source` y los valores tras `OnReady`, con red
y sin red.

## 11. Orden de implementación

1. Core 2.1.0: `VivaFirebase`, `FirebaseAppDetector`, `RequiredModules` en el
   instalador. Analytics 2.1.0: tracker sobre `VivaFirebase`. Compilar y probar
   en Arrows antes de seguir: debe comportarse igual que hoy.
2. Runtime de Remote Config: servicio, opciones, conversor, proveedor local y tests.
3. Proveedor de Firebase sobre `VivaFirebase`, asmdef con define, detector.
4. Editor: settings, modelo JSON, validadores, generador y tests.
5. Ventanas Parameters y Setup, primer arranque.
6. Importador y migración con envoltorio, plantillas.
7. README y CHANGELOG de cada paquete tocado, línea en el catálogo del core, README raíz.
8. `compile-check.sh`, prueba en Arrows, releases `core/v2.1.0`, `analytics/v2.1.0` y `remoteconfig/v1.0.0`.

## 12. Estado de la implementación (2026-09-08)

Hecho, todo en el working tree del repo sin commit (Jesús decide cuándo):

- Pasos 1 a 7 completos. Diferencias respecto a lo escrito arriba, todas
  recogidas ya en las secciones correspondientes: namespace `Viva.Core` para
  `VivaFirebase`; el tracker de analíticas mantiene su comprobación propia bajo
  `#if !VIVA_FIREBASE`; en el editor el modelo se llama
  `RemoteConfigParameterDefinition` (+ `RemoteConfigParameterFile`,
  `RemoteConfigParameterTypes`), la lógica de ficheros compartida por las dos
  ventanas está en `RemoteConfigProjectFiles`, y la conversión clave →
  identificador en `RemoteConfigNaming`. El servicio guarda sus propios
  defaults y los sirve aunque el proveedor no conozca la clave o falle.
- Verificado con Roslyn (`compile-check.sh`): los 16 assemblies compilan, el
  tracker en sus dos variantes (con y sin `VIVA_FIREBASE`) y el proveedor de
  Firebase contra las DLL 13.10 de Arrows.
- Verificado en Unity batchmode (paso 1): 20 tests EditMode (12 de `ReadyGate`
  + 8 de consent) y 1 PlayMode de integración con Firebase real en el editor:
  dos módulos, una sola comprobación, avisos en el hilo principal. La primera
  carga sin `VIVA_FIREBASE` compila y el detector lo define solo.

Pendiente:

- Tests EditMode del módulo Remote Config: Jesús los ejecutó desde el Test
  Runner el 2026-09-08; todos en verde salvo
  `SameKeyInSeveralFiles_MergesCallSites_AndFlagsConflicts`, que destapó que
  el importador comparaba también el default de una lectura con tipo distinto
  y duplicaba el conflicto. Corregido en el importador (con tipo distinto solo
  se anota el conflicto de tipo) y reproducido con el mismo fixture fuera de
  Unity. Tras la corrección Jesús relanzó la suite: **todos los tests en
  verde** (2026-09-08). Queda por ejecutar el PlayMode
  `FirebaseAnalyticsTrackerPlayModeTests` (tracker sobre el gate) si no entró
  en esa ejecución:
  `Unity.exe -batchmode -nographics -projectPath <proyecto> -runTests -testPlatform PlayMode -testResults r.xml -logFile l.txt`
  (con el editor cerrado) o la pestaña PlayMode del Test Runner.
- Prueba en Arrows: instalar core 2.1 + analytics 2.1 + remoteconfig 1.0 desde
  la rama, Import from code (esperados 10 claves, 4 sin default literal),
  completar defaults, Save, Migrate, compilar, y en dispositivo comprobar en el
  log `[Viva] Checking Firebase dependencies (requested by Viva Analytics)...`
  una sola vez, `[RemoteConfig] Values available from Cache|Defaults` y
  `[RemoteConfig] Ready. Values from Remote`, con red y sin red.
- Releases (tags) cuando Jesús lo pida.
