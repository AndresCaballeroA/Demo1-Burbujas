using System;
using UnityEngine;
using UnityEngine.UI;

public class BubbleTarget : MonoBehaviour
{
    [Header("Dwell (segundos)")]
    public float dwellToPop = 0.8f;

    [Header("UI de carga (opcional)")]
    public Image radialFill; // Image (Filled) en World Space

    [Header("Color dinámico (opcional)")]
    [Tooltip("Asigna aquí el MeshRenderer de la esfera (del Prefab Bubble).")]
    public Renderer bubbleRenderer;
    [Tooltip("Gradiente 0..1 para el color mientras se 'carga' el dwell.")]
    public Gradient colorRamp;

    [Header("Emisión (opcional)")]
    [ColorUsage(true, true)] public Color emissionColor = Color.black;
    public float emissionIntensity = 0f; // 0 = sin emisión

    [Header("Overlay de relleno (shader opcional)")]
    [Tooltip("Si creaste el hijo 'FillOverlay' con el shader de relleno, asigna su Renderer aquí.")]
    public Renderer fillOverlayRenderer;
    [Range(0f, 0.2f)] public float fillEdgeSoftness = 0.03f;
    public Color overlayBaseColor  = new Color(1f,1f,1f,0.15f);
    public Color overlayFillColor  = new Color(0f,1f,1f,0.6f);

    [Header("Debug")]
    public bool debugLogs = false;

    public event Action<BubbleTarget> OnPopped;

    private float _dwell;
    private bool _gazing;

    private MaterialPropertyBlock _mpb;
    private static readonly int ID_Color      = Shader.PropertyToID("_Color");      // Standard
    private static readonly int ID_BaseColor  = Shader.PropertyToID("_BaseColor");  // URP Lit
    private static readonly int ID_Emission   = Shader.PropertyToID("_EmissionColor");

    private static readonly int ID_Fill       = Shader.PropertyToID("_Fill");
    private static readonly int ID_Softness   = Shader.PropertyToID("_Softness");
    private static readonly int ID_OLBase     = Shader.PropertyToID("_BaseColor");
    private static readonly int ID_OLFill     = Shader.PropertyToID("_FillColor");

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();

        // Seguridad: si no te acordaste de asignar el renderer, intenta auto-buscarlo.
        if (!bubbleRenderer)
        {
            bubbleRenderer = GetComponentInChildren<MeshRenderer>();
            if (debugLogs) Debug.Log($"[BubbleTarget] Auto-asigné bubbleRenderer: {bubbleRenderer}", this);
        }
    }

    public void OnGazeEnter()
    {
        _gazing = true;
        if (debugLogs) Debug.Log("[BubbleTarget] OnGazeEnter()", this);
    }

    public void OnGazeExit()
    {
        _gazing = false;
        _dwell = 0f;
        UpdateVisuals(0f);
        if (debugLogs) Debug.Log("[BubbleTarget] OnGazeExit()", this);
    }

    public void OnGazeStay(float dt)
    {
        if (!_gazing) return;

        _dwell += dt;
        float t = Mathf.Clamp01(_dwell / dwellToPop);
        UpdateVisuals(t);

        if (_dwell >= dwellToPop)
        {
            Pop();
        }
    }

    private void UpdateVisuals(float t)
    {
        // 1) UI radial (si existe)
        if (radialFill) radialFill.fillAmount = t;

        // 2) Color del material principal
        if (bubbleRenderer && colorRamp != null)
        {
            bubbleRenderer.GetPropertyBlock(_mpb);

            Color c = colorRamp.Evaluate(t);

            // Intentar Standard (_Color) y URP Lit (_BaseColor)
            _mpb.SetColor(ID_Color, c);
            _mpb.SetColor(ID_BaseColor, c);

            // Emisión opcional (requiere Emission habilitado en el material)
            if (emissionIntensity > 0f)
            {
                Color e = emissionColor * (emissionIntensity * t);
                _mpb.SetColor(ID_Emission, e);
            }

            bubbleRenderer.SetPropertyBlock(_mpb);
        }

        // 3) Overlay de relleno (si lo usas)
        if (fillOverlayRenderer)
        {
            fillOverlayRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(ID_Fill, t);
            _mpb.SetFloat(ID_Softness, fillEdgeSoftness);
            _mpb.SetColor(ID_OLBase, overlayBaseColor);
            _mpb.SetColor(ID_OLFill, overlayFillColor);
            fillOverlayRenderer.SetPropertyBlock(_mpb);
        }
    }

    private void Pop()
    {
        if (debugLogs) Debug.Log("[BubbleTarget] Pop()", this);
        OnPopped?.Invoke(this);
        Destroy(gameObject);
    }
}
