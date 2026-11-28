using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HitBoss.Multiplayer.Editor;

namespace HitBoss.Items.Editor
{
    // Variants are ordinary catalog items with independent objects/material assets.
    // Existing inventory, previews, shop, spins and network sync need no special cases.
    public sealed class ItemVariantControls
    {
        PlayerCustomizationManager manager;
        PlayerCustomizationManager.Category category;
        int selected;
        string variantName = "New variant";
        Color tint = Color.white;
        float metallic, roughness = .7f, alpha = 1f;
        Material material;
        string selectedItemName;
        string loadedItemName;
        readonly ItemInspectorPreview preview = new ItemInspectorPreview();
        public void Dispose() => preview.Dispose();
        public void Draw(PlayerCustomizationManager target)
        {
            if (manager != target) { manager = target; selected = 0; LoadProperties(); }
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Item Materials & Duplicates", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            category = (PlayerCustomizationManager.Category)EditorGUILayout.EnumPopup("Category", category);
            var data = Data();
            if (data?.items == null || data.items.Length == 0) { EditorGUI.EndChangeCheck(); EditorGUILayout.HelpBox("Add a model item to this category first.", MessageType.Info); EditorGUILayout.EndVertical(); return; }
            var names = Array.ConvertAll(data.items, i => i?.itemName ?? "Missing item");
            selected = EditorGUILayout.Popup("Selected item", Mathf.Clamp(selected, 0, names.Length - 1), names);
            if (EditorGUI.EndChangeCheck()) LoadProperties();
            if (data.items[selected] != null && data.items[selected].itemName != loadedItemName) LoadProperties();
            if (GUILayout.Button("Reload Current Material Settings")) LoadProperties();
            EditorGUILayout.Space();
            selectedItemName = EditorGUILayout.TextField("Item name", selectedItemName);
            material = (Material)EditorGUILayout.ObjectField("Base material (optional)", material, typeof(Material), false);
            tint = EditorGUILayout.ColorField("Color", tint);
            metallic = EditorGUILayout.Slider("Metallic", metallic, 0, 1);
            roughness = EditorGUILayout.Slider("Roughness", roughness, 0, 1);
            alpha = EditorGUILayout.Slider("Alpha / opacity", alpha, 0, 1);
            preview.Draw(GUILayoutUtility.GetRect(80, 220, GUILayout.ExpandWidth(true)), data.items[selected], material, tint, metallic, roughness, alpha);
            EditorGUILayout.LabelField("Drag to rotate · Scroll to zoom", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.HelpBox("Roughness 0 = glossy, 1 = matte. Alpha 0 = invisible, 1 = opaque. Color multiplies the source texture. Each material slot gets its own copy.", MessageType.None);
            using (new EditorGUI.DisabledScope(Application.isPlaying || string.IsNullOrWhiteSpace(selectedItemName)))
                if (GUILayout.Button("Save Changes To Selected Item", GUILayout.Height(30))) ApplyToSelected();
            EditorGUILayout.Space();
            variantName = EditorGUILayout.TextField("Duplicate name", variantName);
            using (new EditorGUI.DisabledScope(Application.isPlaying || string.IsNullOrWhiteSpace(variantName)))
                if (GUILayout.Button("Duplicate Item With These Settings", GUILayout.Height(28))) CreateVariant();
            EditorGUILayout.EndVertical();
        }

        PlayerCustomizationManager.CategoryData Data()
        {
            if (manager == null) return null;
            switch (category)
            {
                case PlayerCustomizationManager.Category.Head: return manager.headAccessories;
                case PlayerCustomizationManager.Category.Body: return manager.body;
                case PlayerCustomizationManager.Category.Bag: return manager.bags;
                default: return manager.skates;
            }
        }

        void LoadProperties()
        {
            var data = Data();
            if (data?.items == null || data.items.Length == 0) return;
            selected = Mathf.Clamp(selected, 0, data.items.Length - 1);
            selectedItemName = data.items[selected].itemName;
            loadedItemName = selectedItemName;
            variantName = data.items[selected].itemName + " Variant";
            preview.Dispose();
            material = null;
            tint = Color.white; metallic = 0; roughness = .7f; alpha = 1;
            foreach (var go in data.items[selected].objectsToEnable ?? Array.Empty<GameObject>())
            {
                if (go == null) continue;
                var renderer = go.GetComponentInChildren<Renderer>(true);
                if (renderer == null || renderer.sharedMaterial == null) continue;
                var m = renderer.sharedMaterial;
                tint = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.color : Color.white;
                alpha = tint.a;
                metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0;
                roughness = 1 - (m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : .3f);
                break;
            }
        }

        void CreateVariant()
        {
            var data = Data();
            var source = data.items[selected];
            var objects = source.objectsToEnable ?? Array.Empty<GameObject>();
            if (objects.Length == 0) { Debug.LogWarning("Source item has no model objects."); return; }
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create item variant");
            Undo.RecordObject(manager, "Add item variant");
            string id = category.ToString().ToLowerInvariant() + "." + Guid.NewGuid().ToString("N");
            var clones = new GameObject[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null) continue;
                clones[i] = UnityEngine.Object.Instantiate(objects[i], objects[i].transform.parent, false);
                clones[i].name = variantName.Trim() + "_" + id.Substring(id.Length - 8) + "_" + i;
                Undo.RegisterCreatedObjectUndo(clones[i], "Clone item model");
                clones[i].SetActive(false);
            }
            var item = new PlayerCustomizationManager.CustomizationItem { itemId = id, itemName = variantName.Trim(), objectsToEnable = clones, equippedByDefault = false };
            ApplyMaterials(item);
            var list = new List<PlayerCustomizationManager.CustomizationItem>(data.items) { item };
            data.items = list.ToArray();
            selected = data.items.Length - 1;
            Finish();
            LoadProperties();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Item duplicated and synced to multiplayer: " + item.itemName);
        }

        void ApplyToSelected()
        {
            Undo.RecordObject(manager, "Edit existing item");
            var item = Data().items[selected];
            item.itemName = selectedItemName.Trim();
            ApplyMaterials(item);
            Finish();
            LoadProperties();
        }

        void ApplyMaterials(PlayerCustomizationManager.CustomizationItem item)
        {
            string folder = "Assets/item system/item materials";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/item system", "item materials");
            var copies = new Dictionary<Material, Material>();
            foreach (var go in item.objectsToEnable ?? Array.Empty<GameObject>())
            {
                if (go == null) continue;
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var source = material != null ? material : slots[i];
                        if (source == null) continue;
                        if (!copies.TryGetValue(source, out var copy))
                        {
                            copy = new Material(source);
                            var color = tint; color.a = alpha;
                            if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", color);
                            if (copy.HasProperty("_Color")) copy.SetColor("_Color", color);
                            if (copy.HasProperty("_Metallic")) copy.SetFloat("_Metallic", metallic);
                            if (copy.HasProperty("_Smoothness")) copy.SetFloat("_Smoothness", 1 - roughness);
                            if (copy.HasProperty("_Glossiness")) copy.SetFloat("_Glossiness", 1 - roughness);
                            bool transparent = alpha < .999f;
                            if (copy.HasProperty("_Surface")) copy.SetFloat("_Surface", transparent ? 1 : 0);
                            if (copy.HasProperty("_Mode")) copy.SetFloat("_Mode", transparent ? 3 : 0);
                            copy.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
                            if (copy.HasProperty("_SrcBlend")) copy.SetFloat("_SrcBlend", transparent ? 5 : 1);
                            if (copy.HasProperty("_DstBlend")) copy.SetFloat("_DstBlend", transparent ? 10 : 0);
                            if (copy.HasProperty("_ZWrite")) copy.SetFloat("_ZWrite", transparent ? 0 : 1);
                            copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            if (transparent) { copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); copy.EnableKeyword("_ALPHABLEND_ON"); }
                            else { copy.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); copy.DisableKeyword("_ALPHABLEND_ON"); }
                            copy.renderQueue = transparent ? 3000 : 2000;
                            copy.name = item.itemName + " Material";
                            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(folder + "/Variant Material.mat"));
                            copies.Add(source, copy);
                        }
                        slots[i] = copy;
                    }
                    Undo.RecordObject(renderer, "Update item material");
                    renderer.sharedMaterials = slots;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }

        void Finish()
        {
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            AssetDatabase.SaveAssets();
            NetworkAppearanceAutoSync.Sync(manager, false);
            SceneView.RepaintAll();
        }
    }
}
