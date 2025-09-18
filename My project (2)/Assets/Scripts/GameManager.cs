using System.Collections;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("Referencias")]
    public BubbleSpawner spawner;
    public Camera mainCam;
    public TMP_Text scoreText;
    public TMP_Text statusText;

    [Header("Reglas de juego")]
    public int totalBubbles = 20;   // intentos totales (explotadas + perdidas)
    public int minToWin = 10;       // mínimo para "Felicitaciones"

    [Header("Raycast de mirada")]
    public float rayMaxDistance = 100f;
    public LayerMask gazeMask;      // marca SOLO la capa "Bubble" en el inspector

    [Header("Proveedor de mirada")]
    [Tooltip("Arrastra aquí un proveedor (MouseGazeProvider / UnitEye / Tobii). Si queda vacío, se crea MouseGazeProvider automáticamente.")]
    public MonoBehaviour gazeProviderBehaviour; // debe implementar IGazeRayProvider
    private IGazeRayProvider _gazeProvider;

    [Header("Fin de partida")]
    [SerializeField] private bool quitOnEnd = true;     // cerrar juego / parar Play
    [SerializeField] private float quitDelay = 1.0f;    // segundos para leer mensaje
    [SerializeField] private bool freezeTimeOnEnd = true; // congelar escena al final
    [SerializeField] private bool hideScoreOnEnd = false; // ocultar contador al final

    // Estado
    private int _popped = 0;
    private int _seen = 0;
    private bool _finished = false;

    private BubbleTarget _hover;

    private void Awake()
    {
        if (!mainCam) mainCam = Camera.main;

        // Si no arrastras un proveedor, usamos mouse por defecto (Input System)
        if (gazeProviderBehaviour == null)
        {
            gazeProviderBehaviour = gameObject.AddComponent<MouseGazeProvider>();
        }

        _gazeProvider = gazeProviderBehaviour as IGazeRayProvider;
        if (_gazeProvider == null)
        {
            Debug.LogError("El objeto asignado en 'gazeProviderBehaviour' no implementa IGazeRayProvider.");
        }
    }

    private void OnEnable()
    {
        if (spawner != null)
        {
            spawner.OnBubblePopped += HandleBubblePopped;
            spawner.OnBubbleMissed += HandleBubbleMissed;
        }
    }

    private void OnDisable()
    {
        if (spawner != null)
        {
            spawner.OnBubblePopped -= HandleBubblePopped;
            spawner.OnBubbleMissed -= HandleBubbleMissed;
        }
    }

    private void Start()
    {
        UpdateUI();
        if (spawner != null) spawner.SpawnNext();

        // Ajustes útiles de StatusText para evitar cortes raros
        if (statusText)
        {
            statusText.textWrappingMode = TextWrappingModes.NoWrap;

            statusText.overflowMode = TextOverflowModes.Overflow;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 32;
            statusText.fontSizeMax = 96;
        }
    }

    private void Update()
    {
        if (_finished || spawner == null || _gazeProvider == null) return;

        // 1) Obtener un Ray desde el proveedor (mouse/webcam/Tobii)
        if (_gazeProvider.TryGetRay(out var ray))
        {
            // 2) Raycast SOLO contra Layer "Bubble"
            if (Physics.Raycast(ray, out var hit, rayMaxDistance, gazeMask))
            {
                var target = hit.collider.GetComponent<BubbleTarget>();

                // Entradas/salidas de mirada
                if (target != _hover)
                {
                    if (_hover) _hover.OnGazeExit();
                    _hover = target;
                    if (_hover) _hover.OnGazeEnter();
                }

                // Mantener (acumula dwell)
                if (_hover) _hover.OnGazeStay(Time.deltaTime);
            }
            else
            {
                if (_hover)
                {
                    _hover.OnGazeExit();
                    _hover = null;
                }
            }

            // (Opcional) Dibuja el ray en la vista Scene para depurar
            Debug.DrawRay(ray.origin, ray.direction * 10f, Color.cyan);
        }
    }

    private void HandleBubblePopped()
    {
        _popped++;
        _seen++;
        CheckEnd();
        UpdateUI();
    }

    private void HandleBubbleMissed()
    {
        _seen++;
        CheckEnd();
        UpdateUI();
    }

    private void CheckEnd()
    {
        if (_seen >= totalBubbles)
        {
            _finished = true;

            // Mensaje final
            if (statusText)
            {
                statusText.textWrappingMode = TextWrappingModes.NoWrap;

                statusText.overflowMode = TextOverflowModes.Overflow;
                statusText.alignment = TextAlignmentOptions.Center;
                statusText.enableAutoSizing = true;
                statusText.fontSizeMin = 32;
                statusText.fontSizeMax = 96;

                statusText.text = _popped >= minToWin
                    ? "¡Felicitaciones!"
                    : "Game Over";
            }

            EndGame();
        }
    }

    private void EndGame()
    {
        // Cortar lógica de juego
        if (spawner != null)
        {
            spawner.ClearCurrent();   // limpia burbuja activa si existe
            spawner.enabled = false;  // evita nuevos spawns
        }

        if (_hover) { _hover.OnGazeExit(); _hover = null; }

        if (hideScoreOnEnd && scoreText) scoreText.enabled = false;

        if (quitOnEnd)
            StartCoroutine(QuitAfterDelay());

        if (freezeTimeOnEnd)
            Time.timeScale = 0f;
    }

    private IEnumerator QuitAfterDelay()
    {
        // Espera en tiempo REAL (no afectado por timeScale)
        yield return new WaitForSecondsRealtime(quitDelay);

        // Cerrar según plataforma
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        // WebGL no permite cerrar la pestaña/programa desde código.
        // Aquí simplemente no hacemos nada más.
#else
        Application.Quit();
#endif
    }

    private void UpdateUI()
    {
        if (scoreText)
            scoreText.text = $"Burbujas: {_popped}/{_seen}/{totalBubbles}";
    }
}
