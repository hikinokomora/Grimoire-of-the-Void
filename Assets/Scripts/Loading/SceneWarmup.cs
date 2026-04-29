using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GrimoireOfTheVoid.Loading
{
    /// <summary>
    /// Lightweight warmup to reduce first-frame hitching after scene activation.
    /// Runs after the next scene load event.
    /// </summary>
    public static class SceneWarmup
    {
        private sealed class Runner : MonoBehaviour { }

        private static Runner _runner;

        private static Runner GetRunner()
        {
            if (_runner != null) return _runner;
            var go = new GameObject(nameof(SceneWarmup));
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
            return _runner;
        }

        /// <summary>
        /// Waits for next scene activation, then warms up renderers/materials over a few frames.
        /// </summary>
        public static IEnumerator WarmupAfterNextSceneLoad(int frameDelay = 2, float timeSliceMs = 6f)
        {
            // Wait a few frames so renderers/materials are registered.
            for (int i = 0; i < Mathf.Max(0, frameDelay); i++)
            {
                yield return null;
            }

            // Warm up shaders if Unity has a built-in collection.
            Shader.WarmupAllShaders();

            // Enumerate renderers and touch materials/textures in a timesliced loop.
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var mats = new List<Material>(1024);

            float start = Time.realtimeSinceStartup;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                mats.Clear();
                r.GetSharedMaterials(mats);
                for (int m = 0; m < mats.Count; m++)
                {
                    var mat = mats[m];
                    if (mat == null) continue;

                    // Touch commonly-used textures to force dependency load.
                    var tex = mat.mainTexture;
                    if (tex != null)
                    {
                        _ = tex.width;
                        _ = tex.height;
                    }
                }

                // Timeslice (real time), so HDD doesn't create one big spike.
                if ((Time.realtimeSinceStartup - start) * 1000f >= timeSliceMs)
                {
                    start = Time.realtimeSinceStartup;
                    yield return null;
                }
            }
        }
    }
}

