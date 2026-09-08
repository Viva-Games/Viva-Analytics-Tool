# Módulo Viva Ads — Diseño

Fecha: 2026-09-08
Repo: `Viva-Games/Viva-Analytics-Tool`, rama `develop`
Estado: aprobada el 2026-09-08 (decisiones en §11); en implementación.

## Objetivo

Un módulo `com.vivagames.ads`, instalable desde Viva > Package Installer, que
haga de la integración de anuncios con AppLovin MAX algo que cualquier dev del
estudio implemente sin errores: el juego solo declara qué formatos y placements
tiene y llama a `ShowRewarded`, `TryShowInterstitial` o `ShowBanner`. Todo lo
demás, inicialización, consentimiento, carga, recarga, reintentos, protección
del juego mientras hay un anuncio y qué hacer cuando no lo hay, lo lleva el
módulo.

Tres piezas, como en Remote Config:

1. **Runtime.** `AdsService`, fachada estática sobre un manager interno, con
   proveedor de MAX activado por un define autodetectado y proveedor vacío
   cuando el SDK no está. Incluye el componente `RewardedAdButton`.
2. **Ventana de ad units.** Viva > Ads > Ad Units: se añaden los formatos que
   usa el juego, Rewarded, Interstitial o Banner, con su ad unit de Android y
   de iOS y su lista de placements. Se guarda en un JSON del proyecto.
3. **Código generado.** `AdPlacements.cs` con un enum por formato y la
   configuración que consume `AdsService.Initialize`.

El usuario sigue instalando el plugin de MAX y configurando su Integration
Manager: SDK key, adaptadores de mediación y flujo de consentimiento. Setup
comprueba que esa parte está hecha y avisa de lo que falta.

Punto de partida: `Assets/Scripts/Game/Systems/AdManager.cs` y
`ConsentBridge.cs` de Arrows, en producción. Lo que se conserva y lo que
cambia está en §2.

## Fuera de alcance (1.0)

- MREC, App Open y nativos. El diseño deja hueco para añadirlos como formatos.
- Un ad unit distinto por placement. En MAX el ad unit va por formato y
  plataforma y el placement es una etiqueta de reporting; Arrows funciona así.
- Reglas de interstitial que dependen del juego: nivel mínimo, tutorial,
  primer día. Las decide el código del juego antes de llamar (§6.4).
- Segmentos de MAX, waterfall tests, Ad Review.
- Instalar MAX o sus adaptadores. Como con Firebase, lo hace el proyecto.

## 1. Hechos comprobados

Fuentes: código fuente del plugin MAX 8.6.2 instalado en Arrows
(`Library/PackageCache/com.applovin.mediation.ads@…/Scripts`), el
`.unitypackage` 8.6.5 de la última release de GitHub (listado sin instalar),
la guía de integración y la del flujo de consentimiento de AppLovin, y el
código de Arrows.

| Hecho | Fuente |
|---|---|
| El plugin se distribuye por el registro UPM de AppLovin (`https://unity.packages.applovin.com`, paquete `com.applovin.mediation.ads`, que es como lo tiene Arrows) y por `.unitypackage`. **Las dos vías traen el assembly `MaxSdk.Scripts`** (`Scripts/MaxSdk.Scripts.asmdef` en el paquete, `Assets/MaxSdk/Scripts/MaxSdk.Scripts.asmdef` en el unitypackage). Un paquete puede referenciarlo por nombre. | Package cache de Arrows; `.unitypackage` 8.6.5 |
| No hay DLL precompilada: el SDK son fuentes. La detección se hace por la presencia del assembly `MaxSdk.Scripts` en la compilación, no por una DLL. | Ídem |
| API 8.6.2: `InitializeSdk(string[] adUnitIds = null)`, `IsInitialized()`, `GetSdkConfiguration()`; `LoadRewardedAd`, `IsRewardedAdReady`, `ShowRewardedAd(adUnitId, placement = null, customData = null)`; ídem interstitial; `CreateBanner(adUnitId, BannerPosition)` y `CreateBanner(adUnitId, AdViewConfiguration)` con `IsAdaptive`, `ShowBanner`, `HideBanner`, `DestroyBanner`, `SetBannerPlacement`, `SetBannerBackgroundColor`, `SetBannerExtraParameter`, `StartBannerAutoRefresh`/`StopBannerAutoRefresh`; `SetTestDeviceAdvertisingIdentifiers`, `ShowMediationDebugger`, `SetExtraParameter`. | `MaxSdkAndroid.cs`, `MaxSdkiOS.cs`, `MaxSdk.cs` |
| `CreateBanner` carga el primer banner y arranca el auto-refresh; `LoadBanner` solo hace falta si se pausó el refresh. El banner no se ve hasta `ShowBanner`. | Comentario de `LoadBanner` en `MaxSdkAndroid.cs` |
| Callbacks por formato en `MaxSdkCallbacks.Rewarded/Interstitial/Banner`: `OnAdLoadedEvent`, `OnAdLoadFailedEvent(adUnit, ErrorInfo)`, `OnAdDisplayedEvent`, `OnAdDisplayFailedEvent(adUnit, ErrorInfo, AdInfo)`, `OnAdClickedEvent`, `OnAdRevenuePaidEvent(adUnit, AdInfo)`, `OnAdHiddenEvent`, `OnExpiredAdReloadedEvent`; en rewarded además `OnAdReceivedRewardEvent(adUnit, Reward, AdInfo)`; en banner `OnAdExpandedEvent`/`OnAdCollapsedEvent`. `MaxSdkCallbacks.OnSdkInitializedEvent(SdkConfiguration)`. | `MaxSdkCallbacks.cs` |
| `ErrorInfo.Code`: `NoFill = 204`, `AdLoadFailed = -5001`, `AdDisplayFailed = -4205`, `NetworkError = -1000`, `NetworkTimeout = -1001`, `NoNetwork = -1009`, `FullscreenAdAlreadyShowing = -23`, `FullscreenAdNotReady = -24`, `Unspecified = -1`. Los tres códigos de red permiten distinguir "sin internet" de "sin anuncio". | `MaxSdkBase.cs` |
| `AdInfo`: `AdUnitIdentifier`, `AdFormat`, `NetworkName`, `NetworkPlacement`, `Placement`, `CreativeIdentifier`, `Revenue`, `RevenuePrecision`, `DspName`, `LatencyMillis`. Lo que Arrows manda a `ad_impression` y a Singular. | `MaxSdkBase.cs` |
| El flujo de consentimiento de MAX (Google UMP en regiones GDPR y prompt ATT en iOS) corre **dentro de `InitializeSdk`**; `OnSdkInitializedEvent` llega cuando ha terminado. AppLovin pide inicializar MMPs y analíticas en ese callback, no antes, para que tengan acceso al identificador. `SdkConfiguration.ConsentFlowUserGeography` ∈ `Unknown`, `Gdpr`, `Other`; `AppTrackingStatus` en iOS. | Guía "Terms and Privacy Policy Flow"; `MaxSdkBase.cs` |
| TCF tras la inicialización: `MaxSdkUtils.GetPurposeConsentStatus(id)`, `GetTcfConsentStatus(vendorId)`, `GetAdditionalConsentStatus(atpId)` devuelven `bool?` (null sin datos). Re-consentimiento: `MaxSdk.CmpService.ShowCmpForExistingUser(Action<MaxCmpError>)` y `HasSupportedCmp`. | `MaxSdkUtils.cs`, `MaxCmpService.cs`, Arrows `ConsentBridge` y `SettingsMenu` |
| Reintento de carga recomendado por AppLovin: backoff exponencial `2^min(6, intentos)` segundos, es decir hasta 64 s. Es lo que hace Arrows. | Guías de rewarded e interstitial; `AdManager.cs` |
| `MaxSdkUtils` no tiene comprobación de red; Arrows usa `Application.internetReachability`. | `MaxSdkUtils.cs` |
| En el editor el plugin muestra anuncios de prueba (`MaxSdkUnityEditor.cs`, prefabs `Rewarded`, `Interstitial`, `BannerTop/Bottom`): se puede probar el flujo completo en Play Mode sin dispositivo. | Fuentes del plugin |

## 2. Qué se conserva de Arrows y qué cambia

| Aspecto | Arrows hoy | Módulo | Por qué |
|---|---|---|---|
| Un solo manager | `AdManager` MonoBehaviour singleton, `PlayingAd` consultado desde 12 sitios | `AdsService` estático sobre un `AdsRuntime` interno (MonoBehaviour oculto para corrutinas y `OnApplicationPause`) | Misma idea, API estática coherente con los otros módulos |
| Momento de inicialización | `Start` espera a que termine el permiso de notificaciones y luego inicializa MAX | `AdsInit` (fichero del usuario) llama a `AdsService.Initialize(...)` cuando el juego quiera; la plantilla lo hace en `Start` y enseña cómo esperar a otro prompt | El orden de prompts es del juego |
| Timeout de inicialización | 30 s; sin red no espera | Igual, opción `InitializationTimeoutSeconds`. Al vencer, se piden las cargas igualmente: reintentan cuando el SDK esté | Arrows lo sufrió en producción |
| Ad units | Constantes por plataforma en código | Por formato y plataforma en la ventana; `InitializeSdk` recibe la lista | Sin editar código |
| Placements | Enum + array de strings paralelo | Enum generado por formato desde la ventana | Sin desincronizar |
| Carga y recarga | Precarga al inicializar; recarga tras cada cierre o fallo de display; backoff `2^min(6,n)` | Igual, por formato | Es la recomendación de AppLovin |
| Un anuncio a la vez | `PlayingAd` | `AdsService.IsShowingAd` + resultado `AlreadyShowing` | Igual |
| Mientras se muestra | `AudioListener.pause` y `EventSystem` desactivado | Opciones `PauseAudioWhileShowing` y `BlockUiInputWhileShowing`, activas por defecto | Es lo que evita los dobles taps |
| Reward tardío | Espera 0,5 s tras `OnAdHidden` | Opción `RewardGraceSeconds`, 0,5 por defecto | Hay redes que avisan después de cerrar |
| Cierre sin callback | Al volver de segundo plano, si en 1 s no llega `OnAdHidden`, libera | Opción `CloseWatchdogSeconds`, 1 por defecto | El jugador nunca se queda atrapado |
| Resultado del rewarded | `Success`, `Failed`, `Error` sin motivo | `RewardedResult` con motivo: `Rewarded`, `NotRewarded`, `DisplayFailed`, `NotReady`, `NoInternet`, `NotInitialized`, `AlreadyShowing`, `InvalidPlacement` | La UI decide con el motivo real |
| No disponible | Cada menú sondea `IsRewardedReady()` por frame y pinta "sin conexión" aunque la causa sea otra | `AdStatus` por formato derivado de los callbacks y los códigos de error, evento de cambio, `RewardedAdButton` y espera opcional de carga (§6.3) | Cero sondeo, causa real |
| Política de interstitials | Nivel mínimo, temporizador y saltar tras un rewarded, con valores de Remote Config, dentro del manager | En el módulo solo lo global: tiempo entre interstitials y saltar el siguiente tras un rewarded (§6.4). El nivel mínimo lo decide el juego antes de llamar | Decisión de Jesús |
| Consentimiento | `ConsentBridge` lee el TCF de MAX y llama a `AnalyticsService.SetConsent` y a Facebook | El módulo publica el TCF en `VivaConsent` (core 2.2.0) y analíticas 2.2.0 lo recibe sola. `AdsService.OnConsentResolved` queda para otros SDK, como las banderas de Facebook (§6.6) | Los módulos no se referencian entre sí; el core hace de puente, como con Firebase |
| Ingresos | `OnAdRevenuePaid` → `AdImpression.Track` + Singular | `AdsService.OnAdRevenue`; la plantilla trae `AdImpression.Track` si hay analíticas | Ídem |
| Banner | No hay | `ShowBanner`/`HideBanner`, posición, adaptativo, color de fondo | Formato nuevo |
| Debug | `InterstitialsEnabled` y botón del mediation debugger | `AdsOptions.InterstitialsEnabled` y `AdsService.ShowMediationDebugger()` | Igual |
| Toggle de audio Android | `SetExtraParameter("return_audio_focus", "true")` | Opción `ReturnAudioFocus`, activa por defecto | Igual |

## 3. Estructura del paquete

```
Packages/com.vivagames.ads/
  package.json                      com.vivagames.ads 1.0.0, "Viva Ads", unity 2021.3
  README.md, CHANGELOG.md
  Runtime/
    VivaGames.Ads.asmdef            rootNamespace Viva.Services.Ads; refs VivaGames.Core, UnityEngine.UI
    AdsService.cs                   fachada estática y máquina de estados
    AdsOptions.cs
    AdsEnums.cs                     AdFormat, AdsState, AdStatus, RewardedResult, InterstitialResult, BannerPosition
    AdUnitConfiguration.cs          lo que genera AdPlacements.Configuration()
    AdRevenueInfo.cs, AdsConsentInfo.cs
    IAdsProvider.cs                 contrato con el SDK
    NoAdsProvider.cs                sin SDK: todo "no disponible", con aviso
    AdsRuntime.cs                   MonoBehaviour oculto: corrutinas, OnApplicationPause, EventSystem, audio
    UI/RewardedAdButton.cs
    UI/PlacementNameAttribute.cs    para el desplegable del inspector
  Runtime/MaxSdk/
    VivaGames.Ads.MaxSdk.asmdef     defineConstraints VIVA_APPLOVIN_MAX; refs VivaGames.Ads, MaxSdk.Scripts
    MaxAdsProvider.cs               + registro del proveedor al cargar
  Editor/
    VivaGames.Ads.Editor.asmdef     refs VivaGames.Ads, VivaGames.Core.Editor
    AdsPackage.cs, AdsEditorSettings.cs
    MaxSdkDetector.cs               assembly MaxSdk.Scripts -> VIVA_APPLOVIN_MAX
    AdUnitsFile.cs                  modelo + JSON
    AdsValidator.cs, AdsCodeGenerator.cs, AdsNaming.cs (misma regla que Remote Config)
    AdUnitsWindow.cs                Viva > Ads > Ad Units
    AdsSetupWindow.cs               Viva > Ads > Setup
    AdsProjectFiles.cs
    PlacementNameDrawer.cs
  Templates~/
    AdsInit.cs.txt
  Tests/Editor/ ... (§10)
```

Cambios fuera del paquete:

- `com.vivagames.core` → 2.2.0: `VivaConsent` y `ConsentState` en el assembly
  de runtime `VivaGames.Core` (§6.6), con tests. Línea de `VivaModuleCatalog`
  para Ads con `tagPrefix: "ads"` y `requiredModules: core >= 2.2.0,
  analytics >= 2.2.0 si está instalado`; analytics pasa a requerir
  `core >= 2.2.0`.
- `com.vivagames.analytics` → 2.2.0: `AnalyticsService` se suscribe a
  `VivaConsent` al inicializarse y aplica el consentimiento con
  `ConsentModeMapper` sin código del usuario (§6.6). El assembly de runtime
  referencia `VivaGames.Core`. La API pública no cambia; `SetConsent` sigue
  disponible para proyectos con su propio CMP.
- README raíz: fila del módulo. Remote Config no cambia.

## 4. Fichero de configuración (fuente de verdad)

`Assets/VivaAds/AdUnits.json`, ruta configurable en Setup:

```json
{
  "rewarded": {
    "enabled": true,
    "androidAdUnitId": "f760b9e92755eb16",
    "iosAdUnitId": "",
    "placements": ["hint", "continue", "daily_recharge"]
  },
  "interstitial": {
    "enabled": true,
    "androidAdUnitId": "11fd751ba36b110d",
    "iosAdUnitId": "",
    "placements": ["level_start", "level_replay"]
  },
  "banner": {
    "enabled": false,
    "androidAdUnitId": "",
    "iosAdUnitId": "",
    "placements": ["main_menu"],
    "position": "BottomCenter",
    "adaptive": true,
    "backgroundColor": "#000000"
  }
}
```

Validación al guardar:

| Campo | Regla |
|---|---|
| formato activo | al menos un ad unit (Android o iOS). Si falta una plataforma, aviso, no error: el juego puede ser de una sola |
| ad unit | 16 caracteres hexadecimales, que es el formato de MAX; si no, aviso |
| placement | `^[A-Za-z0-9_]+$`, único dentro del formato, y su identificador C# válido y único; recomendación snake_case, no obligatoria. Se pasa a MAX tal cual |
| formato activo sin placements | error: hace falta al menos uno, porque la API los exige |
| banner | posición del enum, color hex válido |

Placement con ad unit propio: no en 1.0 (§Fuera de alcance).

## 5. Código generado

`Assets/VivaAds/AdPlacements.cs`, regenerado entero en cada guardado:

```csharp
// <auto-generated> ... </auto-generated>
namespace Viva.Services.Ads
{
    public enum RewardedPlacement { Hint, Continue, DailyRecharge }
    public enum InterstitialPlacement { LevelStart, LevelReplay }
    public enum BannerPlacement { MainMenu }

    public static class AdPlacements
    {
        public static AdUnitConfiguration Configuration() => new AdUnitConfiguration
        {
            Rewarded = new AdUnitSetup("f760b9e92755eb16", "", typeof(RewardedPlacement), new[] { "hint", "continue", "daily_recharge" }),
            Interstitial = new AdUnitSetup("11fd751ba36b110d", "", typeof(InterstitialPlacement), new[] { "level_start", "level_replay" }),
            Banner = null, // formato no activo
        };
    }
}
```

Solo se generan los enums de los formatos activos. El nombre de cada valor sale
de la regla de Remote Config (`daily_recharge → DailyRecharge`). El orden del
enum es el de la lista, y la configuración registra el nombre de placement de
cada valor por su ordinal.

**Por qué la API del runtime recibe `Enum` y no el enum concreto.** El enum se
genera en Assets y `AdsService` vive en el paquete, que no puede conocerlo.
`ShowRewarded(Enum placement, ...)` acepta `RewardedPlacement.Hint` tal cual y
el servicio comprueba en el acto que el tipo es el registrado para ese formato;
si se pasa un placement de otro formato, error en consola y resultado
`InvalidPlacement`. Se pierde la comprobación en compilación entre formatos, a
cambio de que el servicio, la documentación y los tests sean los del paquete
para todos los proyectos. Hay sobrecargas con `string` para casos dinámicos.

## 6. Runtime

### 6.1 API pública

```csharp
namespace Viva.Services.Ads
{
    public enum AdFormat { Rewarded, Interstitial, Banner }
    public enum AdsState { NotInitialized, Initializing, Ready, Unavailable }   // el SDK
    public enum AdStatus { Disabled, NotInitialized, NoInternet, Loading, Ready, Showing }  // un formato
    public enum RewardedResult { Rewarded, NotRewarded, DisplayFailed, NotReady, NoInternet, NotInitialized, AlreadyShowing, InvalidPlacement }
    public enum InterstitialResult { Closed, DisplayFailed, NotReady, NoInternet, NotInitialized, AlreadyShowing, InvalidPlacement }
    public enum BannerPosition { TopLeft, TopCenter, TopRight, Centered, CenterLeft, CenterRight, BottomLeft, BottomCenter, BottomRight }

    public sealed class AdsOptions
    {
        public float InitializationTimeoutSeconds = 30f;
        public bool PauseAudioWhileShowing = true;
        public bool BlockUiInputWhileShowing = true;
        public float RewardGraceSeconds = 0.5f;
        public float CloseWatchdogSeconds = 1f;
        public int MaxRetryExponent = 6;                       // 2^6 = 64 s
        public float InterstitialCooldownSeconds = 0f;         // 0 = sin espera. Arrows: 125
        public bool InterstitialCooldownStartsAtInitialize = true;
        public bool SkipNextInterstitialAfterRewarded = true;  // semántica de Arrows
        public bool InterstitialsEnabled = true;               // toggle de debug
        public bool ReturnAudioFocus = true;
        public string[] TestDeviceAdvertisingIds;
        public bool LogToConsole = true;
    }

    public static class AdsService
    {
        // Estado del SDK
        public static AdsState State { get; }
        public static bool IsReady { get; }                    // State == Ready
        public static bool IsShowingAd { get; }
        public static event Action OnInitialized;              // una vez, termine como termine (mira State)
        public static void WhenInitialized(Action callback);
        public static event Action<AdsConsentInfo> OnConsentResolved;
        public static event Action<AdRevenueInfo> OnAdRevenue;
        public static event Action<AdFormat, AdStatus> OnStatusChanged;
        public static event Action<bool> OnShowingAdChanged;

        public static void Initialize(AdUnitConfiguration configuration, AdsOptions options = null);

        // Rewarded
        public static AdStatus RewardedStatus { get; }
        public static bool IsRewardedReady { get; }
        public static event Action<bool> OnWaitingForRewarded;  // true mientras ShowRewarded espera una carga
        public static void ShowRewarded(Enum placement, Action<RewardedResult> onResult, float waitForLoadSeconds = 0f);
        public static void ShowRewarded(string placement, Action<RewardedResult> onResult, float waitForLoadSeconds = 0f);

        // Interstitial
        public static AdStatus InterstitialStatus { get; }
        public static bool IsInterstitialReady { get; }
        public static bool CanShowInterstitial { get; }        // listo y la política lo permite
        public static float InterstitialCooldownRemaining { get; }
        public static bool TryShowInterstitial(Enum placement, Action onClosed = null);   // aplica la política
        public static void ShowInterstitial(Enum placement, Action<InterstitialResult> onResult = null); // la ignora
        public static void ResetInterstitialCooldown();
        public static void SkipNextInterstitial();             // por si el juego da una recompensa por otra vía

        // Banner
        public static AdStatus BannerStatus { get; }
        public static bool IsBannerVisible { get; }
        public static void ShowBanner(Enum placement);
        public static void HideBanner();
        public static void SetBannerPosition(BannerPosition position);

        // Consentimiento y utilidades
        public static bool HasConsentDialog { get; }           // CmpService.HasSupportedCmp
        public static void ShowConsentDialog(Action<bool> onCompleted);   // ShowCmpForExistingUser; recalcula OnConsentResolved
        public static void ShowMediationDebugger();
    }

    public sealed class AdsConsentInfo
    {
        public bool IsGdpr;                 // ConsentFlowUserGeography == Gdpr
        public string Geography;            // Unknown, Gdpr, Other
        public bool? Purpose1, Purpose3, Purpose4, Purpose7, GoogleVendor;   // TCF; null sin datos
        public string AppTrackingStatus;    // iOS
    }

    public sealed class AdRevenueInfo
    {
        public AdFormat Format; public string Placement, AdUnitId, NetworkName, NetworkPlacement, CreativeId, RevenuePrecision;
        public double Revenue; public string Currency = "USD";
    }
}
```

### 6.2 Inicialización

`AdsService.Initialize(configuration, options)`:

1. Elige proveedor: `MaxAdsProvider` si `VIVA_APPLOVIN_MAX`, registrado por
   `[RuntimeInitializeOnLoadMethod]` como en Remote Config; si no,
   `NoAdsProvider`, que deja todo en `Disabled`, contesta a cada `Show` con
   `NotInitialized` y avisa una vez en consola. Sin SDK el juego compila y corre.
2. Crea `AdsRuntime` (`DontDestroyOnLoad`, oculto). `State = Initializing`.
3. Proveedor de MAX: suscribe todos los callbacks, `SetTestDeviceAdvertisingIdentifiers`
   si hay, `SetExtraParameter("return_audio_focus", "true")` si la opción está,
   `InitializeSdk(adUnits)` con los ad units de los formatos activos. Arranca
   el temporizador de `InitializationTimeoutSeconds`, que sin red es cero.
4. `OnSdkInitializedEvent` → `State = Ready`, lee el TCF (§6.6) y dispara
   `OnConsentResolved`; luego `OnInitialized`. Si vence el timeout →
   `State = Unavailable` y `OnInitialized` igualmente; si el SDK termina
   después, `State` pasa a `Ready` y `OnConsentResolved` se dispara entonces.
5. En ambos casos se piden las cargas de los formatos activos y se crea el
   banner (oculto). Las cargas que fallen reintentan con backoff.

`OnInitialized` es lo que espera el menú para soltar el botón de jugar, como
hoy `OnConsentCompleted` en Arrows. `WhenInitialized` ejecuta en el acto si ya
ha pasado.

Segunda llamada a `Initialize`: aviso y no-op.

### 6.3 Rewarded y el problema de "no disponible"

Estado por formato, derivado de los callbacks sin sondear al SDK:

| Situación | `AdStatus` |
|---|---|
| Formato no activo en la ventana | `Disabled` |
| Antes de `Initialize` o SDK no listo | `NotInitialized` |
| Carga pedida y sin anuncio, último error de red (`NoNetwork`, `NetworkError`, `NetworkTimeout`) o `Application.internetReachability == NotReachable` | `NoInternet` |
| Carga pedida y sin anuncio por otro motivo (`NoFill`, primer intento) | `Loading` |
| `OnAdLoaded` (o `OnExpiredAdReloaded`) | `Ready` |
| Entre `Show` y el cierre | `Showing` |

`OnStatusChanged(format, status)` se dispara en cada cambio. `IsRewardedReady`
confirma además con `MaxSdk.IsRewardedAdReady` antes de mostrar.

`ShowRewarded(placement, onResult, waitForLoadSeconds)`:

1. Placement inválido → `InvalidPlacement`. SDK no listo → `NotInitialized`.
   Otro anuncio en pantalla → `AlreadyShowing`. Todo en el acto.
2. Sin anuncio: si `waitForLoadSeconds > 0`, el estado es `Loading` y hay red,
   espera hasta que llegue `Ready` o venza el plazo, con
   `OnWaitingForRewarded(true/false)` para que la UI ponga un spinner. Si no
   hay que esperar o vence el plazo → `NoInternet` o `NotReady` según el estado.
3. Con anuncio: `IsShowingAd = true`, pausa audio, bloquea `EventSystem`,
   `ShowRewardedAd(adUnit, placementName)`.
4. `OnAdReceivedReward` marca la recompensa. `OnAdHidden` espera
   `RewardGraceSeconds` y resuelve `Rewarded` o `NotRewarded`;
   `OnAdDisplayFailed` resuelve `DisplayFailed`. En todos los casos se
   restaura audio e input, se recarga y se dispara `onResult` una sola vez.
5. Si al volver de segundo plano sigue `IsShowingAd` y en
   `CloseWatchdogSeconds` no ha llegado el cierre, resuelve `DisplayFailed`.

Tres capas para la UI, de menos a más código:

- **Estado y evento**: `RewardedStatus` y `OnStatusChanged`. Cualquier UI puede
  pintar "cargando", "sin conexión" o habilitar el botón sin `Update`.
- **`RewardedAdButton`** (componente sobre un `Button`), campos en el inspector:
  placement (desplegable con los de la ventana, gracias a
  `PlacementNameAttribute` + drawer), `UnavailableBehaviour`
  (`DisableButton` o `KeepInteractable`), objetos opcionales a mostrar cuando
  está listo, cargando o sin internet, `WaitForLoadSeconds`, y los eventos
  `OnRewarded`, `OnNotRewarded` y `OnUnavailable(AdStatus)` como UnityEvents y
  como eventos C#. Al pulsar llama a `ShowRewarded` y se deshabilita mientras
  se muestra. Con `KeepInteractable`, pulsar sin anuncio dispara
  `OnUnavailable` con el motivo, para el popup del juego.
- **`ShowRewarded` nunca falla en silencio**: el callback siempre llega con el
  motivo, y `waitForLoadSeconds` cubre el patrón "toca, espera unos segundos,
  y si no hay anuncio avisa".

### 6.4 Interstitial y su política

`TryShowInterstitial(placement, onClosed)` devuelve true solo si lanza el
anuncio, y entonces `onClosed` llega al cerrarse o al fallar el display, nunca
de forma espuria. Devuelve false, sin llamar a `onClosed`, cuando:

- `InterstitialsEnabled` es false (toggle de debug).
- No han pasado `InterstitialCooldownSeconds` desde el último interstitial. El
  temporizador arranca en `Initialize` si `InterstitialCooldownStartsAtInitialize`
  (Arrows: la sesión empieza contando) y se reinicia al cerrarse un interstitial.
- `SkipNextInterstitialAfterRewarded` y el jugador ha completado un rewarded
  desde la última comprobación. Es exactamente la semántica de Arrows: la marca
  se pone al recibir la recompensa y se consume en la siguiente llamada a
  `TryShowInterstitial`, tenga el resultado que tenga.
- No hay anuncio listo o hay otro en pantalla.

Lo que depende del juego, nivel mínimo, tutorial, día de instalación, va antes
de llamar, y así lo enseña el README con el ejemplo de Arrows:

```csharp
bool eligible = level > RemoteConfigParameters.StartInterstitialsLevel;
if (!eligible || !AdsService.TryShowInterstitial(InterstitialPlacement.LevelStart, StartLevel))
    StartLevel();
```

`ShowInterstitial(placement, onResult)` muestra sin política, con
`InterstitialResult` para quien quiera el detalle. `CanShowInterstitial` y
`InterstitialCooldownRemaining` sirven para depurar y para UI.

### 6.5 Banner

Al inicializar, si el formato está activo: `CreateBanner(adUnit,
AdViewConfiguration)` con la posición y `IsAdaptive` de la ventana, color de
fondo, y queda oculto. `ShowBanner(placement)` hace `SetBannerPlacement` y
`ShowBanner`; `HideBanner` lo oculta; `SetBannerPosition` lo reposiciona. El
auto-refresh lo lleva MAX. Estado `BannerStatus` con las mismas reglas, e
`IsBannerVisible`. Los ingresos del banner llegan por `OnAdRevenue` como los
demás.

### 6.6 Consentimiento compartido (core 2.2.0 y analytics 2.2.0)

Decisión de Jesús: si el proyecto tiene Viva Analytics, el consentimiento del
CMP de MAX le tiene que llegar sin ninguna línea del usuario. Como los módulos
no se referencian entre sí, el puente es el core, igual que `VivaFirebase`:

```csharp
namespace Viva.Core
{
    /// <summary>Estado TCF publicado por el módulo que tiene el CMP y consumido por los demás.</summary>
    public sealed class ConsentState
    {
        public bool IsGdpr;                                        // región bajo RGPD
        public bool? Purpose1, Purpose3, Purpose4, Purpose7;       // purposes IAB TCF; null = sin dato
        public bool? GoogleVendor;                                 // vendor 755
        public string Source;                                      // "AppLovin MAX", para el log
    }

    public static class VivaConsent
    {
        public static ConsentState Current { get; }                // null hasta que se resuelve
        public static bool IsResolved { get; }
        /// <summary>callback en el acto si ya está resuelto, y en cada cambio posterior (re-consentimiento). Hilo principal.</summary>
        public static void Subscribe(Action<ConsentState> callback);
        public static void Unsubscribe(Action<ConsentState> callback);
        /// <summary>Publica un consentimiento nuevo: Viva Ads tras el CMP de MAX, o el proyecto con su propio CMP.</summary>
        public static void Set(ConsentState state);
    }
}
```

- **Viva Ads publica.** Tras `OnSdkInitializedEvent`, y de nuevo al terminar
  `ShowConsentDialog`, el proveedor lee `GetSdkConfiguration().ConsentFlowUserGeography`,
  `AppTrackingStatus` y, con `MaxSdkUtils`, los purposes 1, 3, 4 y 7 y el
  vendor 755 de Google. Llama a `VivaConsent.Set(...)` y dispara
  `AdsService.OnConsentResolved(AdsConsentInfo)` con lo mismo más el estado ATT,
  para SDKs que no pasan por el core (las banderas de Facebook de Arrows).
- **Viva Analytics consume.** `AnalyticsService.Initialize` se suscribe una vez
  a `VivaConsent` y cada estado lo traduce con el `ConsentModeMapper` que ya
  existe y lo aplica con `SetConsent`, que el tracker de Firebase encola hasta
  que Firebase está listo. Es exactamente lo que hace hoy el `ConsentBridge`
  de Arrows, sin el `ConsentBridge`. Un proyecto con su propio CMP y sin Viva
  Ads sigue llamando a `AnalyticsService.SetConsent` o publica en
  `VivaConsent.Set`; las dos vías conviven porque la última llamada gana.
- **Orden.** Da igual quién se inicializa antes: `Subscribe` entrega el estado
  actual si ya existe, y `Set` avisa a los suscritos. Mismo contrato que
  `ReadyGate`, con la diferencia de que aquí puede haber varios cambios.
- Combinaciones: Ads 1.0 + Analytics 2.2 → automático. Ads 1.0 + Analytics
  2.1 → el instalador pide actualizar analíticas (requisito). Analytics 2.2 sin
  Ads → nada cambia respecto a hoy.

### 6.7 Ingresos

Cada `OnAdRevenuePaidEvent` de cualquier formato → `OnAdRevenue(AdRevenueInfo)`
con los campos de `AdInfo`. La plantilla lo lleva a `AdImpression.Track`.

## 7. Editor

### 7.1 Viva > Ads > Ad Units

Tres secciones, una por formato, como pidió Jesús: un botón **Add rewarded**,
**Add interstitial** y **Add banner**; cada sección activa muestra el ad unit
de Android y el de iOS, su lista de placements (ReorderableList, con Add
dentro de cada formato) y, en banner, posición, adaptativo y color. **Remove**
desactiva el formato conservando sus datos en el JSON. Save valida (§4),
escribe el JSON y regenera `AdPlacements.cs`. Mismo comportamiento de cambios
sin guardar, filas en rojo y estado del fichero generado que la ventana de
Remote Config.

### 7.2 Viva > Ads > Setup

Filas de estado:

- JSON de ad units y `AdPlacements.cs` (generado y al día).
- **MAX SDK**: detectado o no, vía (paquete UPM `com.applovin.mediation.ads`
  con su versión, o `Assets/MaxSdk`), y define `VIVA_APPLOVIN_MAX`. Si hay
  `Assets/MaxSdk/Scripts/MaxSdk.cs` sin el asmdef `MaxSdk.Scripts`, aviso:
  plugin demasiado antiguo, actualizar.
- **Integration Manager**: lee `Assets/MaxSdk/Resources/AppLovinSettings.asset`
  y avisa si falta la SDK key, si el flujo de consentimiento está desactivado o
  sin URL de política de privacidad, y si `MediationAdapters` no tiene ningún
  adaptador. Solo avisos con el nombre del campo a rellenar; no escribe en el
  asset de AppLovin.
- `AdsInit.cs` generado, y si Viva Analytics está instalado, si la plantilla
  incluyó los ganchos de consentimiento e ingresos.
- Botón **Run full setup** y primer arranque con diálogo, como en los otros
  módulos. Ajustes de rutas en `ProjectSettings/VivaAdsSettings.json`.

### 7.3 Detección del SDK

`MaxSdkDetector` (`[InitializeOnLoad]`): busca el assembly `MaxSdk.Scripts`
en `CompilationPipeline.GetAssemblies()` y mantiene `VIVA_APPLOVIN_MAX` con
`ScriptingDefines` del core. Vale para las dos vías de instalación (§1). Como
el plugin son fuentes, en la primera carga el assembly ya existe y el define se
aplica en el mismo arranque.

## 8. Plantilla `AdsInit.cs` y ganchos

Fichero del usuario en `Assets/VivaAds/AdsInit.cs`, MonoBehaviour para la
primera escena:

```csharp
public class AdsInit : MonoBehaviour
{
    private void Start()
    {
        // If another prompt must go first (notifications, ATT of your own), call Initialize when it finishes.
        AdsService.Initialize(AdPlacements.Configuration(), new AdsOptions
        {
            InterstitialCooldownSeconds = 125f,           // or RemoteConfigParameters.InterstitialsTimer
            // SkipNextInterstitialAfterRewarded = true, PauseAudioWhileShowing = true, ...
        });

        // Consent: with Viva Analytics installed nothing to do here, it receives the CMP result through Viva Core.
        // For SDKs outside the Viva modules (Facebook tracking flags, for instance):
        AdsService.OnConsentResolved += consent =>
        {
            // bool adStorage = !consent.IsGdpr || consent.Purpose1 == true;
            // FB.Mobile.SetAdvertiserTrackingEnabled(adStorage); ...
        };
        AdsService.OnAdRevenue += ad =>
        {
            // {{REVENUE_HOOK}}  ->  con el evento ad_impression en el proyecto: AdImpression.Track(...);
            // Singular, Adjust...: SingularSDK.AdRevenue(...)
        };
        AdsService.WhenInitialized(() =>
        {
            // MMPs and anything AppLovin asks to initialize after its consent flow.
        });
    }
}
```

Setup rellena el gancho de ingresos con código real cuando encuentra Viva
Analytics y `Assets/VivaAnalytics/Events/AdImpression.cs`, y con el ejemplo
comentado si no. Es la línea que Arrows tiene hoy en `TrackAdImpression`. El
consentimiento no necesita gancho: va por el core (§6.6).

## 9. Documentación

Requisito explícito de Jesús: el README del módulo documenta cada método y
evento públicos con su firma, qué devuelve y cuándo usarlo, con la misma
estructura que los de Analytics y Remote Config:

1. Qué hace el módulo, en tres líneas de código.
2. Instalación: MAX (UPM o unitypackage), Integration Manager (SDK key,
   adaptadores, flujo de consentimiento), core, instalador, primer arranque,
   `AdsInit` en la primera escena.
3. Declarar formatos y placements en la ventana; qué genera.
4. Inicialización: `Initialize`, `AdsOptions` en tabla, `OnInitialized`,
   `WhenInitialized`, `State`.
5. Rewarded: `ShowRewarded`, `RewardedResult` en tabla, `RewardedStatus`,
   `OnStatusChanged`, `RewardedAdButton` campo a campo, `waitForLoadSeconds`.
6. Interstitial: `TryShowInterstitial` y su contrato, la política y sus
   opciones, `ShowInterstitial`, el ejemplo del nivel mínimo.
7. Banner: `ShowBanner`, `HideBanner`, `SetBannerPosition`, posición y
   adaptativo.
8. Consentimiento e ingresos: los dos eventos y lo que la plantilla ya hace.
9. Sin el SDK: qué pasa. En el editor: anuncios de prueba de MAX.
10. Tabla completa de la API pública al final, como referencia.

## 10. Tests y verificación

- EditMode con un `FakeAdsProvider`: máquina de estados por formato,
  transición de `AdStatus` con cada código de error, `ShowRewarded` en todos
  sus caminos (inválido, no listo, sin red, espera de carga, recompensa
  tardía, display fallido, vigilante de cierre), política de interstitial
  (cooldown desde `Initialize`, reinicio al cerrar, salto tras rewarded que se
  consume una vez, toggle), banner, `OnInitialized` una vez, timeout, segunda
  `Initialize`, validador, naming, generador, fichero JSON.
- **MAX en el proyecto de desarrollo por UPM** (registro de AppLovin en
  `Packages/manifest.json` y `com.applovin.mediation.ads`): el plugin muestra
  anuncios de prueba en el editor, así que un test PlayMode puede inicializar,
  esperar `OnInitialized`, mostrar un rewarded de prueba y comprobar el
  resultado y los estados. Hay que decidir si se añade (§11).
- `compile-check.sh` con el assembly del plugin de Arrows para el proveedor.

## 11. Decisiones de la revisión (2026-09-08) y preguntas

Decididas por Jesús:

1. La fachada se llama `AdsService`, como el resto de módulos.
2. Entran en la 1.0 el estado consultable con evento y el `RewardedAdButton`.
3. Política de interstitials: solo lo global. Tiempo entre interstitials sí;
   nivel mínimo no, lo decide el juego. Saltar tras un rewarded se propone con
   la semántica exacta de Arrows, que sí es global: la marca se consume en la
   siguiente comprobación. Opción `SkipNextInterstitialAfterRewarded`.
4. Se admiten las dos instalaciones de MAX, UPM y unitypackage. Comprobado que
   ambas traen el assembly `MaxSdk.Scripts`.
5. La documentación de la API es parte del entregable (§9).

6. API con `Enum` en el runtime y validación en ejecución (§5).
7. MAX por UPM en el proyecto de desarrollo para el test PlayMode con anuncios
   de prueba, con el mismo registro y paquete que Arrows.
8. Consentimiento compartido por el core desde la 1.0 (§6.6): con Viva
   Analytics instalado tiene que funcionar solo.

## 12. Estado de la implementación (2026-09-08)

Hecho, en el working tree sin commit:

- Pasos 1 a 7 del orden de abajo. Core 2.2.0 (`VivaConsent`, `ConsentState`,
  tests, línea de catálogo de Ads, requisito de analytics subido a 2.2.0),
  analytics 2.2.0 (suscripción automática en `AnalyticsService.AddTracker`,
  tests `SharedConsentTests`), y `com.vivagames.ads` 1.0.0 completo: runtime
  (`AdsService` con planificador inyectable, `NoAdsProvider`, `AdsRuntime`,
  `RewardedAdButton`), `MaxAdsProvider` sobre la API vigente de la 8.6.2
  (`CreateBanner(AdViewConfiguration)`, `UpdateBannerPosition(AdViewPosition)`;
  las sobrecargas con `BannerPosition` están obsoletas en el plugin), editor
  (detector por assembly `MaxSdk.Scripts`, JSON, validador, generador, ventanas
  Ad Units y Setup con las comprobaciones del Integration Manager, drawer de
  placements, plantilla `AdsInit`), README con la API completa, CHANGELOG,
  README raíz.
- Diferencias respecto al diseño: `IAdsScheduler` (reloj y esperas inyectables)
  para que la máquina de estados se pruebe sin frames; el listener del
  proveedor es una interfaz (`IAdsListener`) en vez de acciones sueltas; las
  llamadas antes de `Initialize` devuelven `NotInitialized` sin error de
  consola; `OnWaitingForRewarded` es un evento del servicio.
- Verificado con Roslyn (`compile-check.sh`): 24 assemblies, incluidas las
  fuentes del plugin MAX 8.6.2 compiladas a `MaxSdk.Scripts.dll` y el
  proveedor y el test PlayMode contra ellas.
- MAX añadido al proyecto de desarrollo por UPM (`Packages/manifest.json`, los
  dos registros de Arrows y `com.applovin.mediation.ads` 8.6.2), y test
  PlayMode `AdsMaxPlayModeTests` que recorre el flujo entero con el stub del
  editor pulsando sus botones por código.

Verificado en Unity por Jesús el 2026-09-08: **todos los tests EditMode y
PlayMode en verde**, incluido `AdsMaxPlayModeTests` con el stub de MAX del
editor. La primera pasada destapó tres fallos, corregidos: la suscripción de
analíticas a `VivaConsent` dependía de una bandera estática que no sobrevivía
al `Reset` de los tests; `Listener.OnAdRevenue` se llamaba a sí mismo en vez
de disparar `AdsService.OnAdRevenue` (desbordamiento de pila en la primera
impresión con ingresos); y `ColorUtility.TryParseHtmlString` deja blanco al
fallar, no negro.

Pendiente: commit, merge y tags cuando Jesús lo pida.

## 13. Orden de implementación

1. Core 2.2.0: `VivaConsent` y `ConsentState` con tests. Analytics 2.2.0:
   suscripción en `AnalyticsService.Initialize`, referencia a
   `VivaGames.Core`, requisito de core, tests con un estado publicado antes y
   después de inicializar.
2. Runtime de Ads: enums, opciones, configuración, `AdsService` con
   `FakeAdsProvider` y tests de la máquina de estados y de la política.
3. `MaxAdsProvider` sobre el plugin, asmdef con define, detector, publicación
   en `VivaConsent`.
4. `RewardedAdButton` y el drawer de placements.
5. Editor: settings, JSON, validador, naming, generador y tests.
6. Ventanas Ad Units y Setup, comprobaciones del Integration Manager, primer
   arranque, plantilla `AdsInit`.
7. README completo (§9), CHANGELOG, catálogo del core, README raíz.
8. `compile-check.sh`, MAX en el proyecto de desarrollo y test PlayMode,
   commit, merge y tags `core/v2.2.0`, `analytics/v2.2.0` y `ads/v1.0.0`.
