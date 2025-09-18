using UnityEngine;

public interface IGazeRayProvider
{
    // Devuelve un Ray desde la cámara hacia donde el usuario "mira".
    // Retorna true si el dato es válido este frame.
    bool TryGetRay(out Ray ray);
}
