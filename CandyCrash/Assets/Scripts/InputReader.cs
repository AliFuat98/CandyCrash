using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class InputReader : MonoBehaviour {
    public event Action<Vector2> Fire;
    PlayerInput _playerInput;

    void Awake() {
        _playerInput = GetComponent<PlayerInput>();
        _playerInput.actions["Fire"].performed += OnFirePerformed;
    }

    void OnFirePerformed(InputAction.CallbackContext context) {
        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Fire?.Invoke(screenPosition);
    }

    void OnDestroy() {
        _playerInput.actions["Fire"].performed -= OnFirePerformed;
    }
}

