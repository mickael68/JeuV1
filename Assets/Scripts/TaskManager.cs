using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Système de tâches quotidiennes.
/// Porté directement — la logique est identique.
/// </summary>
public class TaskManager : MonoBehaviour
{
    [System.Serializable]
    public class GameTask
    {
        public string Description;
        public bool IsCompleted;
        public GameTask(string desc) { Description = desc; IsCompleted = false; }
    }

    private List<GameTask> _dailyTasks = new List<GameTask>();
    public IReadOnlyList<GameTask> DailyTasks => _dailyTasks;

    // Event pour notifier l'UI
    public event System.Action OnTasksUpdated;

    private void Start()
    {
        GenerateDailyTasks();
    }

    public void GenerateDailyTasks()
    {
        _dailyTasks.Clear();
        _dailyTasks.Add(new GameTask("Aller en cours de mathématiques"));
        _dailyTasks.Add(new GameTask("Vider les poubelles de la cuisine commune"));
        _dailyTasks.Add(new GameTask("Récupérer le courrier à l'accueil"));
        Debug.Log("Nouvelles tâches reçues pour aujourd'hui !");
        OnTasksUpdated?.Invoke();
    }

    public void CompleteTask(int index)
    {
        if (index >= 0 && index < _dailyTasks.Count)
        {
            _dailyTasks[index].IsCompleted = true;
            Debug.Log($"Tâche complétée : {_dailyTasks[index].Description}");
            OnTasksUpdated?.Invoke();
        }
    }
}
