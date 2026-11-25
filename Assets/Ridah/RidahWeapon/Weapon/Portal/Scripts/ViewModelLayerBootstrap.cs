using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tags first-person weapon renderers with the ViewModel layer at runtime.
/// Particle systems and projectile/VFX clones are intentionally left on their
/// original layers so their collision filters and decals keep working.
/// </summary>
public static class ViewModelLayerBootstrap
{
    public const string LayerName = "ViewModel";
    public const int FallbackLayerIndex = 14;

    private static bool registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        if (registered)
            return;
        registered = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyAfterSceneLoad()
    {
        ApplyNow();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyNow();
    }

    public static int ApplyNow()
    {
        int layer = LayerMask.NameToLayer(LayerName);
        if (layer < 0)
            layer = FallbackLayerIndex;

        int changed = 0;
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include);
        for (int i = 0; i < transforms.Length; ++i)
        {
            Transform candidate = transforms[i];
            if (candidate == null ||
                candidate.name != "Weapons" ||
                !candidate.gameObject.scene.IsValid() ||
                !HasAncestorNamed(candidate.parent, "CameraHolder"))
                continue;

            Renderer[] renderers =
                candidate.GetComponentsInChildren<Renderer>(true);
            for (int j = 0; j < renderers.Length; ++j)
            {
                Renderer renderer = renderers[j];
                if (renderer == null)
                    continue;

                GameObject rendererObject = renderer.gameObject;
                if (rendererObject.GetComponent<ParticleSystem>() != null ||
                    IsProjectileOrVfx(renderer.transform))
                    continue;

                if (rendererObject.layer == layer)
                    continue;

                rendererObject.layer = layer;
                changed++;
            }
        }
        return changed;
    }

    private static bool HasAncestorNamed(Transform parent, string name)
    {
        while (parent != null)
        {
            if (parent.name == name)
                return true;
            parent = parent.parent;
        }
        return false;
    }

    private static bool IsProjectileOrVfx(Transform transform)
    {
        while (transform != null)
        {
            string name = transform.name;
            if (name.IndexOf("(Clone)", StringComparison.Ordinal) >= 0 ||
                name.StartsWith("LaserPrefab", StringComparison.Ordinal) ||
                name.StartsWith("crossvfx", StringComparison.Ordinal) ||
                name.StartsWith("Fire ", StringComparison.Ordinal) ||
                name == "Bullet" ||
                name == "MagnetWave")
                return true;
            transform = transform.parent;
        }
        return false;
    }
}
