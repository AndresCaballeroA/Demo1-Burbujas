using UnityEngine;
using UnityEngine.InputSystem; // <- nuevo Input System

public class MouseGazeProvider : MonoBehaviour, IGazeRayProvider
{
    [SerializeField] private Camera cam;

    private void Awake()
    {
        if (!cam) cam = Camera.main;
    }

    public bool TryGetRay(out Ray ray)
    {
        ray = default;
        if (!cam) return false;

        // Lee la posición del puntero usando el Input System nuevo
        Vector2 pos;
        if (Mouse.current != null)
        {
            pos = Mouse.current.position.ReadValue();
        }
        else if (Pointer.current != null)
        {
            pos = Pointer.current.position.ReadValue();
        }
        else
        {
            pos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        ray = cam.ScreenPointToRay(pos);
        return true;
    }
}
