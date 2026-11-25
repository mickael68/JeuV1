using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Petit parking devant le portail de la résidence (hors de la cour clôturée) : accès depuis le portail,
/// allée centrale, deux rangées de places marquées au sol, butées, lampadaires, voitures garées.
/// Voitures : tout prefab placé dans Assets/Vehicles (pack de voitures de l'Asset Store) ; sinon, et en plus,
/// quelques voitures bâchées (Poly Haven).
/// </summary>
public static partial class EchoesMapGenerator
{
    static readonly Rect ParkingRect = Rect.MinMaxRect(47f, -15f, 64f, 10f);
    const string VehiclesFolder = "Assets/Vehicles";

    static void BuildParking(Transform ext, LevelDef X, float gy)
    {
        var root = Child(ext, "Parking");
        var P = new LevelDef { number = 1, y = gy, h = 4f, root = root, props = Child(root, "Props"), markers = X.markers, lightColor = X.lightColor, lightIntensity = X.lightIntensity };
        var asphalt = Mat("Parking_Asphalte", Color.white);
        var paint = Mat("Parking_Peinture", new Color(0.92f, 0.92f, 0.88f), 0.2f);
        var r = ParkingRect;

        // Revêtement : accès depuis le portail + parking
        Box(root, "Acces_Parking", new Vector3(YardRect.xMax, gy, ExitZ - 2.5f), new Vector3(r.xMin, gy + 0.02f, ExitZ + 2.5f), asphalt, _layerGround);
        Box(root, "Parking", new Vector3(r.xMin, gy, r.yMin), new Vector3(r.xMax, gy + 0.02f, r.yMax), asphalt, _layerGround);
        var curb = Mat("Facade_Bandeau", new Color(0.82f, 0.8f, 0.75f));
        foreach (var (a, b) in new[] { (new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMax, 0, r.yMin)), (new Vector3(r.xMin, 0, r.yMax), new Vector3(r.xMax, 0, r.yMax)),
                                        (new Vector3(r.xMax, 0, r.yMin), new Vector3(r.xMax, 0, r.yMax)) })
        {
            var c = (a + b) * 0.5f; var d = b - a;
            Cube(root, "Bordure_Parking", new Vector3(c.x, gy + 0.07f, c.z), new Vector3(Mathf.Max(0.15f, Mathf.Abs(d.x)), 0.14f, Mathf.Max(0.15f, Mathf.Abs(d.z))), Quaternion.identity, curb, true, _layerGround);
        }

        // Places : deux rangées perpendiculaires à l'allée centrale (en z), 2,5 m de large, 5 m de profondeur
        const float slotW = 2.5f, slotD = 5f;
        float laneX0 = r.xMin + slotD, laneX1 = r.xMax - slotD;                 // allée centrale
        var slots = new List<(Vector3 c, float yaw)>();
        for (float z = r.yMin + 0.6f; z + slotW <= r.yMax - 0.4f; z += slotW)
        {
            float zc = z + slotW * 0.5f;
            bool entrance = Mathf.Abs(zc - ExitZ) < 3.2f;                         // pas de place devant l'accès
            if (!entrance)
            {
                slots.Add((new Vector3(r.xMin + slotD * 0.5f, gy, zc), 90f));      // rangée ouest, avant vers l'allée
                Line(root, paint, new Vector3(r.xMin + slotD * 0.5f, gy, z), slotD, true);
            }
            slots.Add((new Vector3(r.xMax - slotD * 0.5f, gy, zc), -90f));          // rangée est
            Line(root, paint, new Vector3(r.xMax - slotD * 0.5f, gy, z), slotD, true);
            if (!entrance || z + slotW * 2f > r.yMax) Line(root, paint, new Vector3(r.xMin + slotD * 0.5f, gy, z + slotW), slotD, true);
            Line(root, paint, new Vector3(r.xMax - slotD * 0.5f, gy, z + slotW), slotD, true);
        }
        // Lignes de fond de place et pointillés de l'allée
        Line(root, paint, new Vector3(laneX0, gy, r.center.y), r.height - 1f, false);
        Line(root, paint, new Vector3(laneX1, gy, r.center.y), r.height - 1f, false);

        // Voitures : pack de l'Asset Store (Assets/Vehicles) si présent, sinon voitures bâchées
        var cars = new List<GameObject>();
        if (AssetDatabase.IsValidFolder(VehiclesFolder))
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { VehiclesFolder }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null && go.GetComponentInChildren<Renderer>() != null) cars.Add(go);
            }
        int parked = 0, covered = 0;
        foreach (var (c, yaw) in slots)
        {
            double roll = _rng.NextDouble();
            if (roll < 0.35) continue;                                            // place libre
            var pos = c + new Vector3(Rand(-0.15f, 0.15f), 0.02f, Rand(-0.15f, 0.15f));
            float y = yaw + Rand(-3f, 3f) + (_rng.NextDouble() < 0.5 ? 0f : 180f) * (cars.Count > 0 ? 0f : 1f);
            GameObject car = null;
            if (cars.Count > 0 && roll > 0.5)
            {
                car = (GameObject)PrefabUtility.InstantiatePrefab(cars[_rng.Next(cars.Count)], P.props);
                FitCar(car, pos, y + CarYawFix(car), 4.4f);
                parked++;
            }
            else
            {
                car = CaveProp(P, "covered_car", pos, 0f, Vector3.one * 10f);
                if (car != null) FitCar(car, pos, y + CarYawFix(car), 4.5f);
                covered++;
            }
            if (car != null)
            {
                foreach (var rd in car.GetComponentsInChildren<Rigidbody>()) Object.DestroyImmediate(rd);
                if (car.GetComponentInChildren<Collider>() == null && TryBounds(car, out var cb))
                {
                    var bc = car.AddComponent<BoxCollider>();
                    bc.center = car.transform.InverseTransformPoint(cb.center);
                    var ls = car.transform.lossyScale;
                    var sz = Quaternion.Inverse(car.transform.rotation) * cb.size;
                    bc.size = new Vector3(Mathf.Abs(sz.x / ls.x), Mathf.Abs(sz.y / ls.y), Mathf.Abs(sz.z / ls.z)) * 0.95f;
                }
            }
            // Butée de roue au fond de la place
            var back = c + new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad)) * (slotD * 0.5f - 0.45f);
            Cube(root, "Butee", new Vector3(back.x, gy + 0.07f, back.z), new Vector3(Mathf.Abs(Mathf.Cos(yaw * Mathf.Deg2Rad)) > 0.5f ? 1.6f : 0.2f, 0.12f, Mathf.Abs(Mathf.Cos(yaw * Mathf.Deg2Rad)) > 0.5f ? 0.2f : 1.6f), Quaternion.identity, curb, false, -1);
        }

        // Lampadaires, barrières en béton au bord, panneau
        foreach (var z in new[] { r.yMin + 3f, r.center.y + 3f, r.yMax - 3f })
            StreetLamp(P, new Vector3(r.xMax + 0.8f, gy, z));
        StreetLamp(P, new Vector3(r.xMin - 0.8f, gy, r.yMin + 2f));
        StreetLamp(P, new Vector3(r.xMin - 0.8f, gy, r.yMax - 2f));
        foreach (var z in new[] { r.yMin + 5f, r.yMax - 5f })
            CaveProp(P, "concrete_road_barrier", new Vector3(r.xMax + 2.2f, gy, z), 90f, new Vector3(0.6f, 0.8f, 3.6f), true);
        PlaqueBig(P, "PARKING RÉSIDENTS", new Vector3(r.xMin - 0.3f, gy + 2.2f, ExitZ + 3.4f), Vector3.left, 0.012f, 1.8f, 0.3f);
        Cylinder(root, "Poteau_Panneau", new Vector3(r.xMin - 0.32f, gy + 1.05f, ExitZ + 3.4f), 0.04f, 2.1f, Quaternion.identity, Mat("Grille_Metal", new Color(0.18f, 0.18f, 0.2f), 0.5f), true);
        CaveView(X, "Vue_Parking", new Vector3(YardRect.xMax - 1f, gy, ExitZ), new Vector3(r.center.x + 2f, gy, r.center.y - 3f));
        Debug.Log($"[EchoesMap] Parking : {slots.Count} places, {parked} voitures du pack, {covered} voitures bâchées" +
                  (cars.Count == 0 ? " (aucun modèle dans Assets/Vehicles)." : "."));
    }

    /// Ligne de peinture au sol (centre, longueur, le long de x ou de z).
    static void Line(Transform p, Material m, Vector3 center, float length, bool alongX)
    {
        var size = alongX ? new Vector3(length, 0.005f, 0.1f) : new Vector3(0.1f, 0.005f, length);
        Cube(p, "Marquage", center + Vector3.up * 0.023f, size, Quaternion.identity, m, false, -1)
            .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// Met une voiture du pack à la bonne taille (longueur), posée au sol.
    static void FitCar(GameObject car, Vector3 pos, float yaw, float length)
    {
        var src = PrefabUtility.GetCorrespondingObjectFromSource(car);
        var baseRot = src != null ? src.transform.localRotation : Quaternion.identity;
        car.transform.SetPositionAndRotation(Vector3.zero, baseRot);
        car.transform.localScale = Vector3.one;
        if (!TryBounds(car, out var b)) { car.transform.position = pos; return; }
        float s = length / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
        car.transform.localScale = Vector3.one * s;
        car.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * baseRot;
        Ground(car, pos);
    }

    /// Les voitures des packs sont souvent modélisées le long de X : on les tourne pour qu'elles suivent la place.
    static float CarYawFix(GameObject car)
    {
        var src = PrefabUtility.GetCorrespondingObjectFromSource(car);
        var keep = car.transform.rotation;
        car.transform.rotation = src != null ? src.transform.localRotation : Quaternion.identity;
        bool alongX = TryBounds(car, out var b) && b.size.x > b.size.z;
        car.transform.rotation = keep;
        return alongX ? 90f : 0f;
    }
}
