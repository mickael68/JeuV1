"""
Script Blender pour générer un personnage stylisé (femme étudiante)
Basé sur : T-shirt blanc, jean bleu, baskets roses, cheveux bruns mi-longs

UTILISATION :
1. Ouvrir Blender (version 3.6+ recommandée)
2. Aller dans l'onglet "Scripting" en haut
3. Cliquer "New" pour créer un nouveau script
4. Coller ce script et cliquer "Run Script" (▶)
5. Le personnage apparaîtra au centre de la scène
6. Exporter ensuite via : File > Export > glTF 2.0 (.glb)
   - Cocher "Apply Modifiers"
   - Sauver dans Assets/Models/Characters/
"""

import bpy
import bmesh
import math
from mathutils import Vector


def clear_scene():
    """Supprime tous les objets de la scène"""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    # Supprimer les matériaux orphelins
    for mat in bpy.data.materials:
        if mat.users == 0:
            bpy.data.materials.remove(mat)


def create_material(name, color, roughness=0.8, metallic=0.0):
    """Crée un matériau PBR simple"""
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return mat


def create_body_part(name, location, scale, material, subdivisions=2):
    """Crée une partie du corps à partir d'une UV Sphere subdivisée"""
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=16, ring_count=12,
        radius=1.0,
        location=location
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.shade_smooth()

    # Appliquer le matériau
    obj.data.materials.append(material)
    return obj


def create_cube_part(name, location, scale, material):
    """Crée une partie à partir d'un cube arrondi"""
    bpy.ops.mesh.primitive_cube_add(location=location, size=1.0)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale

    # Subdivision pour arrondir
    bpy.ops.object.modifier_add(type='SUBSURF')
    obj.modifiers["Subdivision"].levels = 2
    obj.modifiers["Subdivision"].render_levels = 2
    bpy.ops.object.shade_smooth()

    obj.data.materials.append(material)
    return obj


def create_cylinder_part(name, location, scale, material, vertices=16):
    """Crée une partie à partir d'un cylindre"""
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=1.0,
        depth=1.0,
        location=location
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.shade_smooth()

    obj.data.materials.append(material)
    return obj


def generate_character():
    """Génère le personnage complet"""

    clear_scene()

    # =========================================================
    #  MATÉRIAUX
    # =========================================================

    # Peau
    mat_skin = create_material("Skin", (0.87, 0.72, 0.60, 1.0), roughness=0.6)

    # T-shirt blanc
    mat_tshirt = create_material("TShirt_White", (0.95, 0.95, 0.93, 1.0), roughness=0.85)

    # Jean bleu
    mat_jeans = create_material("Jeans_Blue", (0.25, 0.35, 0.55, 1.0), roughness=0.9)

    # Baskets roses
    mat_shoes = create_material("Shoes_Pink", (0.85, 0.35, 0.45, 1.0), roughness=0.7)

    # Semelle blanche
    mat_sole = create_material("Shoe_Sole", (0.95, 0.92, 0.88, 1.0), roughness=0.6)

    # Cheveux bruns
    mat_hair = create_material("Hair_Brown", (0.22, 0.12, 0.06, 1.0), roughness=0.75)

    # Yeux
    mat_eyes = create_material("Eyes", (0.15, 0.35, 0.25, 1.0), roughness=0.3)
    mat_eye_white = create_material("Eye_White", (0.95, 0.95, 0.95, 1.0), roughness=0.3)

    all_parts = []

    # =========================================================
    #  CORPS - TORSE (T-shirt blanc)
    # =========================================================

    # Torse principal
    torso = create_body_part(
        "Torso", (0, 0, 1.15),
        (0.28, 0.18, 0.32), mat_tshirt
    )
    all_parts.append(torso)

    # Poitrine (arrondi subtil)
    chest = create_body_part(
        "Chest", (0, -0.1, 1.25),
        (0.26, 0.12, 0.15), mat_tshirt
    )
    all_parts.append(chest)

    # Épaules
    for side, x in [("L", -0.3), ("R", 0.3)]:
        shoulder = create_body_part(
            f"Shoulder_{side}", (x, 0, 1.38),
            (0.1, 0.09, 0.08), mat_tshirt
        )
        all_parts.append(shoulder)

    # Cou
    neck = create_cylinder_part(
        "Neck", (0, 0, 1.48),
        (0.07, 0.07, 0.08), mat_skin
    )
    all_parts.append(neck)

    # Ventre / taille (transition t-shirt → jean)
    waist = create_body_part(
        "Waist", (0, 0, 0.88),
        (0.22, 0.15, 0.1), mat_tshirt
    )
    all_parts.append(waist)

    # =========================================================
    #  BRAS (peau - manches courtes)
    # =========================================================

    for side, x_sign in [("L", -1), ("R", 1)]:
        # Haut du bras (manche t-shirt)
        upper_arm = create_cylinder_part(
            f"UpperArm_{side}", (x_sign * 0.38, 0, 1.3),
            (0.065, 0.065, 0.15), mat_tshirt
        )
        upper_arm.rotation_euler = (0, 0, x_sign * 0.15)
        all_parts.append(upper_arm)

        # Avant-bras (peau)
        forearm = create_cylinder_part(
            f"Forearm_{side}", (x_sign * 0.42, 0, 1.1),
            (0.055, 0.055, 0.15), mat_skin
        )
        forearm.rotation_euler = (0, 0, x_sign * 0.1)
        all_parts.append(forearm)

        # Main
        hand = create_body_part(
            f"Hand_{side}", (x_sign * 0.45, 0, 0.95),
            (0.04, 0.025, 0.06), mat_skin
        )
        all_parts.append(hand)

    # =========================================================
    #  TÊTE
    # =========================================================

    # Tête
    head = create_body_part(
        "Head", (0, 0, 1.65),
        (0.12, 0.14, 0.15), mat_skin
    )
    all_parts.append(head)

    # Yeux
    for side, x in [("L", -0.045), ("R", 0.045)]:
        eye_white = create_body_part(
            f"EyeWhite_{side}", (x, -0.11, 1.68),
            (0.025, 0.015, 0.02), mat_eye_white
        )
        all_parts.append(eye_white)

        eye_iris = create_body_part(
            f"Eye_{side}", (x, -0.125, 1.68),
            (0.015, 0.01, 0.015), mat_eyes
        )
        all_parts.append(eye_iris)

    # Nez
    nose = create_body_part(
        "Nose", (0, -0.14, 1.64),
        (0.02, 0.025, 0.025), mat_skin
    )
    all_parts.append(nose)

    # Bouche (lèvres)
    mat_lips = create_material("Lips", (0.75, 0.45, 0.42, 1.0), roughness=0.5)
    mouth = create_body_part(
        "Mouth", (0, -0.115, 1.59),
        (0.035, 0.015, 0.01), mat_lips
    )
    all_parts.append(mouth)

    # =========================================================
    #  CHEVEUX BRUNS MI-LONGS
    # =========================================================

    # Calotte principale
    hair_top = create_body_part(
        "Hair_Top", (0, 0.01, 1.72),
        (0.135, 0.155, 0.12), mat_hair
    )
    all_parts.append(hair_top)

    # Frange (devant)
    hair_bangs = create_body_part(
        "Hair_Bangs", (0, -0.1, 1.74),
        (0.12, 0.06, 0.05), mat_hair
    )
    all_parts.append(hair_bangs)

    # Cheveux côtés et arrière (mi-longs, tombant sur les épaules)
    for side, x in [("L", -0.08), ("R", 0.08)]:
        hair_side = create_body_part(
            f"Hair_Side_{side}", (x, 0.04, 1.55),
            (0.08, 0.07, 0.18), mat_hair
        )
        all_parts.append(hair_side)

    # Cheveux arrière (tombant dans le dos)
    hair_back = create_body_part(
        "Hair_Back", (0, 0.1, 1.5),
        (0.12, 0.08, 0.25), mat_hair
    )
    all_parts.append(hair_back)

    # =========================================================
    #  JAMBES (Jean bleu)
    # =========================================================

    # Hanches / bassin
    hips = create_body_part(
        "Hips", (0, 0, 0.78),
        (0.24, 0.16, 0.12), mat_jeans
    )
    all_parts.append(hips)

    for side, x in [("L", -0.12), ("R", 0.12)]:
        # Cuisse
        thigh = create_cylinder_part(
            f"Thigh_{side}", (x, 0, 0.58),
            (0.09, 0.085, 0.18), mat_jeans
        )
        all_parts.append(thigh)

        # Mollet
        calf = create_cylinder_part(
            f"Calf_{side}", (x, 0, 0.28),
            (0.07, 0.065, 0.16), mat_jeans
        )
        all_parts.append(calf)

    # =========================================================
    #  CHAUSSURES (Baskets roses style Converse)
    # =========================================================

    for side, x in [("L", -0.12), ("R", 0.12)]:
        # Corps de la chaussure
        shoe = create_cube_part(
            f"Shoe_{side}", (x, -0.02, 0.06),
            (0.08, 0.15, 0.06), mat_shoes
        )
        all_parts.append(shoe)

        # Semelle blanche
        sole = create_cube_part(
            f"Sole_{side}", (x, -0.02, 0.02),
            (0.085, 0.16, 0.025), mat_sole
        )
        all_parts.append(sole)

        # Bout rond blanc (Converse-style)
        toe_cap = create_body_part(
            f"ToeCap_{side}", (x, -0.1, 0.06),
            (0.055, 0.04, 0.04), mat_sole
        )
        all_parts.append(toe_cap)

    # =========================================================
    #  FINALISATION
    # =========================================================

    # Sélectionner tous les objets du personnage
    bpy.ops.object.select_all(action='DESELECT')
    for part in all_parts:
        part.select_set(True)

    # Activer le dernier objet
    bpy.context.view_layer.objects.active = all_parts[0]

    # Joindre tous les objets en un seul mesh
    bpy.ops.object.join()

    # Renommer l'objet final
    character = bpy.context.active_object
    character.name = "Character_Student"

    # Appliquer les modifications
    bpy.ops.object.select_all(action='DESELECT')
    character.select_set(True)
    bpy.context.view_layer.objects.active = character

    # Appliquer les modifiers de subdivision
    for mod in character.modifiers:
        bpy.ops.object.modifier_apply(modifier=mod.name)

    # Centrer l'origine
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')

    # Repositionner pour que les pieds soient à Z=0
    character.location.z = -character.bound_box[0][2] * character.scale.z

    print("=" * 50)
    print("✅ Personnage généré avec succès !")
    print("=" * 50)
    print(f"Nom : {character.name}")
    print(f"Vertices : {len(character.data.vertices)}")
    print(f"Faces : {len(character.data.polygons)}")
    print("")
    print("Pour exporter vers Godot :")
    print("  File > Export > glTF 2.0 (.glb/.gltf)")
    print("  Sauver dans : Assets/Models/Characters/student.glb")
    print("=" * 50)


# ============================================================
#  EXÉCUTION
# ============================================================

if __name__ == "__main__":
    generate_character()
