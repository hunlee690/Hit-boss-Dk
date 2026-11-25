using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DoubleSidedMaterialRuntime
{
    static readonly Dictionary<Material, Material> Copies = new Dictionary<Material, Material>();
    static readonly string[] CullProperties =
    {
        "_Cull", "_CullMode", "_CullModeForward",
        "_TransparentCullMode", "_RenderFace"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyFirstScene()
    {
        ApplyToAllRenderers();
    }

    static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToAllRenderers();
    }

    public static void ApplyToAllRenderers()
    {
        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                Material replacement = DoubleSided(materials[i]);
                if (replacement == materials[i]) continue;
                materials[i] = replacement;
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = materials;
        }
    }

    static Material DoubleSided(Material source)
    {
        if (source == null) return null;
        if (Copies.TryGetValue(source, out Material existing)) return existing;

        var copy = new Material(source)
        {
            name = source.name + " (Double Sided)",
            hideFlags = HideFlags.DontSave
        };

        foreach (string property in CullProperties)
            if (copy.HasProperty(property))
                copy.SetFloat(property, 0f);

        if (copy.HasProperty("_DoubleSidedEnable"))
        {
            copy.SetFloat("_DoubleSidedEnable", 1f);
            copy.EnableKeyword("_DOUBLESIDED_ON");
        }

        copy.doubleSidedGI = true;
        Copies[source] = copy;
        return copy;
    }
}
