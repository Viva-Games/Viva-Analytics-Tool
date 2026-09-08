# Trackers de Facebook y Singular en Viva Analytics — Diseño

Fecha: 2026-09-08
Repo: `Viva-Games/Viva-Analytics-Tool`, rama `develop`
Estado: aprobado (decisiones en §9); en implementación.

## Objetivo

Que un proyecto con Viva Analytics mande a Facebook y a Singular lo que toca
sin escribir código: dos trackers nuevos dentro del paquete de analíticas,
cada uno activo solo si su SDK está en el proyecto, y un **destino por
evento** elegido en el Event Manager. Firebase sigue recibiendo todo; Facebook
y Singular reciben solo los eventos marcados. Además, con Viva Ads instalado,
los ingresos de anuncios se atribuyen en Singular solos, con un toggle.

Punto de partida: lo que Arrows hace hoy con los dos SDK (§1), que es un
reenvío selectivo, no total.

## Fuera de alcance (2.3)

- Compras: `SingularSDK.InAppPurchase` y los eventos de compra de Facebook.
  Irán con el módulo de IAP si llega.
- SKAdNetwork y conversion values de Singular, deep links, Facebook Login.
- Configurar las claves de Singular desde nuestra ventana: se queda el
  componente `SingularSDK` en la escena, que es la vía oficial del SDK.
- Consentimiento hacia Singular (`LimitDataSharing`): Arrows decidió no
  tocarlo porque el SDK gestiona ATT e IDFA por su cuenta. Se mantiene.

## 1. Hechos comprobados

Fuentes: código de Arrows, fuentes del paquete de Singular 5.7.0 instalado en
Arrows (`Library/PackageCache/singular-unity-package@…`), `Facebook.Unity.dll`
de Arrows, documentación de Meta App Events y de Singular.

| Hecho | Fuente |
|---|---|
| Singular se instala como paquete UPM desde `https://github.com/singular-labs/Singular-Unity-SDK.git` (5.7.0) con el assembly `SingularSDK`. El SDK se inicializa desde su componente `SingularSDK` en la escena (API key, secret, `InitializeOnAwake`). `SingularSDK.Initialized` es un `static bool`; `Event(Dictionary<string, object> args, string name)` **no hace nada si no está inicializado**, así que hay que encolar. `SetCustomUserId(string)`, `SetGlobalProperty(key, value, overrideExisting)`, `AdRevenue(SingularAdData)` con `SingularAdData(platform, currency, revenue).WithAdType().WithAdUnitId().WithNetworkName().WithAdPlacmentName().WithPrecision()`. | `SingularSDK.cs`, `SingularAdData.cs`, manifest de Arrows |
| Límites de Singular: nombre de evento hasta 32 caracteres ASCII; atributos y valores hasta 500. | Doc de Singular "Tracking In-app Events" |
| Facebook es el SDK en `Assets/FacebookSDK` con `Facebook.Unity.dll` (precompilado, detectable como Firebase). `FB.Init(callback)`, `FB.IsInitialized`, `FB.ActivateApp()` al iniciar y al reanudar, `FB.LogAppEvent(name, float? valueToSum, Dictionary<string, object> parameters)`, `FB.Mobile.SetAutoLogAppEventsEnabled/SetAdvertiserIDCollectionEnabled/SetAdvertiserTrackingEnabled`, `FB.Mobile.UserID`. La configuración (app id) vive en `Assets/FacebookSDK/SDK/Resources/FacebookSettings.asset`. | DLL de Arrows, doc de Meta |
| Límites de Facebook: nombres de evento y de parámetro de 2 a 40 caracteres, alfanuméricos, `_`, `-` o espacio; hasta 25 parámetros por evento; valores de hasta 100 caracteres; hasta 1000 nombres de evento distintos. | Doc de Meta App Events |
| Arrows: a Singular solo van el custom user id (un GUID guardado en PlayerPrefs, fijado tras inicializar MAX) y los ingresos de anuncios. A Facebook solo `level_reached10` y `level_reached20` con `FB.LogAppEvent` a mano, además de `FB.Init` con las banderas a false, `FB.ActivateApp` al iniciar y al reanudar, y las banderas subidas por `ConsentBridge` con la señal `AdStorage`. | `AdManager.cs`, `GameController.cs`, `ConsentBridge.cs` |
| Los nombres de evento de Firebase admiten hasta 40 caracteres: un evento marcado para Singular puede pasarse de 32. La ventana lo valida. | Doc de Firebase y de Singular |

## 2. Qué se conserva de Arrows y qué cambia

| Aspecto | Arrows hoy | Módulo | Por qué |
|---|---|---|---|
| Inicialización de Facebook | `FB.Init` en `GameController` con banderas a false, `ActivateApp` al iniciar y en `OnApplicationPause` | El tracker lo hace igual, si el proyecto no ha inicializado ya el SDK | Es lo que exige Meta |
| Banderas de tracking de Facebook | `ConsentBridge` con `AdStorage` | El tracker se suscribe a `VivaConsent` y aplica la misma regla: fuera de GDPR o purpose 1 concedido | Sin `ConsentBridge` |
| Eventos a Facebook | Dos llamadas a mano | Los eventos marcados con destino Facebook en el Event Manager, con sus parámetros | Sin duplicar código |
| Eventos a Singular | Ninguno | Los marcados con destino Singular | Toggle por evento |
| Custom user id | GUID en PlayerPrefs, solo para Singular | `VivaUserId.GetOrCreate()` en `AnalyticsInit` → `SetUserId`, que llega a Firebase como user id y a Singular como custom user id | Mismo identificador en las dos plataformas |
| Ingresos de anuncios a Singular | `AdManager` los manda a mano | Viva Ads los publica en el core (`VivaAdRevenue`) y el tracker de Singular los atribuye si el toggle está activo | Sin código del usuario |
| Momento de fijar el user id en Singular | Tras inicializar MAX | Cuando `SingularSDK.Initialized` es true, encolado hasta entonces | El SDK lo ignora antes |

## 3. Cambios por paquete

### Core 2.3.0

`VivaAdRevenue` en `VivaGames.Core`: flujo de impresiones con ingresos entre
módulos, como `VivaConsent` pero sin estado (no hay "última impresión" que
entregar a quien se suscribe tarde).

```csharp
namespace Viva.Core
{
    public sealed class AdRevenueEvent
    {
        public string Platform = "AppLovin";  // red de mediación
        public string Format;                 // REWARDED, INTER, BANNER (como lo reporta el SDK)
        public string Placement, AdUnitId, NetworkName, NetworkPlacement, CreativeId, RevenuePrecision;
        public double Revenue;
        public string Currency = "USD";
    }

    public static class VivaAdRevenue
    {
        public static void Publish(AdRevenueEvent revenue);
        public static void Subscribe(Action<AdRevenueEvent> callback);   // deduplica por callback
        public static void Unsubscribe(Action<AdRevenueEvent> callback);
    }
}
```

Tests: publicación a varios suscriptores en orden, excepción aislada, null
ignorado, unsubscribe.

### Ads 1.1.0

`AdsService` publica cada `OnAdRevenue` también en `VivaAdRevenue`. La
plantilla `AdsInit` deja de enseñar la llamada a Singular y dice que con Viva
Analytics y Singular la atribución es automática. Requisito de core 2.3.0.

### Analytics 2.3.0

**Destino por evento.**

```csharp
[Flags]
public enum AnalyticsTargets { None = 0, Firebase = 1, Facebook = 2, Singular = 4, All = ~0 }

public interface IRoutedAnalyticsEvent : IAnalyticsEvent
{
    AnalyticsTargets Targets { get; }
}

public abstract class AAnalyticsTracker
{
    /// <summary>Qué eventos recibe este tracker. All = todos (consola). Por defecto Firebase, para los trackers antiguos.</summary>
    public virtual AnalyticsTargets Targets => AnalyticsTargets.Firebase;
}
```

`AnalyticsService.TrackEvent`: el evento declara sus destinos
(`IRoutedAnalyticsEvent`; un evento sin interfaz cuenta como solo Firebase, así
que nada cambia para los eventos ya generados) y cada tracker recibe el evento
si `(tracker.Targets & evento.Targets) != 0`. El tracker de consola declara
`All`; el de Firebase, `Firebase`; los nuevos, el suyo. El FTUE interno sigue
yendo a Firebase.

- Generador: `public AnalyticsTargets Targets => AnalyticsTargets.Firebase | AnalyticsTargets.Facebook;`
  en la clase del evento e implementación de `IRoutedAnalyticsEvent`. El
  editor lo vuelve a leer con una expresión regular, como hace con los
  parámetros. Sin la línea, el evento cuenta como solo Firebase.
- Event Editor: dos casillas, **Facebook** y **Singular**, con Firebase fijo.
  Con Singular marcado, aviso si el nombre pasa de 32 caracteres; con Facebook,
  aviso si hay más de 25 parámetros o algún nombre se sale de las reglas.
- Event Manager: los destinos aparecen en cada fila. El catálogo del estudio
  (`Templates~/EventCatalog.json`) admite `"targets": ["facebook"]`, y Add y
  Restore respetan esos destinos. El catálogo actual no marca ninguno: los
  `level_reached10` y `level_reached20` de Arrows son eventos de ese juego, no
  del catálogo del estudio; cada proyecto los marca en su Event Editor.

**`FacebookAnalyticsTracker`** (assembly `VivaGames.Analytics.Facebook`,
`defineConstraints: VIVA_FACEBOOK`, referencia `VivaGames.Core`; el define lo
mantiene `FacebookSdkDetector` con `SdkDetection.SyncDefine` sobre
`Facebook.Unity.dll`):

- `Initialize`: si `FB.IsInitialized`, listo; si no, `FB.Init(OnInitialized)`.
  Al inicializar: banderas a false salvo que `VivaConsent` ya esté resuelto,
  `FB.ActivateApp()`, y vaciado de la cola. `AnalyticsRuntime` (MonoBehaviour
  oculto del paquete, nuevo) llama a `FB.ActivateApp()` al reanudar.
- `VivaConsent.Subscribe`: las tres banderas a `!IsGdpr || Purpose1 == true`,
  la regla de `ConsentBridge`.
- `TrackEvent`: `FB.LogAppEvent(nombre, null, parámetros)`. Conversión de
  parámetros en un helper puro y testeado: números como números, bool como 1
  o 0, resto `ToString()`, valores recortados a 100 caracteres, máximo 25
  parámetros (los sobrantes se descartan con aviso una vez por evento).
- `SetUserId`: `FB.Mobile.UserID`. Propiedades de usuario: sin equivalente,
  se ignoran.
- Antes de `FB.IsInitialized` se encola (hasta 200 eventos, como Firebase).

**`SingularAnalyticsTracker`** (assembly `VivaGames.Analytics.Singular`,
`defineConstraints: VIVA_SINGULAR`, referencias `VivaGames.Core` y
`SingularSDK`; el define lo mantiene `SingularSdkDetector` buscando el
assembly `SingularSDK` en la compilación, como el detector de MAX):

- No inicializa el SDK: lo hace el componente `SingularSDK` de la escena.
  `AnalyticsRuntime` comprueba `SingularSDK.Initialized` cada medio segundo
  hasta que lo esté, y entonces vacía la cola.
- `TrackEvent`: `SingularSDK.Event(atributos, nombre)`. Helper puro: valores
  como cadena o número, recorte a 500 caracteres.
- `SetUserId`: `SingularSDK.SetCustomUserId`. `SetUserProperty`:
  `SingularSDK.SetGlobalProperty(key, value, true)`, que acompaña a cada evento.
- **Atribución de ingresos de anuncios**: propiedad `AttributeAdRevenue`
  (true por defecto). Con ella, el tracker se suscribe a `VivaAdRevenue` y
  manda `SingularSDK.AdRevenue(new SingularAdData(platform, currency, revenue)
  .WithAdType(format).WithAdUnitId(adUnitId).WithNetworkName(network)
  .WithAdPlacmentName(placement).WithPrecision(precision))`, encolado hasta
  que el SDK esté listo. Sin Viva Ads no llega nada y no pasa nada.

**`VivaUserId`** en `VivaGames.Analytics`: `GetOrCreate()` devuelve un GUID
de 32 hexadecimales guardado en PlayerPrefs (`viva_user_id`), y
`Reset()` genera otro. La plantilla `AnalyticsInit` llama a
`AnalyticsService.SetUserId(VivaUserId.GetOrCreate())`.

**Plantilla `AnalyticsInit`**: construye la lista de trackers bajo defines:

```csharp
var trackers = new List<AAnalyticsTracker>();
#if VIVA_FIREBASE_ANALYTICS
var firebase = new FirebaseAnalyticsTracker();
firebase.FirebaseReady += OnFirebaseReady;
trackers.Add(firebase);
#else
trackers.Add(new ConsoleAnalyticsTracker());
#endif
#if VIVA_FACEBOOK
trackers.Add(new FacebookAnalyticsTracker());          // remove the line to keep Facebook out
#endif
#if VIVA_SINGULAR
trackers.Add(new SingularAnalyticsTracker { AttributeAdRevenue = true });
#endif
AnalyticsService.Initialize(trackers.ToArray());
AnalyticsService.SetUserId(VivaUserId.GetOrCreate());
```

Los `AnalyticsInit` ya generados no cambian: Setup detecta el SDK y avisa si
el fichero no añade el tracker, con las líneas a copiar.

**Setup**: filas nuevas. Facebook SDK (detectado, define, app id en
`FacebookSettings.asset`, tracker en `AnalyticsInit`). Singular (paquete y
versión, define, recordatorio del componente en la primera escena, tracker en
`AnalyticsInit`, toggle de ingresos).

## 4. Ficheros

```
com.vivagames.core/Runtime/VivaAdRevenue.cs (+ tests)
com.vivagames.ads/Runtime/AdsService.cs (publica), Templates~/AdsInit.cs.txt
com.vivagames.analytics/
  Runtime/AnalyticsTargets.cs, IRoutedAnalyticsEvent.cs, VivaUserId.cs, AnalyticsRuntime.cs
  Runtime/AnalyticsService.cs (reparto), AAnalyticsTracker.cs (Targets), ConsoleAnalyticsTracker.cs (All), FirebaseAnalyticsTracker.cs (Firebase)
  Runtime/Facebook/VivaGames.Analytics.Facebook.asmdef, FacebookAnalyticsTracker.cs, FacebookEventConverter.cs
  Runtime/Singular/VivaGames.Analytics.Singular.asmdef, SingularAnalyticsTracker.cs, SingularEventConverter.cs
  Editor/FacebookSdkDetector.cs, SingularSdkDetector.cs, AnalyticsEventCodeGenerator.cs (Targets), AnalyticsEventEditor.cs (casillas), AnalyticsEventManager.cs (etiquetas), AnalyticsEventCatalog.cs (targets), AnalyticsSetupWindow.cs (filas), TargetRules.cs (límites)
  Templates~/AnalyticsInit.cs.txt, EventCatalog.json
  Tests/Editor/RoutingTests.cs, FacebookEventConverterTests.cs, SingularEventConverterTests.cs, VivaUserIdTests.cs, TargetRulesTests.cs, generator y catálogo
```

## 5. Documentación

README de Analytics: sección "Facebook and Singular" con instalación de cada
SDK, qué recibe cada uno, cómo marcar destinos, la atribución de ingresos y
su toggle, `VivaUserId`, límites de cada plataforma, y las líneas a añadir en
un `AnalyticsInit` antiguo. Tabla de la API pública nueva. README del core:
`VivaAdRevenue`. README de Ads: nota de la atribución automática.

## 6. Verificación

- EditMode: reparto por destinos con trackers falsos (un evento sin interfaz
  solo a Firebase y consola; con Facebook, también al de Facebook; `All`
  recibe todo), generador con y sin destinos y relectura por el editor,
  catálogo con `targets`, conversores de parámetros y límites, `VivaUserId`,
  `VivaAdRevenue`.
- Los trackers reales necesitan sus SDK: compilación con Roslyn contra
  `Facebook.Unity.dll` y las fuentes de Singular de Arrows. No se añaden al
  proyecto de desarrollo (Singular necesita claves y Facebook un app id).

## 7. Orden de implementación

1. Core 2.3.0 `VivaAdRevenue` + tests; Ads 1.1.0 publica.
2. Analytics: `AnalyticsTargets`, `IRoutedAnalyticsEvent`, `Targets` en trackers,
   reparto en `AnalyticsService` + tests.
3. Generador, relectura, catálogo con `targets`, Event Editor y Manager,
   reglas de límites + tests.
4. `VivaUserId`, `AnalyticsRuntime`, tracker de Facebook, tracker de
   Singular, conversores + tests, detectores.
5. Plantilla `AnalyticsInit`, Setup, READMEs, CHANGELOGs, catálogo del core
   (requisitos), `compile-check.sh`.

## 8. Estado

Implementado el 2026-09-08 en el working tree, sin commit: core 2.3.0
(`VivaAdRevenue` + tests), Ads 1.1.0 (publica en `VivaAdRevenue`, plantilla sin
el ejemplo de Singular), Analytics 2.3.0 (enrutado por destinos, `VivaUserId`,
`AnalyticsRuntime`, conversores y trackers de Facebook y Singular con sus
detectores y asmdefs, generador/editor/catálogo/gestor con destinos, filas de
Setup, plantilla `AnalyticsInit` con los tres trackers, README y CHANGELOG de
los tres paquetes, requisitos del catálogo). Todo compila con Roslyn
(`compile-check.sh`): el tracker de Facebook contra la `Facebook.Unity.dll` de
Arrows y el de Singular contra las fuentes del paquete UPM 5.7.0 de Arrows, y
`AnalyticsInit` con los tres defines. Pendiente: Jesús corre los tests EditMode
en su editor (RoutingTests, TargetsCodecAndConvertersTests, VivaAdRevenueTests
y el `Revenue_IsForwarded` ampliado), commit a petición, push y tags.

Revisión de Jesús del 2026-09-08: el toggle de atribución en Singular vive en
la ventana Setup, guardado en `Assets/VivaAnalytics/Resources/VivaAnalyticsSettings.asset`
(`AnalyticsSettings`, leído en ejecución por `SingularAnalyticsTracker`), no en
código; y las filas de Facebook y Singular de Setup muestran un estado neutro
("–", not installed) cuando el SDK no está, en vez de OK.

## 9. Decisiones de la revisión (2026-09-08)

1. Ingresos de anuncios a Singular por el core, automáticos con Viva Ads y
   con toggle en el tracker (`AttributeAdRevenue`).
2. `VivaUserId.GetOrCreate()` como identificador común para Firebase y Singular.
3. Del análisis previo: trackers dentro del paquete de analíticas; destino por
   evento con Firebase fijo; el consentimiento de Facebook lo aplica el
   tracker desde `VivaConsent`; Singular sigue inicializándose por su
   componente.
