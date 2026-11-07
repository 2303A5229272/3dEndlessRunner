using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DessertRunner
{
    /// <summary>Materials + primitive-based 3D models (everything is generated from code).</summary>
    public static class Builders
    {
        static Shader shader;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static Shader GetShader()
        {
            if (shader != null) return shader;
            if (GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.defaultMaterial != null)
                shader = GraphicsSettings.currentRenderPipeline.defaultMaterial.shader;
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return shader;
        }

        public static Material Mat(Color c, float smooth = 0.15f, bool emissive = false)
        {
            string key = c.ToString() + smooth + emissive;
            Material m;
            if (cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(GetShader());
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 0.7f);
            }
            cache[key] = m;
            return m;
        }

        public static Material SandMat(Color c, float tu = 6f, float tv = 6f)
        {
            var m = new Material(GetShader());
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.02f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.02f);
            var tex = NoiseTex();
            if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", new Vector2(tu, tv)); }
            if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", new Vector2(tu, tv)); }
            return m;
        }

        static Texture2D noise;
        static Texture2D NoiseTex()
        {
            if (noise != null) return noise;
            int n = 128;
            noise = new Texture2D(n, n, TextureFormat.RGB24, true);
            noise.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float a = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                    float b = Mathf.PerlinNoise(x * 0.25f + 40f, y * 0.25f + 40f);
                    float ripple = 0.5f + 0.5f * Mathf.Sin((x + a * 30f) * 0.35f);
                    float v = 0.80f + 0.12f * a + 0.06f * b + 0.04f * ripple;
                    noise.SetPixel(x, y, new Color(v, v, v));
                }
            noise.Apply();
            return noise;
        }

        public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default(Vector3))
        {
            var g = GameObject.CreatePrimitive(t);
            var col = g.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localEulerAngles = euler;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static GameObject Empty(string n, Transform parent, Vector3 pos)
        {
            var g = new GameObject(n);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            return g;
        }

        // ---------- Player (same character in every scene: teal tee, brown shorts, red headband) ----------
        public class PlayerRig { public Transform root, model, legL, legR, armL, armR; }

        public static PlayerRig BuildPlayer()
        {
            var rig = new PlayerRig();
            var root = new GameObject("Player");
            rig.root = root.transform;
            var model = Empty("Model", root.transform, Vector3.zero);
            rig.model = model.transform;
            Material skin = Mat(new Color(0.84f, 0.61f, 0.44f));
            Material tee = Mat(new Color(0.12f, 0.63f, 0.67f));
            Material shorts = Mat(new Color(0.43f, 0.28f, 0.17f));
            Material hair = Mat(new Color(0.15f, 0.1f, 0.08f));
            Material band = Mat(new Color(0.8f, 0.15f, 0.15f));
            Material shoe = Mat(new Color(0.95f, 0.95f, 0.95f));

            Prim(PrimitiveType.Cube, model.transform, new Vector3(0, 1.15f, 0), new Vector3(0.62f, 0.7f, 0.34f), tee);
            Prim(PrimitiveType.Cube, model.transform, new Vector3(0, 0.78f, 0), new Vector3(0.58f, 0.22f, 0.34f), shorts);
            var head = Prim(PrimitiveType.Sphere, model.transform, new Vector3(0, 1.74f, 0), new Vector3(0.42f, 0.46f, 0.42f), skin);
            Prim(PrimitiveType.Sphere, model.transform, new Vector3(0, 1.8f, -0.03f), new Vector3(0.45f, 0.42f, 0.44f), hair);
            Prim(PrimitiveType.Cube, model.transform, new Vector3(0, 1.76f, -0.01f), new Vector3(0.47f, 0.08f, 0.45f), band);

            var lL = Empty("LegL", model.transform, new Vector3(-0.16f, 0.72f, 0));
            var lR = Empty("LegR", model.transform, new Vector3(0.16f, 0.72f, 0));
            foreach (var leg in new[] { lL, lR })
            {
                Prim(PrimitiveType.Cube, leg.transform, new Vector3(0, -0.28f, 0), new Vector3(0.22f, 0.5f, 0.24f), skin);
                Prim(PrimitiveType.Cube, leg.transform, new Vector3(0, -0.62f, 0.04f), new Vector3(0.24f, 0.14f, 0.34f), shoe);
            }
            var aL = Empty("ArmL", model.transform, new Vector3(-0.42f, 1.42f, 0));
            var aR = Empty("ArmR", model.transform, new Vector3(0.42f, 1.42f, 0));
            foreach (var arm in new[] { aL, aR })
            {
                Prim(PrimitiveType.Cube, arm.transform, new Vector3(0, -0.28f, 0), new Vector3(0.16f, 0.56f, 0.16f), skin);
                Prim(PrimitiveType.Cube, arm.transform, new Vector3(0, -0.02f, 0), new Vector3(0.2f, 0.18f, 0.2f), tee);
            }
            rig.legL = lL.transform; rig.legR = lR.transform; rig.armL = aL.transform; rig.armR = aR.transform;
            return rig;
        }

        // ---------- Obstacles ----------
        static readonly Color rockC = new Color(0.52f, 0.4f, 0.3f);
        static readonly Color woodC = new Color(0.45f, 0.3f, 0.18f);

        public static GameObject BuildObstacle(string key, out float halfDepth, out float height, out bool gate)
        {
            var g = new GameObject(key);
            gate = false; halfDepth = 0.7f; height = 1f;
            switch (key)
            {
                case "Rock":
                    Prim(PrimitiveType.Sphere, g.transform, new Vector3(0, 0.45f, 0), new Vector3(1.6f, 1.0f, 1.3f), Mat(rockC), new Vector3(0, 25, 0));
                    Prim(PrimitiveType.Sphere, g.transform, new Vector3(0.5f, 0.25f, 0.3f), new Vector3(0.8f, 0.55f, 0.7f), Mat(rockC * 0.85f));
                    halfDepth = 0.7f; height = 0.95f; break;
                case "BigRock":
                    Prim(PrimitiveType.Sphere, g.transform, new Vector3(0, 0.65f, 0), new Vector3(2.2f, 1.5f, 1.8f), Mat(rockC * 0.9f), new Vector3(0, 40, 0));
                    Prim(PrimitiveType.Sphere, g.transform, new Vector3(-0.6f, 0.3f, 0.4f), new Vector3(1.0f, 0.7f, 0.9f), Mat(rockC));
                    halfDepth = 0.95f; height = 1.3f; break;
                case "Log":
                    Prim(PrimitiveType.Cylinder, g.transform, new Vector3(0, 0.4f, 0), new Vector3(0.8f, 1.4f, 0.8f), Mat(woodC), new Vector3(0, 0, 90));
                    Prim(PrimitiveType.Cylinder, g.transform, new Vector3(1.4f, 0.4f, 0), new Vector3(0.62f, 0.02f, 0.62f), Mat(new Color(0.75f, 0.58f, 0.38f)), new Vector3(0, 0, 90));
                    halfDepth = 0.5f; height = 0.75f; break;
                case "Block":
                    Prim(PrimitiveType.Cube, g.transform, new Vector3(0, 0.6f, 0), new Vector3(1.9f, 1.2f, 1.4f), Mat(new Color(0.65f, 0.6f, 0.52f)));
                    Prim(PrimitiveType.Cube, g.transform, new Vector3(0, 1.22f, 0), new Vector3(1.95f, 0.06f, 1.45f), Mat(new Color(0.78f, 0.74f, 0.66f)));
                    halfDepth = 0.75f; height = 1.2f; break;
                case "Cactus":
                    var cg = Mat(new Color(0.27f, 0.5f, 0.27f));
                    Prim(PrimitiveType.Cylinder, g.transform, new Vector3(0, 0.9f, 0), new Vector3(0.5f, 0.9f, 0.5f), cg);
                    Prim(PrimitiveType.Cylinder, g.transform, new Vector3(-0.45f, 1.0f, 0), new Vector3(0.25f, 0.35f, 0.25f), cg);
                    Prim(PrimitiveType.Cylinder, g.transform, new Vector3(0.45f, 1.2f, 0), new Vector3(0.25f, 0.35f, 0.25f), cg);
                    halfDepth = 0.5f; height = 1.7f; break;
                case "Mound":
                    Prim(PrimitiveType.Sphere, g.transform, new Vector3(0, 0, 0), new Vector3(2.4f, 1.5f, 2.0f), Mat(new Color(0.82f, 0.66f, 0.4f), 0.05f));
                    halfDepth = 0.9f; height = 0.7f; break;
                case "Barrier":
                    var w = Mat(new Color(0.7f, 0.5f, 0.3f)); var wd = Mat(woodC);
                    Prim(PrimitiveType.Cube, g.transform, new Vector3(-1.2f, 0.45f, 0), new Vector3(0.16f, 0.9f, 0.16f), wd);
                    Prim(PrimitiveType.Cube, g.transform, new Vector3(1.2f, 0.45f, 0), new Vector3(0.16f, 0.9f, 0.16f), wd);
                    Prim(PrimitiveType.Cube, g.transform, new Vector3(0, 0.3f, 0), new Vector3(2.7f, 0.22f, 0.14f), w);
                    Prim(PrimitiveType.Cube, g.transform, new Vector3(0, 0.65f, 0), new Vector3(2.7f, 0.22f, 0.14f), w);
                    halfDepth = 0.35f; height = 0.9f; break;
                case "Gate":
                    var gw = Mat(new Color(0.7f, 0.5f, 0.3f)); var gd = Mat(woodC);
                    Prim(PrimitiveType.Cube, g.transform, new Vector3(-1.4f, 1.5f, 0), new Vector3(0.2f, 3f, 0.2f), gd);
                    Prim(PrimitiveType.Cube, g.transform, new Vector3(1.4f, 1.5f, 0), new Vector3(0.2f, 3f, 0.2f), gd);
                    Prim(PrimitiveType.Cube, g.transform, new Vector3(0, 1.55f, 0), new Vector3(3.0f, 0.5f, 0.3f), gw);
                    gate = true; halfDepth = 0.3f; height = 1.2f; break;
            }
            return g;
        }

        // ---------- Pickups ----------
        public static GameObject BuildPickup(int kind)
        {
            var g = new GameObject("Pickup" + kind);
            switch (kind)
            {
                case 0: // coin
                    Prim(PrimitiveType.Cylinder, g.transform, Vector3.zero, new Vector3(0.7f, 0.06f, 0.7f), Mat(new Color(1f, 0.8f, 0.15f), 0.6f, true), new Vector3(90, 0, 0));
                    Prim(PrimitiveType.Cylinder, g.transform, Vector3.zero, new Vector3(0.5f, 0.08f, 0.5f), Mat(new Color(0.9f, 0.62f, 0.08f), 0.6f, true), new Vector3(90, 0, 0));
                    break;
                case 1: // gem
                    var gc = Mat(new Color(0.9f, 0.1f, 0.25f), 0.9f, true);
                    Prim(PrimitiveType.Cube, g.transform, Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), gc, new Vector3(45, 45, 0));
                    break;
                case 2: PowerOrb(g, new Color(0.2f, 0.5f, 1f)); break;   // shield
                case 3: PowerOrb(g, new Color(0.9f, 0.15f, 0.15f)); break; // magnet
                case 4: PowerOrb(g, new Color(1f, 0.6f, 0.1f)); break;   // speed boost
                case 5: PowerOrb(g, new Color(0.65f, 0.3f, 0.85f)); break; // score x2
            }
            return g;
        }

        static void PowerOrb(GameObject g, Color c)
        {
            Prim(PrimitiveType.Sphere, g.transform, Vector3.zero, new Vector3(0.9f, 0.9f, 0.9f), Mat(new Color(c.r, c.g, c.b, 1f), 0.8f, true));
            Prim(PrimitiveType.Sphere, g.transform, Vector3.zero, new Vector3(1.15f, 1.15f, 1.15f), Mat(Color.Lerp(c, Color.white, 0.6f), 0.9f, true));
            g.transform.GetChild(1).gameObject.SetActive(false);
            Prim(PrimitiveType.Cube, g.transform, Vector3.zero, new Vector3(0.35f, 0.35f, 0.35f), Mat(Color.white, 0.9f, true), new Vector3(45, 45, 0));
        }

        // ---------- Scenery ----------
        static readonly Color leafA = new Color(0.2f, 0.5f, 0.2f);
        static readonly Color leafB = new Color(0.3f, 0.6f, 0.25f);

        public static void Palm(Transform parent, Vector3 pos, float h)
        {
            var root = Empty("Palm", parent, pos);
            Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0, h * 0.5f, 0), new Vector3(0.35f, h * 0.5f, 0.35f), Mat(new Color(0.45f, 0.32f, 0.2f)), new Vector3(0, 0, Random.Range(-6f, 6f)));
            for (int i = 0; i < 7; i++)
            {
                var pivot = Empty("Frond", root.transform, new Vector3(0, h, 0));
                pivot.transform.localEulerAngles = new Vector3(0, i * (360f / 7f), 0);
                Prim(PrimitiveType.Cube, pivot.transform, new Vector3(0, -0.15f, 1.35f), new Vector3(0.55f, 0.07f, 2.8f), Mat(i % 2 == 0 ? leafA : leafB), new Vector3(24, 0, 0));
            }
        }

        public static void Acacia(Transform parent, Vector3 pos, float h)
        {
            var root = Empty("Acacia", parent, pos);
            Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0, h * 0.5f, 0), new Vector3(0.28f, h * 0.5f, 0.28f), Mat(new Color(0.36f, 0.26f, 0.17f)));
            Prim(PrimitiveType.Sphere, root.transform, new Vector3(0, h + 0.2f, 0), new Vector3(3.6f, 0.9f, 3.6f), Mat(leafA));
            Prim(PrimitiveType.Sphere, root.transform, new Vector3(0.4f, h + 0.5f, 0.2f), new Vector3(2.2f, 0.6f, 2.2f), Mat(leafB));
        }

        public static void Bush(Transform parent, Vector3 pos, float s)
        {
            var root = Empty("Bush", parent, pos);
            Prim(PrimitiveType.Sphere, root.transform, new Vector3(0, 0.4f * s, 0), Vector3.one * 1.2f * s, Mat(new Color(0.2f, 0.42f, 0.18f)));
            Prim(PrimitiveType.Sphere, root.transform, new Vector3(0.6f * s, 0.3f * s, 0.2f * s), Vector3.one * 0.9f * s, Mat(new Color(0.28f, 0.52f, 0.22f)));
            Prim(PrimitiveType.Sphere, root.transform, new Vector3(-0.5f * s, 0.3f * s, -0.2f * s), Vector3.one * 0.8f * s, Mat(new Color(0.24f, 0.46f, 0.2f)));
        }

        public static void SideRock(Transform parent, Vector3 pos, float s, Color tint)
        {
            Prim(PrimitiveType.Sphere, parent, pos + new Vector3(0, 0.3f * s, 0), new Vector3(1.6f * s, 1.0f * s, 1.3f * s), Mat(tint), new Vector3(0, Random.Range(0f, 180f), 0));
        }

        public static void SideCactus(Transform parent, Vector3 pos, float s)
        {
            var root = Empty("Cactus", parent, pos);
            var cg = Mat(new Color(0.27f, 0.5f, 0.27f));
            Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0, 1.2f * s, 0), new Vector3(0.5f * s, 1.2f * s, 0.5f * s), cg);
            Prim(PrimitiveType.Cylinder, root.transform, new Vector3(-0.55f * s, 1.3f * s, 0), new Vector3(0.25f * s, 0.45f * s, 0.25f * s), cg);
            Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0.55f * s, 1.6f * s, 0), new Vector3(0.25f * s, 0.45f * s, 0.25f * s), cg);
        }

        public static void SideLog(Transform parent, Vector3 pos)
        {
            Prim(PrimitiveType.Cylinder, parent, pos + new Vector3(0, 0.35f, 0), new Vector3(0.7f, 1.6f, 0.7f), Mat(woodC), new Vector3(0, Random.Range(0f, 180f), 90));
        }

        public static void Dune(Transform parent, Vector3 pos, Vector3 scale, Color c)
        {
            Prim(PrimitiveType.Sphere, parent, pos, scale, Mat(c, 0.03f));
        }
    }
}
