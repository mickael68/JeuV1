using UnityEngine;
using UnityEngine.UI;

/// <summary>Onglets simples : un bouton par page, une seule page visible à la fois.</summary>
public class TabGroup : MonoBehaviour
{
    public Button[] tabs;
    public GameObject[] pages;
    public Color activeColor = new Color(1f, 0.7f, 0.2f, 0.9f);
    public Color idleColor = new Color(1f, 1f, 1f, 0.07f);

    private void Awake()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int k = i;
            if (tabs[i] != null) tabs[i].onClick.AddListener(() => Show(k));
        }
    }

    private void OnEnable() => Show(0);

    public void Show(int index)
    {
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == index);
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] == null) continue;
            var img = tabs[i].targetGraphic as Image;
            if (img != null) img.color = i == index ? activeColor : idleColor;
            var label = tabs[i].GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (label != null) label.color = i == index ? new Color(0.08f, 0.06f, 0.04f) : Color.white;
        }
    }
}
