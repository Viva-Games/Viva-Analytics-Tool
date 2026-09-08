using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Viva.Services.Ads
{
    /// <summary>
    /// Botón de rewarded listo para usar: refleja el estado del anuncio (listo, cargando, sin conexión), decide
    /// qué hacer cuando no hay anuncio y muestra el rewarded al pulsar. La recompensa la da el juego en
    /// OnRewarded; qué hacer sin anuncio, en OnUnavailable o desactivando el botón.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("Viva/Ads/Rewarded Ad Button")]
    public class RewardedAdButton : MonoBehaviour
    {
        public enum UnavailableBehaviour
        {
            /// <summary>El botón se desactiva (interactable = false) mientras no hay anuncio.</summary>
            DisableButton,
            /// <summary>El botón sigue activo; al pulsarlo sin anuncio se dispara OnUnavailable con el motivo (para un popup).</summary>
            KeepInteractable
        }

        [Serializable]
        public class StatusEvent : UnityEvent<AdStatus>
        {
        }

        [SerializeField, PlacementName(AdFormat.Rewarded)]
        private string placement = string.Empty;

        [SerializeField]
        private UnavailableBehaviour whenUnavailable = UnavailableBehaviour.DisableButton;

        [SerializeField, Tooltip("If the ad is still loading when pressed, wait up to this many seconds for it (0 = do not wait).")]
        private float waitForLoadSeconds = 0f;

        [Header("Optional indicators")]
        [SerializeField] private GameObject shownWhenReady;
        [SerializeField] private GameObject shownWhenLoading;
        [SerializeField] private GameObject shownWhenNoInternet;

        [Header("Events")]
        [SerializeField] private UnityEvent onRewarded = new UnityEvent();
        [SerializeField] private UnityEvent onNotRewarded = new UnityEvent();
        [SerializeField] private StatusEvent onUnavailable = new StatusEvent();

        /// <summary>El jugador ha visto el anuncio: dar la recompensa.</summary>
        public event Action Rewarded;

        /// <summary>El anuncio se cerró sin recompensa o falló al mostrarse.</summary>
        public event Action NotRewarded;

        /// <summary>Se pulsó sin anuncio disponible (solo con KeepInteractable), con el estado del formato.</summary>
        public event Action<AdStatus> Unavailable;

        private Button _button;
        private bool _busy;

        public string Placement
        {
            get => placement;
            set
            {
                placement = value ?? string.Empty;
                Refresh();
            }
        }

        public UnavailableBehaviour WhenUnavailable
        {
            get => whenUnavailable;
            set
            {
                whenUnavailable = value;
                Refresh();
            }
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(OnClick);
            AdsService.OnStatusChanged += OnStatusChanged;
            AdsService.OnShowingAdChanged += OnShowingAdChanged;
            AdsService.OnWaitingForRewarded += OnWaitingForRewarded;
            Refresh();
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(OnClick);
            AdsService.OnStatusChanged -= OnStatusChanged;
            AdsService.OnShowingAdChanged -= OnShowingAdChanged;
            AdsService.OnWaitingForRewarded -= OnWaitingForRewarded;
        }

        private void OnStatusChanged(AdFormat format, AdStatus status)
        {
            if (format == AdFormat.Rewarded) Refresh();
        }

        private void OnShowingAdChanged(bool showing)
        {
            Refresh();
        }

        private void OnWaitingForRewarded(bool waiting)
        {
            Refresh();
        }

        /// <summary>Vuelve a pintar el botón según el estado actual del rewarded.</summary>
        public void Refresh()
        {
            if (_button == null) _button = GetComponent<Button>();
            var status = AdsService.RewardedStatus;
            bool ready = status == AdStatus.Ready;

            bool interactable = !_busy && !AdsService.IsShowingAd && (ready || whenUnavailable == UnavailableBehaviour.KeepInteractable);
            _button.interactable = interactable;

            if (shownWhenReady != null) shownWhenReady.SetActive(ready);
            if (shownWhenLoading != null) shownWhenLoading.SetActive(status == AdStatus.Loading || status == AdStatus.NotInitialized || _busy);
            if (shownWhenNoInternet != null) shownWhenNoInternet.SetActive(status == AdStatus.NoInternet);
        }

        private void OnClick()
        {
            if (_busy || AdsService.IsShowingAd) return;

            var status = AdsService.RewardedStatus;
            bool canWait = waitForLoadSeconds > 0f && status == AdStatus.Loading;
            if (status != AdStatus.Ready && !canWait)
            {
                ReportUnavailable(status);
                return;
            }

            _busy = true;
            Refresh();
            AdsService.ShowRewarded(placement, OnResult, waitForLoadSeconds);
        }

        private void OnResult(RewardedResult result)
        {
            _busy = false;
            Refresh();

            switch (result)
            {
                case RewardedResult.Rewarded:
                    onRewarded.Invoke();
                    Rewarded?.Invoke();
                    break;
                case RewardedResult.NotRewarded:
                case RewardedResult.DisplayFailed:
                    onNotRewarded.Invoke();
                    NotRewarded?.Invoke();
                    break;
                case RewardedResult.NoInternet:
                    ReportUnavailable(AdStatus.NoInternet);
                    break;
                case RewardedResult.NotInitialized:
                    ReportUnavailable(AdStatus.NotInitialized);
                    break;
                case RewardedResult.AlreadyShowing:
                    ReportUnavailable(AdStatus.Showing);
                    break;
                default:
                    ReportUnavailable(AdsService.RewardedStatus == AdStatus.Ready ? AdStatus.Loading : AdsService.RewardedStatus);
                    break;
            }
        }

        private void ReportUnavailable(AdStatus status)
        {
            onUnavailable.Invoke(status);
            Unavailable?.Invoke(status);
        }
    }
}
