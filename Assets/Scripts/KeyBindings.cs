using System.Collections.Generic;
using UnityEngine;

/// <summary>Actions du joueur dont la touche se choisit dans les options.</summary>
public enum GameAction
{
    Forward,
    Backward,
    Left,
    Right,
    Jump,
    Sprint,
    Crouch,
    Interact,
    Flashlight,
    UseItem,
    DropItem,
    Inventory,
}

/// <summary>
/// Touches du joueur, modifiables dans les options et sauvegardées (PlayerPrefs "key_&lt;action&gt;").
/// Les flèches restent toujours actives pour se déplacer, et Échap pour la pause.
/// Attention : les KeyCode de Unity désignent la POSITION de la touche sur un clavier QWERTY
/// (KeyCode.W = la touche Z d'un clavier AZERTY). Le nom affiché est donc lu dans la disposition
/// réelle du clavier (Input System), pour montrer "Z" et pas "W" sur un clavier français.
/// </summary>
public static class KeyBindings
{
    // Positions physiques : Z Q S D sur un clavier AZERTY, W A S D sur un clavier QWERTY
    static readonly Dictionary<GameAction, KeyCode> Defaults = new Dictionary<GameAction, KeyCode>
    {
        [GameAction.Forward] = KeyCode.W,
        [GameAction.Backward] = KeyCode.S,
        [GameAction.Left] = KeyCode.A,
        [GameAction.Right] = KeyCode.D,
        [GameAction.Jump] = KeyCode.Space,
        [GameAction.Sprint] = KeyCode.LeftShift,
        [GameAction.Crouch] = KeyCode.LeftControl,
        [GameAction.Interact] = KeyCode.E,
        [GameAction.Flashlight] = KeyCode.F,
        [GameAction.UseItem] = KeyCode.Mouse0,
        [GameAction.DropItem] = KeyCode.G,
        [GameAction.Inventory] = KeyCode.Tab,
    };

    public static readonly GameAction[] All =
    {
        GameAction.Forward, GameAction.Backward, GameAction.Left, GameAction.Right,
        GameAction.Jump, GameAction.Sprint, GameAction.Crouch, GameAction.Interact, GameAction.Flashlight,
        GameAction.UseItem, GameAction.DropItem, GameAction.Inventory,
    };

    static Dictionary<GameAction, KeyCode> _keys;

    static Dictionary<GameAction, KeyCode> Keys
    {
        get
        {
            if (_keys == null) Load();
            return _keys;
        }
    }

    public static KeyCode Get(GameAction a) => Keys[a];
    public static bool Held(GameAction a) => Input.GetKey(Keys[a]);
    public static bool Down(GameAction a) => Input.GetKeyDown(Keys[a]);

    /// Axe de déplacement (-1, 0, 1) : touches choisies + flèches.
    public static float Axis(GameAction negative, GameAction positive, KeyCode negArrow, KeyCode posArrow)
    {
        float v = 0f;
        if (Held(negative) || Input.GetKey(negArrow)) v -= 1f;
        if (Held(positive) || Input.GetKey(posArrow)) v += 1f;
        return v;
    }

    /// Change la touche d'une action. Si une autre action l'utilisait déjà, les deux touches sont échangées.
    public static void Set(GameAction a, KeyCode key)
    {
        var old = Keys[a];
        foreach (var other in All)
            if (other != a && Keys[other] == key) Keys[other] = old;
        Keys[a] = key;
        Save();
    }

    public static void ResetDefaults()
    {
        _keys = new Dictionary<GameAction, KeyCode>(Defaults);
        Save();
    }

    const int Version = 2; // v1 enregistrait Z/Q/S/D comme des positions QWERTY (inversé sur AZERTY)

    static void Load()
    {
        _keys = new Dictionary<GameAction, KeyCode>(Defaults);
        if (PlayerPrefs.GetInt("key_version", 1) < Version)
        {
            Save(); // anciennes touches abandonnées : on repart des valeurs par défaut
            return;
        }
        foreach (var a in All)
        {
            int v = PlayerPrefs.GetInt("key_" + a, -1);
            if (v >= 0 && System.Enum.IsDefined(typeof(KeyCode), v)) _keys[a] = (KeyCode)v;
        }
    }

    static void Save()
    {
        foreach (var a in All) PlayerPrefs.SetInt("key_" + a, (int)_keys[a]);
        PlayerPrefs.SetInt("key_version", Version);
        PlayerPrefs.Save();
    }

    public static string ActionName(GameAction a)
    {
        switch (a)
        {
            case GameAction.Forward: return "Avancer";
            case GameAction.Backward: return "Reculer";
            case GameAction.Left: return "Aller à gauche";
            case GameAction.Right: return "Aller à droite";
            case GameAction.Jump: return "Sauter";
            case GameAction.Sprint: return "Courir";
            case GameAction.Crouch: return "S'accroupir";
            case GameAction.Interact: return "Interagir";
            case GameAction.Flashlight: return "Lampe torche";
            case GameAction.UseItem: return "Utiliser l'objet";
            case GameAction.DropItem: return "Lâcher l'objet";
            case GameAction.Inventory: return "Détail de l'inventaire";
            default: return a.ToString();
        }
    }

    /// Nom lisible (en français) d'une touche.
    public static string KeyName(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.Space: return "Espace";
            case KeyCode.LeftShift: return "Maj gauche";
            case KeyCode.RightShift: return "Maj droite";
            case KeyCode.LeftControl: return "Ctrl gauche";
            case KeyCode.RightControl: return "Ctrl droite";
            case KeyCode.LeftAlt: return "Alt";
            case KeyCode.RightAlt: return "Alt Gr";
            case KeyCode.Tab: return "Tab";
            case KeyCode.CapsLock: return "Verr. maj";
            case KeyCode.Return: return "Entrée";
            case KeyCode.KeypadEnter: return "Entrée (pavé)";
            case KeyCode.Backspace: return "Retour arrière";
            case KeyCode.Delete: return "Suppr";
            case KeyCode.Insert: return "Inser";
            case KeyCode.UpArrow: return "Flèche haut";
            case KeyCode.DownArrow: return "Flèche bas";
            case KeyCode.LeftArrow: return "Flèche gauche";
            case KeyCode.RightArrow: return "Flèche droite";
            case KeyCode.Mouse0: return "Clic gauche";
            case KeyCode.Mouse1: return "Clic droit";
            case KeyCode.Mouse2: return "Clic molette";
            case KeyCode.Mouse3: return "Souris 4";
            case KeyCode.Mouse4: return "Souris 5";
        }
        string layout = LayoutName(k);
        if (!string.IsNullOrEmpty(layout)) return layout;
        string s = k.ToString();
        if (s.StartsWith("Alpha")) return s.Substring(5);
        if (s.StartsWith("Keypad")) return s.Substring(6) + " (pavé)";
        return s;
    }

    /// Caractère réellement imprimé sur la touche, d'après la disposition du clavier (AZERTY, QWERTY…).
    static string LayoutName(KeyCode k)
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return null;
        string n = k.ToString();
        if (n.StartsWith("Alpha")) n = "Digit" + n.Substring(5);
        else if (n.StartsWith("Keypad")) return null; // le pavé numérique garde son nom
        else if (n == "BackQuote") n = "Backquote";
        if (!System.Enum.TryParse(n, out UnityEngine.InputSystem.Key key) || key == UnityEngine.InputSystem.Key.None) return null;
        try
        {
            var control = kb[key];
            string d = control != null ? control.displayName : null;
            return string.IsNullOrWhiteSpace(d) ? null : (d.Length == 1 ? d.ToUpperInvariant() : d);
        }
        catch (System.ArgumentOutOfRangeException) { return null; }
    }
}
