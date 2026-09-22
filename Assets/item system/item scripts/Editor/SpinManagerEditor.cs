using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HitBoss.Items.Editor
{
    [CustomEditor(typeof(SpinManager))]
    public sealed class SpinManagerEditor : UnityEditor.Editor
    {
        SerializedProperty customization, titleText, resultText, wheelText, oddsText, wheel;
        SerializedProperty rewardLabelPrefab, spinButton, spinButtonText, animationSeconds, spins;
        SerializedProperty dailySpinIndex, freeGemSpinIndex;

        void OnEnable()
        {
            customization = serializedObject.FindProperty("customization");
            titleText = serializedObject.FindProperty("titleText");
            resultText = serializedObject.FindProperty("resultText");
            wheelText = serializedObject.FindProperty("wheelText");
            oddsText = serializedObject.FindProperty("oddsText");
            wheel = serializedObject.FindProperty("wheel");
            rewardLabelPrefab = serializedObject.FindProperty("rewardLabelPrefab");
            spinButton = serializedObject.FindProperty("spinButton");
            spinButtonText = serializedObject.FindProperty("spinButtonText");
            animationSeconds = serializedObject.FindProperty("animationSeconds");
            spins = serializedObject.FindProperty("spins");
            dailySpinIndex = serializedObject.FindProperty("dailySpinIndex");
            freeGemSpinIndex = serializedObject.FindProperty("freeGemSpinIndex");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(customization);
            EditorGUILayout.PropertyField(titleText);
            EditorGUILayout.PropertyField(resultText);
            EditorGUILayout.PropertyField(wheelText);
            EditorGUILayout.PropertyField(oddsText);
            EditorGUILayout.PropertyField(wheel);
            EditorGUILayout.PropertyField(rewardLabelPrefab);
            EditorGUILayout.PropertyField(spinButton);
            EditorGUILayout.PropertyField(spinButtonText);
            EditorGUILayout.PropertyField(animationSeconds);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Main Menu Spin Buttons", EditorStyles.boldLabel);
            string[] spinNames = new string[spins.arraySize + 1];
            spinNames[0] = "Not assigned";
            for (int i = 0; i < spins.arraySize; i++)
            {
                string configuredName = spins.GetArrayElementAtIndex(i).FindPropertyRelative("spinName").stringValue;
                spinNames[i + 1] = string.IsNullOrWhiteSpace(configuredName) ? "Spin " + (i + 1) : configuredName;
            }
            dailySpinIndex.intValue = EditorGUILayout.Popup("Daily Spin Button", Mathf.Clamp(dailySpinIndex.intValue + 1, 0, spinNames.Length - 1), spinNames) - 1;
            freeGemSpinIndex.intValue = EditorGUILayout.Popup("Free Gem Spin Button", Mathf.Clamp(freeGemSpinIndex.intValue + 1, 0, spinNames.Length - 1), spinNames) - 1;
            EditorGUILayout.HelpBox("These dropdowns choose which configured wheel opens from the two main-menu buttons.", MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Spin Types", EditorStyles.boldLabel);

            var manager = (SpinManager)target;
            Catalog(manager.customization, out List<string> labels, out List<string> ids);
            for (int i = 0; i < spins.arraySize; i++)
            {
                var spin = spins.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.PropertyField(spin.FindPropertyRelative("spinName"));
                EditorGUILayout.PropertyField(spin.FindPropertyRelative("currency"));
                EditorGUILayout.PropertyField(spin.FindPropertyRelative("cost"));
                EditorGUILayout.PropertyField(spin.FindPropertyRelative("available"));
                EditorGUILayout.LabelField("Rewards", EditorStyles.boldLabel);
                var rewards = spin.FindPropertyRelative("rewards");
                float total = 0;
                for (int r = 0; r < rewards.arraySize; r++)
                    total += Mathf.Max(0, rewards.GetArrayElementAtIndex(r).FindPropertyRelative("probabilityWeight").floatValue);

                for (int r = 0; r < rewards.arraySize; r++)
                {
                    var reward = rewards.GetArrayElementAtIndex(r);
                    EditorGUILayout.BeginVertical("helpbox");
                    var type = reward.FindPropertyRelative("rewardType");
                    EditorGUILayout.PropertyField(type);
                    if ((SpinRewardType)type.enumValueIndex == SpinRewardType.Item)
                    {
                        var id = reward.FindPropertyRelative("itemId");
                        int selected = Mathf.Max(0, ids.IndexOf(id.stringValue));
                        selected = EditorGUILayout.Popup("Item", selected, labels.ToArray());
                        id.stringValue = ids[selected];
                        var duplicateCoins = reward.FindPropertyRelative("duplicateCoins");
                        duplicateCoins.intValue = Mathf.Max(1, EditorGUILayout.IntField("Owned Item Compensation", duplicateCoins.intValue));
                    }
                    else EditorGUILayout.PropertyField(reward.FindPropertyRelative("amount"), new GUIContent("Reward Amount"));

                    var weight = reward.FindPropertyRelative("probabilityWeight");
                    weight.floatValue = Mathf.Max(.01f, EditorGUILayout.FloatField("Probability Weight", weight.floatValue));
                    float chance = total > 0 ? weight.floatValue / total * 100f : 0;
                    EditorGUILayout.LabelField("Configured Chance", chance.ToString("0.#") + "%");
                    if (GUILayout.Button("Remove reward")) { rewards.DeleteArrayElementAtIndex(r); EditorGUILayout.EndVertical(); break; }
                    EditorGUILayout.EndVertical();
                }
                if (GUILayout.Button("Add reward")) rewards.InsertArrayElementAtIndex(rewards.arraySize);
                if (GUILayout.Button("Remove spin")) { spins.DeleteArrayElementAtIndex(i); EditorGUILayout.EndVertical(); break; }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("Add spin type")) spins.InsertArrayElementAtIndex(spins.arraySize);
            if (ids.Count == 1) EditorGUILayout.HelpBox("Assign the Customization Manager to auto-detect every item.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
        }

        static void Catalog(PlayerCustomizationManager manager, out List<string> labels, out List<string> ids)
        {
            labels = new List<string> { "Choose item..." };
            ids = new List<string> { "" };
            if (manager == null) return;
            Add(PlayerCustomizationManager.Category.Head, manager.headAccessories, labels, ids);
            Add(PlayerCustomizationManager.Category.Body, manager.body, labels, ids);
            Add(PlayerCustomizationManager.Category.Bag, manager.bags, labels, ids);
            Add(PlayerCustomizationManager.Category.Skates, manager.skates, labels, ids);
        }

        static void Add(PlayerCustomizationManager.Category category, PlayerCustomizationManager.CategoryData data, List<string> labels, List<string> ids)
        {
            if (data?.items == null) return;
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

