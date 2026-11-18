using UnityEngine;

/// <summary>
/// Interface pour les objets interactifs.
/// Identique à la version Godot.
/// </summary>
public interface IInteractable
{
    string GetInteractText();
    void Interact();
}
