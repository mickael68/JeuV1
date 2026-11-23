using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sons du jeu (Assets/Sounds) : retrouvés par mots-clés dans le nom du fichier, réglages d'import,
/// puis branchés sur les néons, les portes, l'ascenseur, les pas du joueur et les ambiances.
/// </summary>
public static partial class EchoesMapGenerator
{
    const string SoundsFolder = "Assets/Sounds";
    static Dictionary<string, AudioClip> _soundCache;

    /// Premier son dont le nom contient tous les mots-clés (insensible à la casse), sinon null.
    static AudioClip Snd(params string[] keywords)
    {
        if (_soundCache == null) _soundCache = new Dictionary<string, AudioClip>();
        string key = string.Join("|", keywords);
        if (_soundCache.TryGetValue(key, out var c)) return c;
        c = null;
        if (AssetDatabase.IsValidFolder(SoundsFolder))
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SoundsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Menu/")) continue;
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                bool all = true;
                foreach (var k in keywords) if (!name.Contains(k.ToLowerInvariant())) { all = false; break; }
                if (all) { c = AssetDatabase.LoadAssetAtPath<AudioClip>(path); break; }
            }
        _soundCache[key] = c;
        return c;
    }

    // Sons utilisés
    static AudioClip SndNeonBuzz => Snd("fluorescent-light-buzz") ?? Snd("fluores");
    static AudioClip SndDoorOpen => Snd("open-door");
    static AudioClip SndDoorClose => Snd("open-and-close-door") ?? Snd("door", "close");
    static AudioClip SndDoorCreak => Snd("door-creak") ?? Snd("creak");
    static AudioClip SndStepConcrete => Snd("concrete-footsteps") ?? Snd("footsteps", "concrete");
    static AudioClip SndStepWood => Snd("footsteps-on-wooden") ?? Snd("footsteps", "wood");
    static AudioClip SndStepGrass => Snd("walking-through-grass") ?? Snd("grass");
    static AudioClip SndAmbResidence => Snd("room-tone");
    static AudioClip SndAmbOutside => Snd("crickets");
    static AudioClip SndAmbBasement => Snd("industrial-sounds-ambience") ?? Snd("industrial");
    static AudioClip SndAmbDeep => Snd("deep-haunting-drone") ?? Snd("drone");
    static AudioClip SndAmbAbyss => Snd("creepy-dark-drone") ?? Snd("dark-drone");
    static AudioClip SndElevatorArrival => Snd("elevator");

    /// Import : longues ambiances en streaming (pas tout en mémoire), bruitages décompressés (pas : analyse des débuts de pas).
    static void ConfigureSoundImports()
    {
        _soundCache = null;
        if (!AssetDatabase.IsValidFolder(SoundsFolder)) return;
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SoundsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Menu/")) continue;
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) continue;
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            bool steps = name.Contains("footstep") || name.Contains("walking");
            bool ambience = name.Contains("ambience") || name.Contains("drone") || name.Contains("room-tone") || name.Contains("crickets");
            var s = imp.defaultSampleSettings;
            // Ambiances (une ou deux à la fois) en streaming ; le reste en mémoire (34 néons grésillent en même temps)
            var load = steps ? AudioClipLoadType.DecompressOnLoad
                     : ambience ? AudioClipLoadType.Streaming
                     : AudioClipLoadType.CompressedInMemory;
            bool changed = s.loadType != load || s.preloadAudioData != steps || imp.forceToMono != !name.Contains("ambience") || imp.loadInBackground != !steps;
            if (!changed) continue;
            s.loadType = load;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            s.preloadAudioData = steps;
            imp.defaultSampleSettings = s;
            imp.forceToMono = !name.Contains("ambience");
            imp.loadInBackground = !steps;
            imp.SaveAndReimport();
        }
        _soundCache = null;
    }

    /// Pas du joueur et ambiances (objets créés une fois, sons réassignés à chaque génération).
    static void AssignPlayerAndAmbienceSounds()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var fs = player.GetComponent<Footsteps>();
            if (fs == null) fs = player.AddComponent<Footsteps>();
            fs.concrete = SndStepConcrete;
            fs.wood = SndStepWood;
            fs.grass = SndStepGrass;
            EditorUtility.SetDirty(fs);
        }

        var sys = GameObject.Find("Systemes");
        if (sys != null)
        {
            var amb = sys.GetComponent<AmbienceManager>();
            if (amb == null) amb = sys.AddComponent<AmbienceManager>();
            amb.residence = SndAmbResidence;
            amb.outside = SndAmbOutside;
            amb.basement = SndAmbBasement;
            amb.deep = SndAmbDeep;
            amb.abyss = SndAmbAbyss;
            amb.building = Rect.MinMaxRect(-HalfX - 0.3f, CorrS - RoomDepth - 0.3f, HalfX + 0.3f, CorrN + RoomDepth + 0.3f);
            amb.groundY = _levels[1].y;
            EditorUtility.SetDirty(amb);
        }
    }
}
