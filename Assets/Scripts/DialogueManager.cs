using UnityEngine;
using TMPro;

/// <summary>
/// Système de dialogue simple avec simulation IA.
/// Remplace le Control/RichTextLabel Godot par Canvas/TextMeshPro Unity.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TextMeshProUGUI displayText;
    [SerializeField] private UnityEngine.UI.Button sendButton;

    private string _currentNPCName;

    private void Start()
    {
        dialoguePanel.SetActive(false);
        sendButton.onClick.AddListener(OnSendPressed);
    }

    public void OpenDialogue(string npcName)
    {
        _currentNPCName = npcName;
        dialoguePanel.SetActive(true);
        displayText.text = $"<b>{_currentNPCName}</b> vous regarde. Que voulez-vous lui dire ?";
        inputField.text = "";
        inputField.Select();
        inputField.ActivateInputField();

        // Libérer la souris
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private async void OnSendPressed()
    {
        string userText = inputField.text.Trim();
        if (string.IsNullOrEmpty(userText)) return;

        displayText.text += $"\n\n<color=#00BFFF>Moi :</color> {userText}";
        inputField.text = "";

        // Simulation du temps de réflexion
        displayText.text += $"\n\n<i>{_currentNPCName} réfléchit...</i>";
        await System.Threading.Tasks.Task.Delay(1000);

        // Réponse IA simulée
        string aiResponse = GetMockAIResponse(userText);

        // Nettoyer le "réfléchit..."
        int lastIndex = displayText.text.LastIndexOf($"\n\n<i>{_currentNPCName} réfléchit...</i>");
        if (lastIndex != -1) displayText.text = displayText.text.Substring(0, lastIndex);

        displayText.text += $"\n\n<b>{_currentNPCName} :</b> {aiResponse}";
    }

    private string GetMockAIResponse(string input)
    {
        input = input.ToLower();
        if (input.Contains("bonjour") || input.Contains("salut"))
            return "Salut ! Tu es l'étudiant de la chambre 204, c'est ça ?";
        if (input.Contains("sous-sol") || input.Contains("descendre"))
            return "N'y va pas... Les gens disent qu'ils entendent des bruits bizarres la nuit venant de là-bas.";
        if (input.Contains("crous"))
            return "Le CROUS ? On fait avec ce qu'on a, mais c'est pas le luxe.";

        return "C'est intéressant ce que tu me dis là. Tu m'en diras plus une prochaine fois ?";
    }

    public void CloseDialogue()
    {
        dialoguePanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
