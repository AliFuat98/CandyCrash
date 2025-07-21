using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class InputReader : MonoBehaviour
{
    public event Action<Vector2> Fire;

    public void OnFire(InputValue value)
    {
        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Fire?.Invoke(screenPosition);
    }
}

