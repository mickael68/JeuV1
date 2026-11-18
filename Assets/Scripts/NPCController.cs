using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NPC étudiant qui se promène aléatoirement et peut être interagi.
/// Remplace CharacterBody3D + custom wander par NavMeshAgent Unity.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NPCController : MonoBehaviour, IInteractable
{
    [Header("NPC Settings")]
    [SerializeField] private float speed = 2.0f;
    [SerializeField] private float wanderRadius = 10.0f;
    [SerializeField] private string npcName = "Étudiant";

    private NavMeshAgent _agent;
    private float _waitTimer = 0f;
    private bool _isWaiting = false;

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = speed;
        SetNewWanderTarget();
    }

    private void Update()
    {
        if (_isWaiting)
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0f)
            {
                _isWaiting = false;
                SetNewWanderTarget();
            }
            return;
        }

        // Vérifie si arrivé à destination
        if (!_agent.pathPending && _agent.remainingDistance < 0.5f)
        {
            _isWaiting = true;
            _waitTimer = Random.Range(2.0f, 5.0f);
        }
    }

    private void SetNewWanderTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += transform.position;

        // Trouve un point valide sur le NavMesh
        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
        {
            _agent.SetDestination(hit.position);
        }
    }

    // ── Interface IInteractable ──
    public string GetInteractText()
    {
        return $"Parler à {npcName}";
    }

    public void Interact()
    {
        var dialogueManager = FindFirstObjectByType<DialogueManager>();

        if (dialogueManager != null)
        {
            dialogueManager.OpenDialogue(npcName);
        }
        else
        {
            Debug.Log($"Bonjour ! Je suis {npcName}, un étudiant de cette résidence.");
        }
    }
}
