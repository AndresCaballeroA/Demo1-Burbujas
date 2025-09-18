using System;
using UnityEngine;

public class BubbleSpawner : MonoBehaviour
{
    [Header("Prefab y referencias")]
    public BubbleTarget bubblePrefab;
    public Transform playerCam;

    [Header("Zona de aparición (frente a la cámara)")]
    public Vector2 spawnDistance = new Vector2(2.0f, 3.5f); // metros [min, max]
    public Vector2 spawnAngles   = new Vector2(25f, 25f);   // apertura (grados) [yaw, pitch]

    [Header("Vida de la burbuja")]
    public float bubbleLifespan = 2.5f;

    public event Action OnBubblePopped;
    public event Action OnBubbleMissed;

    private BubbleTarget _current;

    private void Start()
    {
        if (!playerCam) playerCam = Camera.main ? Camera.main.transform : null;
    }

    public void SpawnNext()
    {
        if (bubblePrefab == null || playerCam == null) return;
        if (_current != null) return;

        var dist  = UnityEngine.Random.Range(spawnDistance.x, spawnDistance.y);
        var yaw   = UnityEngine.Random.Range(-spawnAngles.x, spawnAngles.x);
        var pitch = UnityEngine.Random.Range(-spawnAngles.y, spawnAngles.y);

        var dir = Quaternion.AngleAxis(yaw, Vector3.up) *
                  Quaternion.AngleAxis(pitch, Vector3.right) *
                  playerCam.forward;

        var pos = playerCam.position + dir.normalized * dist;

        _current = Instantiate(bubblePrefab, pos, Quaternion.identity);
        _current.transform.LookAt(playerCam);

        _current.OnPopped += HandlePopped;

        Invoke(nameof(ExpireCurrent), bubbleLifespan);
    }

    private void HandlePopped(BubbleTarget b)
    {
        CancelInvoke(nameof(ExpireCurrent));
        if (_current != null)
        {
            _current.OnPopped -= HandlePopped;
            _current = null;
        }

        OnBubblePopped?.Invoke();
        SpawnNext();
    }

    private void ExpireCurrent()
    {
        if (_current == null) return;

        _current.OnPopped -= HandlePopped;
        var go = _current.gameObject;
        _current = null;
        Destroy(go);

        OnBubbleMissed?.Invoke();
        SpawnNext();
    }

    public void ClearCurrent()
    {
        CancelInvoke(nameof(ExpireCurrent));
        if (_current != null)
        {
            _current.OnPopped -= HandlePopped;
            Destroy(_current.gameObject);
            _current = null;
        }
    }
}
