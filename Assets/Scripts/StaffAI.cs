using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// IA des surveillants — patrouille la nuit, détecte le joueur.
/// Remplace le système Godot par NavMeshAgent + Physics.Raycast.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class StaffAI : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 3.0f;

    [Header("Patrol")]
    [SerializeField] private Transform patrolPointsParent; // Un objet contenant les waypoints enfants

    [Header("Detection")]
    [SerializeField] private float viewDistance = 10.0f;
    [SerializeField] private float viewAngleDegrees = 45.0f;
    [SerializeField] private LayerMask detectionMask = ~0;

    private NavMeshAgent _agent;
    private List<Vector3> _waypoints = new List<Vector3>();
    private int _currentWaypointIndex = 0;
    private Transform _player;
    private bool _isNight = false;

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = speed;

        // Trouver le joueur
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        // Récupérer les points de patrouille
        if (patrolPointsParent != null)
        {
            foreach (Transform child in patrolPointsParent)
            {
                _waypoints.Add(child.position);
            }
        }

        // Se connecter au TimeManager
        var timeManager = FindFirstObjectByType<TimeManager>();
        if (timeManager != null)
        {
            timeManager.OnNightStarted += () => _isNight = true;
            timeManager.OnDayStarted += () => _isNight = false;
        }
    }

    private void Update()
    {
        if (!_isNight || _waypoints.Count == 0) return;

        // 1. Patrouille via NavMesh
        if (!_agent.pathPending && _agent.remainingDistance < 1.0f)
        {
            _currentWaypointIndex = (_currentWaypointIndex + 1) % _waypoints.Count;
            _agent.SetDestination(_waypoints[_currentWaypointIndex]);
        }

        // 2. Détection du joueur
        CheckForPlayer();
    }

    private void CheckForPlayer()
    {
        if (_player == null) return;

        Vector3 toPlayer = _player.position - transform.position;
        float distance = toPlayer.magnitude;

        if (distance < viewDistance)
        {
            // Vérifier l'angle de vue
            Vector3 forward = transform.forward;
            float angleToPlayer = Vector3.Angle(forward, toPlayer);

            if (angleToPlayer < viewAngleDegrees)
            {
                // Raycast pour vérifier les murs
                Vector3 eyePosition = transform.position + Vector3.up * 1.5f;
                Vector3 playerHead = _player.position + Vector3.up * 1.5f;
                Vector3 direction = (playerHead - eyePosition).normalized;

                if (Physics.Raycast(eyePosition, direction, out RaycastHit hit, viewDistance, detectionMask))
                {
                    if (hit.collider.CompareTag("Player"))
                    {
                        OnPlayerDetected();
                    }
                }
            }
        }
    }

    private void OnPlayerDetected()
    {
        Debug.Log("ALERTE : Un surveillant vous a repéré ! Dehors après le couvre-feu !");
        // TODO : Déclencher un écran "Game Over" ou une punition
    }
}
