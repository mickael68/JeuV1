using UnityEngine;

public enum ItemKind
{
    Generic,    // objet sans effet particulier (souvenir, indice)
    Key,        // ouvre une porte dont Door.requiredKeyId correspond
    Note,       // se lit (texte affiché à l'écran)
    Battery,    // recharge la lampe torche
    Watch,      // donne l'heure
}

/// <summary>
/// Fiche d'un objet ramassable : nom, description, modèle 3D, effet. (Assets/Items/*.asset)
/// </summary>
[CreateAssetMenu(menuName = "The Echoes Below/Objet", fileName = "Objet")]
public class ItemData : ScriptableObject
{
    public string id = "objet";
    public string displayName = "Objet";
    [TextArea(2, 6)] public string description;
    public ItemKind kind = ItemKind.Generic;
    [Tooltip("Modèle posé dans le monde, et tenu en main.")]
    public GameObject model;
    [Tooltip("Taille (m) du modèle tenu en main.")]
    public float handSize = 0.22f;
    public bool stackable;
    public int maxStack = 1;
    public bool consumedOnUse;

    [Header("Clé")]
    public string keyId;

    [Header("Note")]
    [TextArea(6, 20)] public string noteText;

    [Header("Pile")]
    [Range(0f, 1f)] public float batteryCharge = 1f;
}
