using UnityEngine;
using TMPro;

/// <summary>
/// Gère les interactions joueur via raycast depuis la caméra.
/// Remplace le RayCast3D Godot par Physics.Raycast Unity.
/// </summary>
public class InteractionManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactableMask = ~0; // Toutes les couches par défaut

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI interactionLabel;

    private Camera _camera;

    private void Start()
    {
        _camera = Camera.main;
    }

    private void Update()
    {
        // Pas d'interaction pendant la pause (souris libérée)
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (interactionLabel != null) interactionLabel.gameObject.SetActive(false);
            return;
        }

        // Raycast depuis le centre de l'écran
        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableMask))
        {
            // Cherche l'interface IInteractable sur l'objet touché ou ses parents
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {
                if (interactionLabel != null)
                {
                    interactionLabel.text = $"[E] {interactable.GetInteractText()}";
                    interactionLabel.gameObject.SetActive(true);
                }

                // Input System : touche E pour interagir
                if (Input.GetKeyDown(KeyCode.E))
                {
                    interactable.Interact();
                }
                return;
            }
        }

        // Rien en vue
        if (interactionLabel != null)
        {
            interactionLabel.gameObject.SetActive(false);
        }
    }
}
