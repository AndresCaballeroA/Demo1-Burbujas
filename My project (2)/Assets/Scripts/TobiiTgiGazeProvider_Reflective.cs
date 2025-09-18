using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

/// Proveedor TGI por REFLEXIÓN (no depende de tipos concretos de la DLL).
/// - Busca en tiempo de ejecución las clases/métodos: ApiFactory.GetApi, GetStreamsProvider, GetTrackerController.TrackWindow,
///   Update y GetLatestGazePoint(out GazePoint).
/// - Convierte el gaze point (X,Y) a Ray para tu GameManager.
/// Requisitos:
///   * Tener Tobii.GameIntegration.Net.dll en Assets/Plugins (y nativas en Assets/Plugins/x86_64).
///   * Tener el software de Tobii abierto y calibrado.
public class TobiiTgiGazeProvider_Reflective : MonoBehaviour, IGazeRayProvider
{
    [SerializeField] private Camera cam;
    [Header("TGI")]
    public string applicationName = "Unity Bubble Demo";
    [Range(0f, 0.95f)] public float smooth = 0.25f;
    public bool flipY = false;
    public bool debugLogs = false;

    // HWND de la ventana (para TrackWindow)
    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();

    // Tipos/objetos reflejados
    private Assembly _tgiAsm;
    private Type _apiFactoryT;
    private MethodInfo _miGetApi;

    private object _api;                 // ITobiiGameIntegrationApi (desconocido en compile-time)
    private MethodInfo _miApiUpdate;     // api.Update()
    private MethodInfo _miGetStreams;    // api.GetStreamsProvider()
    private object _streams;             // IStreamsProvider
    private MethodInfo _miGetTracker;    // api.GetTrackerController()
    private object _tracker;             // ITrackerController
    private MethodInfo _miTrackWindow;   // tracker.TrackWindow(IntPtr)
    private MethodInfo _miGetLatestGaze; // streams.GetLatestGazePoint(out GazePoint)
    private Type _gazePointT;            // tipo struct/class GazePoint
    private PropertyInfo _gpX, _gpY;     // props X,Y

    // Estado de filtrado
    private Vector2 _latestPx;
    private bool _hasLatest;
    private Vector2 _last;
    private bool _hasLast;

    private void Awake()
    {
        if (!cam) cam = Camera.main;
    }

    private void OnEnable()
    {
        try
        {
            // 1) Localizar el assembly de TGI cargado en el dominio
            _tgiAsm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name.Contains("Tobii.GameIntegration", StringComparison.OrdinalIgnoreCase)
                                          || a.GetName().Name.Contains("TobiiGameIntegration", StringComparison.OrdinalIgnoreCase));
            if (_tgiAsm == null)
            {
                LogErr("No encontré el assembly de TGI (¿Tobii.GameIntegration.Net.dll en Assets/Plugins?).");
                return;
            }

            // 2) Buscar un tipo "ApiFactory" y su método estático GetApi(string)
            _apiFactoryT = _tgiAsm.GetTypes().FirstOrDefault(t => t.Name.Equals("ApiFactory", StringComparison.OrdinalIgnoreCase));
            if (_apiFactoryT == null)
            {
                LogErr("No encontré 'ApiFactory' en la DLL de TGI.");
                return;
            }
            _miGetApi = _apiFactoryT.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                    .FirstOrDefault(m => m.Name.Equals("GetApi", StringComparison.OrdinalIgnoreCase)
                                                      && m.GetParameters().Length == 1
                                                      && m.GetParameters()[0].ParameterType == typeof(string));
            if (_miGetApi == null)
            {
                LogErr("No encontré ApiFactory.GetApi(string).");
                return;
            }

            // 3) Crear la API y obtener streams/tracker
            _api = _miGetApi.Invoke(null, new object[] { applicationName });
            if (_api == null) { LogErr("ApiFactory.GetApi devolvió null."); return; }

            _miApiUpdate = _api.GetType().GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
            _miGetStreams = _api.GetType().GetMethod("GetStreamsProvider", BindingFlags.Public | BindingFlags.Instance);
            _miGetTracker = _api.GetType().GetMethod("GetTrackerController", BindingFlags.Public | BindingFlags.Instance);
            if (_miApiUpdate == null || _miGetStreams == null || _miGetTracker == null)
            {
                LogErr("Faltan métodos en la API (Update/GetStreamsProvider/GetTrackerController).");
                return;
            }

            _streams = _miGetStreams.Invoke(_api, null);
            _tracker = _miGetTracker.Invoke(_api, null);
            if (_streams == null || _tracker == null)
            {
                LogErr("No pude obtener StreamsProvider o TrackerController.");
                return;
            }

            // 4) TrackWindow(hwnd)
            _miTrackWindow = _tracker.GetType().GetMethod("TrackWindow", BindingFlags.Public | BindingFlags.Instance);
            if (_miTrackWindow == null)
            {
                LogErr("No encontré TrackerController.TrackWindow(IntPtr).");
                return;
            }
            _miTrackWindow.Invoke(_tracker, new object[] { GetActiveWindow() });

            // 5) GetLatestGazePoint(out gp)
            _miGetLatestGaze = _streams.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m =>
                {
                    if (!m.Name.Equals("GetLatestGazePoint", StringComparison.OrdinalIgnoreCase)) return false;
                    var ps = m.GetParameters();
                    return ps.Length == 1 && ps[0].IsOut;
                });
            if (_miGetLatestGaze == null)
            {
                LogErr("No encontré StreamsProvider.GetLatestGazePoint(out GP).");
                return;
            }

            // Descubrir el tipo GazePoint y sus propiedades X/Y
            _gazePointT = _miGetLatestGaze.GetParameters()[0].ParameterType.GetElementType(); // 'out GP' -> GP
            _gpX = _gazePointT.GetProperty("X", BindingFlags.Public | BindingFlags.Instance);
            _gpY = _gazePointT.GetProperty("Y", BindingFlags.Public | BindingFlags.Instance);
            if (_gpX == null || _gpY == null)
            {
                LogErr("No encontré propiedades X/Y en GazePoint.");
                return;
            }

            LogOk("TGI (reflexión) inicializado.");
        }
        catch (Exception e)
        {
            LogErr("Excepción inicializando TGI: " + e.Message);
        }
    }

    private void OnDisable()
    {
        try
        {
            // api.Shutdown() si existe
            if (_api != null)
            {
                var miShutdown = _api.GetType().GetMethod("Shutdown", BindingFlags.Public | BindingFlags.Instance);
                miShutdown?.Invoke(_api, null);
            }
        } catch { /* ignore */ }

        _api = null; _streams = null; _tracker = null;
        _tgiAsm = null; _apiFactoryT = null; _miGetApi = null;
        _miApiUpdate = null; _miGetStreams = null; _miGetTracker = null;
        _miTrackWindow = null; _miGetLatestGaze = null; _gazePointT = null; _gpX = null; _gpY = null;

        _hasLatest = _hasLast = false;
    }

    private void LateUpdate()
    {
        // TGI necesita Update() cada frame (según Getting Started).
        _miApiUpdate?.Invoke(_api, null);

        if (_miGetLatestGaze == null || _streams == null) return;

        // Preparar arg 'out' (caja del struct)
        object[] args = new object[] { Activator.CreateInstance(_gazePointT) };
        bool ok = (bool)_miGetLatestGaze.Invoke(_streams, args);

        if (ok)
        {
            object gp = args[0];
            float x = Convert.ToSingle(_gpX.GetValue(gp));
            float y = Convert.ToSingle(_gpY.GetValue(gp));

            // Algunas builds pueden dar 0..1; si ves <~1.001 lo escalamos.
            if (x >= 0f && x <= 1.001f && y >= 0f && y <= 1.001f)
            {
                x *= Screen.width;
                y *= Screen.height;
            }
            if (flipY) y = Screen.height - y;

            _latestPx = new Vector2(x, y);
            _hasLatest = true;

            if (debugLogs) Debug.Log($"[TGI] gaze px: {_latestPx}");
        }
    }

    public bool TryGetRay(out Ray ray)
    {
        ray = default;
        if (!_hasLatest || !cam) return false;

        var p = _latestPx;
        if (_hasLast) p = Vector2.Lerp(_last, p, 1f - smooth);
        _last = p; _hasLast = true;

        ray = cam.ScreenPointToRay(p);
        return true;
    }

    private void LogOk(string msg)    { if (debugLogs) Debug.Log("[TGI] " + msg, this); }
    private void LogErr(string msg)   { Debug.LogError("[TGI] " + msg, this); }
}
