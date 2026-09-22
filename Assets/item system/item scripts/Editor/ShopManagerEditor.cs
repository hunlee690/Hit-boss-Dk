using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HitBoss.Items.Editor
{
    [CustomEditor(typeof(ShopManager))]
    public sealed class ShopManagerEditor : UnityEditor.Editor
    {
        SerializedProperty customization, content, rowPrefab, spinCardPrefab, spinManager, mainMenu, statusText, offers;
        void OnEnable()
        {
            customization = serializedObject.FindProperty("customization");
            content = serializedObject.FindProperty("content");
            rowPrefab = serializedObject.FindProperty("rowPrefab");
            spinCardPrefab = serializedObject.FindProperty("spinCardPrefab");
            spinManager = serializedObject.FindProperty("spinManager");
            mainMenu = serializedObject.FindProperty("mainMenu");
            statusText = serializedObject.FindProperty("statusText");
            offers = serializedObject.FindProperty("offers");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var manager = (ShopManager)target;
            if (manager.SyncOffersFromCustomization())
            {
                EditorUtility.SetDirty(manager);
                serializedObject.Update();
            }
            EditorGUILayout.PropertyField(customization);
            EditorGUILayout.PropertyField(content);
            EditorGUILayout.PropertyField(rowPrefab);
            EditorGUILayout.PropertyField(spinCardPrefab);
            EditorGUILayout.PropertyField(spinManager);
            EditorGUILayout.PropertyField(mainMenu);
            EditorGUILayout.PropertyField(statusText);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Items For Sale", EditorStyles.boldLabel);

            var labels = new List<string> { "Choose item..." };
            var ids = new List<string> { "" };
            Add(manager.customization, PlayerCustomizationManager.Category.Head, manager.customization?.headAccessories, labels, ids);
            Add(manager.customization, PlayerCustomizationManager.Category.Body, manager.customization?.body, labels, ids);
            Add(manager.customization, PlayerCustomizationManager.Category.Bag, manager.customization?.bags, labels, ids);
            Add(manager.customization, PlayerCustomizationManager.Category.Skates, manager.customization?.skates, labels, ids);

            for (int i = 0; i < offers.arraySize; i++)
            {
                var offer = offers.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical("box");
                var itemId = offer.FindPropertyRelative("itemId");
                int selected = Mathf.Max(0, ids.IndexOf(itemId.stringValue));
                selected = EditorGUILayout.Popup("Item", selected, labels.ToArray());
                itemId.stringValue = ids[selected];
                EditorGUILayout.PropertyField(offer.FindPropertyRelative("currency"));
                EditorGUILayout.PropertyField(offer.FindPropertyRelative("price"));
                EditorGUILayout.PropertyField(offer.FindPropertyRelative("available"));
                if (GUILayout.Button("Remove offer")) { offers.DeleteArrayElementAtIndex(i); break; }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("Add item offer")) offers.InsertArrayElementAtIndex(offers.arraySize);
            if (ids.Count == 1) EditorGUILayout.HelpBox("Assign the Customization Manager to auto-detect its items.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
        }

        static void Add(PlayerCustomizationManager manager, PlayerCustomizationManager.Category category,
            PlayerCustomizationManager.CategoryData data, List<string> labels, List<string> ids)
        {
            if (manager == null || data?.items == null) return;
            for (int i = 0; i < data.items.Length; i++)
            {
                var item = data.items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.itemId)) continue;
                labels.Add(category + " / " + (string.IsNullOrWhiteSpace(item.itemName) ? "Item " + (i + 1) : item.itemName));
                ids.Add(item.itemId);
            }
        }
    }
}
