using System.Collections.Generic;
using UnityEngine;

namespace NeonBreach.Farm
{
    /// <summary>
    /// A planted field. Feeding rats lower its health; the crop renderers (registered by the field's
    /// VegetationPlanters) are tinted towards a withered colour as it drops, using a MaterialPropertyBlock so the
    /// shared material assets are never modified.
    /// </summary>
    public class CropField : MonoBehaviour
    {
        public string cropName = "Crops";
        [Tooltip("Field rectangle size in metres (X, Z) around this transform.")]
        public Vector2 size = new Vector2(20f, 20f);
        [Tooltip("Tall crops (corn) hide rats.")]
        public bool tallCrop;
        [Range(0f, 1f)] public float health = 1f;
        [Tooltip("How much this field counts towards the farm's total crop health.")]
        [Min(0f)] public float weight = 1f;
        public Color witheredTint = new Color(0.55f, 0.45f, 0.30f, 1f);

        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<Color> baseColors = new List<Color>();
        MaterialPropertyBlock block;
        float shownHealth = -1f;
        static readonly int idColor = Shader.PropertyToID("_Color");

        public Vector3 RandomPoint(float margin = 1.5f)
        {
            float x = Random.Range(-size.x * 0.5f + margin, size.x * 0.5f - margin);
            float z = Random.Range(-size.y * 0.5f + margin, size.y * 0.5f - margin);
            Vector3 p = transform.position + new Vector3(x, 0f, z);
            p.y = FarmLayout.Height(p.x, p.z);
            return p;
        }

        public bool Contains(Vector3 p, float margin = 0f)
        {
            Vector3 d = p - transform.position;
            return Mathf.Abs(d.x) <= size.x * 0.5f + margin && Mathf.Abs(d.z) <= size.y * 0.5f + margin;
        }

        public void Damage(float amount) { health = Mathf.Clamp01(health - amount); }

        public void ResetHealth() { health = 1f; UpdateTint(true); }

        public void RegisterRenderers(IList<Renderer> rs)
        {
            for (int i = 0; i < rs.Count; i++)
            {
                if (rs[i] == null) continue;
                renderers.Add(rs[i]);
                baseColors.Add(rs[i].sharedMaterial != null ? rs[i].sharedMaterial.color : Color.white);
            }
            shownHealth = -1f;
        }

        public void UnregisterRenderers(IList<Renderer> rs)
        {
            for (int i = 0; i < rs.Count; i++)
            {
                int k = renderers.IndexOf(rs[i]);
                if (k >= 0) { renderers.RemoveAt(k); baseColors.RemoveAt(k); }
            }
        }

        void Update() { if (Application.isPlaying) UpdateTint(false); }

        void UpdateTint(bool force)
        {
            if (!force && Mathf.Abs(shownHealth - health) < 0.01f) return;
            if (block == null) block = new MaterialPropertyBlock();
            shownHealth = health;
            float k = 1f - health;
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(idColor, Color.Lerp(baseColors[i], witheredTint, k * 0.85f));
                r.SetPropertyBlock(block);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(size.x, 1f, size.y));
        }
    }
}
