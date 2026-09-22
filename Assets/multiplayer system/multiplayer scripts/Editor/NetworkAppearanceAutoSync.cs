using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HitBoss.Multiplayer;

namespace HitBoss.Multiplayer.Editor
{
    [InitializeOnLoad]
    public static class NetworkAppearanceAutoSync
    {
        const string NetworkPlayerPath = "Assets/multiplayer system/multiplayer prefabs/Network player.prefab";
        static bool syncing;
        static bool queued;

        static NetworkAppearanceAutoSync()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        public static void Schedule(PlayerCustomizationManager source)
        {
            if (source == null || Application.isPlaying || queued) return;
            queued = true;
            EditorApplication.delayCall += () =>
            {
                queued = false;
                if (source != null) Sync(source, false);
            };
        }

        [MenuItem("Tools/Hit Boss/Sync Multiplayer Customization")]
        public static void SyncOpenCustomization()
        {
            var source = UnityEngine.Object.FindFirstObjectByType<PlayerCustomizationManager>(FindObjectsInactive.Include);
            if (source == null)
            {
                Debug.LogWarning("Open the main menu scene before syncing multiplayer customization.");
                return;
            }
            Sync(source, true);
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            var source = UnityEngine.Object.FindFirstObjectByType<PlayerCustomizationManager>(FindObjectsInactive.Include);
            if (source != null) Sync(source, false);
        }

        static void OnSceneSaving(Scene scene, string path)
        {
            if (syncing || Application.isPlaying) return;
            foreach (var source in UnityEngine.Object.FindObjectsByType<PlayerCustomizationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (source.gameObject.scene == scene) { Sync(source, false); break; }
        }

        public static bool Sync(PlayerCustomizationManager source, bool logResult)
        {
            if (syncing || source == null || Application.isPlaying) return false;
            if (EnsureItemIds(source))
            {
                EditorUtility.SetDirty(source);
                if (source.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
            }
            var sourceRoot = FindPlayerRoot(source);
            if (sourceRoot == null || AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayerPath) == null) return false;

            syncing = true;
            GameObject targetRoot = null;
            try
            {
                targetRoot = PrefabUtility.LoadPrefabContents(NetworkPlayerPath);
                var target = targetRoot.GetComponentInChildren<PlayerAppearance>(true);
                if (target == null) return false;

                var oldObjects = CollectObjects(target);
                bool addedObject = false;
                target.head = SyncCategory(source.headAccessories, sourceRoot, targetRoot.transform, ref addedObject);
                target.body = SyncCategory(source.body, sourceRoot, targetRoot.transform, ref addedObject);
                target.bag = SyncCategory(source.bags, sourceRoot, targetRoot.transform, ref addedObject);
                target.skates = SyncCategory(source.skates, sourceRoot, targetRoot.transform, ref addedObject);

                var currentObjects = CollectObjects(target);
                foreach (var oldObject in oldObjects)
                    if (oldObject != null && !currentObjects.Contains(oldObject)) oldObject.SetActive(false);

                EditorUtility.SetDirty(target);
                PrefabUtility.SaveAsPrefabAsset(targetRoot, NetworkPlayerPath);
                if (logResult || addedObject)
                    Debug.Log("Multiplayer customization synced automatically: " +
                        target.head.items.Length + " head, " + target.body.items.Length + " body, " +
                        target.bag.items.Length + " bag, " + target.skates.items.Length + " skates.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
            finally
            {
                if (targetRoot != null) PrefabUtility.UnloadPrefabContents(targetRoot);
                syncing = false;
            }
        }

        static bool EnsureItemIds(PlayerCustomizationManager source)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            bool changed = false;
            EnsureCategoryIds(source.headAccessories, "head", used, ref changed);
            EnsureCategoryIds(source.body, "body", used, ref changed);
            EnsureCategoryIds(source.bags, "bag", used, ref changed);
            EnsureCategoryIds(source.skates, "skates", used, ref changed);
            return changed;
        }

        static void EnsureCategoryIds(PlayerCustomizationManager.CategoryData category, string prefix, HashSet<string> used, ref bool changed)
        {
            if (category?.items == null) return;
            for (int i = 0; i < category.items.Length; i++)
            {
                var item = category.items[i];
                if (item == null) continue;
                string id = item.itemId?.Trim();
                if (!string.IsNullOrEmpty(id) && used.Add(id)) continue;

                string slug = Slug(item.itemName);
                if (string.IsNullOrEmpty(slug)) slug = "item-" + (i + 1);
                string baseId = prefix + "." + slug;
                id = baseId;
                int suffix = 2;
                while (!used.Add(id)) id = baseId + "-" + suffix++;
                item.itemId = id;
                changed = true;
            }
        }

        static string Slug(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var chars = new List<char>();
            bool separator = false;
            foreach (char character in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    chars.Add(character);
                    separator = false;
                }
                else if (!separator && chars.Count > 0)
                {
                    chars.Add('-');
                    separator = true;
                }
            }
            while (chars.Count > 0 && chars[chars.Count - 1] == '-') chars.RemoveAt(chars.Count - 1);
            return new string(chars.ToArray());
        }

        static Transform FindPlayerRoot(PlayerCustomizationManager source)
        {
            foreach (var menu in UnityEngine.Object.FindObjectsByType<MainMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (menu.customizationManager == source && menu.playerSetup != null) return menu.playerSetup.transform;

            foreach (var category in Categories(source))
                if (category?.items != null)
                    foreach (var item in category.items)
                        if (item?.objectsToEnable != null)
                            foreach (var itemObject in item.objectsToEnable)
                                if (itemObject != null) return itemObject.transform.root;
            return null;
        }

        static IEnumerable<PlayerCustomizationManager.CategoryData> Categories(PlayerCustomizationManager source)
        {
            yield return source.headAccessories;
            yield return source.body;
            yield return source.bags;
            yield return source.skates;
        }

        static HashSet<GameObject> CollectObjects(PlayerAppearance appearance)
        {
            var objects = new HashSet<GameObject>();
            AddObjects(appearance.head, objects);
            AddObjects(appearance.body, objects);
            AddObjects(appearance.bag, objects);
            AddObjects(appearance.skates, objects);
            return objects;
        }

        static void AddObjects(PlayerCustomizationManager.CategoryData category, HashSet<GameObject> objects)
        {
            if (category?.items == null) return;
            foreach (var item in category.items)
                if (item?.objectsToEnable != null)
                    foreach (var itemObject in item.objectsToEnable)
                        if (itemObject != null) objects.Add(itemObject);
        }

        static PlayerCustomizationManager.CategoryData SyncCategory(
            PlayerCustomizationManager.CategoryData source, Transform sourceRoot, Transform targetRoot, ref bool addedObject)
        {
            var output = new PlayerCustomizationManager.CategoryData
            {
                allowNone = source != null && source.allowNone,
                items = new PlayerCustomizationManager.CustomizationItem[source?.items?.Length ?? 0]
            };
            for (int i = 0; i < output.items.Length; i++)
            {
                var sourceItem = source.items[i];
                var targetObjects = new GameObject[sourceItem?.objectsToEnable?.Length ?? 0];
                for (int j = 0; j < targetObjects.Length; j++)
                    targetObjects[j] = MapOrClone(sourceItem.objectsToEnable[j], sourceRoot, targetRoot, ref addedObject);
                output.items[i] = new PlayerCustomizationManager.CustomizationItem
                {
                    itemId = sourceItem?.itemId ?? "",
                    itemName = sourceItem?.itemName ?? "",
                    icon = sourceItem?.icon,
                    equippedByDefault = sourceItem?.equippedByDefault == true,
                    objectsToEnable = targetObjects
                };
            }
            return output;
        }

        static GameObject MapOrClone(GameObject sourceObject, Transform sourceRoot, Transform targetRoot, ref bool addedObject)
        {
            if (sourceObject == null) return null;
            string path = AnimationUtility.CalculateTransformPath(sourceObject.transform, sourceRoot);
            var existing = targetRoot.Find(path);
            if (existing != null) return existing.gameObject;

            string parentPath = AnimationUtility.CalculateTransformPath(sourceObject.transform.parent, sourceRoot);
            var parent = string.IsNullOrEmpty(parentPath) ? targetRoot : targetRoot.Find(parentPath);
            if (parent == null)
            {
                Debug.LogWarning("Could not sync multiplayer item because its matching parent is missing: " + path);
                return null;
            }

            var clone = UnityEngine.Object.Instantiate(sourceObject, parent, false);
            clone.name = sourceObject.name;
            RemapSkinnedMeshes(sourceObject, clone, sourceRoot, targetRoot);
            RemapObjectReferences(clone, sourceRoot, targetRoot);
            addedObject = true;
            return clone;
        }

        static void RemapSkinnedMeshes(GameObject sourceObject, GameObject clone, Transform sourceRoot, Transform targetRoot)
        {
            foreach (var sourceRenderer in sourceObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string rendererPath = AnimationUtility.CalculateTransformPath(sourceRenderer.transform, sourceObject.transform);
                var cloneTransform = string.IsNullOrEmpty(rendererPath) ? clone.transform : clone.transform.Find(rendererPath);
                var targetRenderer = cloneTransform != null ? cloneTransform.GetComponent<SkinnedMeshRenderer>() : null;
                if (targetRenderer == null) continue;

                targetRenderer.rootBone = MapTransform(sourceRenderer.rootBone, sourceRoot, targetRoot);
                var sourceBones = sourceRenderer.bones;
                var targetBones = new Transform[sourceBones.Length];
                for (int i = 0; i < sourceBones.Length; i++)
                    targetBones[i] = MapTransform(sourceBones[i], sourceRoot, targetRoot);
                targetRenderer.bones = targetBones;
            }
        }

        static Transform MapTransform(Transform source, Transform sourceRoot, Transform targetRoot)
        {
            if (source == null) return null;
            string path = AnimationUtility.CalculateTransformPath(source, sourceRoot);
            return string.IsNullOrEmpty(path) ? targetRoot : targetRoot.Find(path);
        }

        static void RemapObjectReferences(GameObject clone, Transform sourceRoot, Transform targetRoot)
        {
            foreach (var component in clone.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var mapped = MapReference(property.objectReferenceValue, sourceRoot, targetRoot);
                    if (mapped != null && mapped != property.objectReferenceValue) property.objectReferenceValue = mapped;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static UnityEngine.Object MapReference(UnityEngine.Object value, Transform sourceRoot, Transform targetRoot)
        {
            Transform sourceTransform = value is GameObject go ? go.transform : value is Component component ? component.transform : null;
            if (sourceTransform == null || !sourceTransform.IsChildOf(sourceRoot)) return null;
            string path = AnimationUtility.CalculateTransformPath(sourceTransform, sourceRoot);
            var targetTransform = string.IsNullOrEmpty(path) ? targetRoot : targetRoot.Find(path);
            if (targetTransform == null) return null;
            if (value is GameObject) return targetTransform.gameObject;
            if (value is Transform) return targetTransform;
            return targetTransform.GetComponent(value.GetType());
        }
    }

    [CustomEditor(typeof(PlayerCustomizationManager))]
    public sealed class PlayerCustomizationManagerEditor : UnityEditor.Editor
    {
        void OnEnable() => NetworkAppearanceAutoSync.Schedule((PlayerCustomizationManager)target);

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) NetworkAppearanceAutoSync.Schedule((PlayerCustomizationManager)target);
        }
    }
}
