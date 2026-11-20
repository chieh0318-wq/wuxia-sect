using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public static class CharacterModelFactory
    {
        private const string Path = "M3Characters/";
        private static readonly MaterialPropertyBlock Properties = new();
        private static readonly Dictionary<string, Material> Materials = new();
        private static readonly Dictionary<Mesh, Mesh> VisibleSkin = new();
        private static readonly Dictionary<(Mesh, string), Mesh> FittedAccessories = new();
        private static AnimationClip idle, walk, formalWalk;
        private static bool warned;

        internal static Material Cloth => MaterialFor("Woven cloth", null, .14f, true);

        public static GameObject Build(CharacterLook look, Transform parent = null)
        {
            look ??= new CharacterLook();
            GameObject result = TryBuild(look);
            if (result == null)
            {
                if (!warned) Debug.LogWarning("[M3 characters] Humanoid resources unavailable; using fallback figures.");
                warned = true;
                result = CharacterFactory.BuildFallback(look);
            }
            result.transform.SetParent(parent, false);
            return result;
        }

        private static GameObject TryBuild(CharacterLook look)
        {
            var spec = look.model?.HasOverrides == true ? look.model : null;
            bool female = spec?.@base == "female" || (spec?.@base != "male" && look.gender == "女");
            string sex = female ? "Female" : "Male";
            var prefab = Resources.Load<GameObject>(Path + "Superhero_" + sex + "_FullBody");
            if (prefab == null) return null;
            if (idle == null)
            {
                var clips = Resources.LoadAll<AnimationClip>(Path + "UAL1_Standard");
                idle = Clip(clips, "Idle_Loop");
                walk = Clip(clips, "Walk_Loop");
                formalWalk = Clip(clips, "Walk_Formal_Loop");
            }
            if (idle == null || walk == null) return null;

            var figure = new GameObject("Tang humanoid");
            var body = UnityEngine.Object.Instantiate(prefab, figure.transform, false);
            var rig = body.GetComponent<Animator>();
            var skin = body.GetComponentsInChildren<SkinnedMeshRenderer>()
                .FirstOrDefault(renderer => renderer.name.StartsWith("Superhero_", StringComparison.OrdinalIgnoreCase));
            if (rig == null || !rig.isHuman || skin == null)
            {
                UnityEngine.Object.Destroy(figure);
                return null;
            }

            bool elder = look.age >= 50 || look.role == "師父";
            string origin = look.origin ?? "";
            bool western = origin.Contains("西域") || origin.Contains("粟特") || origin.Contains("胡商") || origin == "hu" || origin == "sute" ||
                look.id == "hero_m8" || look.id == "hero_f5";
            string role = look.role ?? "";
            string style = Value(spec?.style) ?? (role.Contains("武將") || role.Contains("武官") || origin.Contains("前府兵") ? "armor" :
                western ? "yuanling" : role == "師父" || role.Contains("道") || origin == "louguan" ? "taoist" :
                female ? "ruqun" : "jiaoling");
            string hair = Value(spec?.hair) ?? Value(look.hairStyle) ?? (female ? "bun" : "topknot");
            string beard = Value(spec?.beard) ?? (!female && elder ? "long" : role.Contains("武將") ? "short" : look.id == "hero_m2" ? "stubble" : "none");
            Color robe = Tint(Value(spec?.robe) ?? look.robeColor, new Color(.32f, .39f, .41f));
            Color lining = Tint(spec?.robe2, Color.Lerp(robe, new Color(.71f, .65f, .55f), .57f));
            Color sash = Tint(Value(spec?.sash) ?? look.sashColor, Color.Lerp(robe, new Color(.29f, .23f, .18f), .6f));
            Color ink = elder ? new Color(.64f, .62f, .57f) : western ? new Color(.22f, .145f, .106f) :
                Tint(look.hairColor, new Color(.125f, .108f, .093f));
            Color complexion = Color.Lerp(new Color(1, 1, 1), new Color(.72f, .64f, .62f),
                Mathf.Clamp01(look.skinTone * .78f - (western ? .17f : 0)));
            body.AddComponent<CharacterModelMeshes>();
            var root = body.transform;
            var bones = root.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
            var neck = rig.GetBoneTransform(HumanBodyBones.Neck) ?? rig.GetBoneTransform(HumanBodyBones.Head);
            var hand = rig.GetBoneTransform(HumanBodyBones.LeftHand);
            var matrix = root.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            var points = skin.sharedMesh.vertices;
            float ground = points.Min(p => matrix.MultiplyPoint3x4(p).y);
            float height = points.Max(p => matrix.MultiplyPoint3x4(p).y) - ground;
            Skin(skin, matrix, root.InverseTransformPoint(neck.position).y - height * .006f, height * .061f,
                Mathf.Abs(root.InverseTransformPoint(hand.position).x) - height * .012f);
            Set(skin, MaterialFor(sex + " skin",
                female ? "T_Superhero_Female_Light_BaseColor" : "T_Superhero_Male_Ligh", .21f), complexion);
            foreach (var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (renderer.name == "Eyes")
                    Set(renderer, MaterialFor("Eyes", "T_Eye_Brown", .25f), Color.white);
                if (renderer.name == "Eyebrows")
                    Set(renderer, MaterialFor("Hair", null, .22f), Color.Lerp(ink, new Color(.12f,.1f,.09f), .24f));
            }
            if (style == "armor")
                Attach(root, bones, sex + "_Peasant_Legs", Cloth, Color.Lerp(robe, new Color(.18f, .17f, .16f), .55f));
            Attach(root, bones, sex + "_Peasant_Feet", Cloth, new Color(.19f, .17f, .15f), fit: style != "armor" ? "cloth shoes" : null);
            new TangWardrobe(rig, ground, height, robe, lining, sash).Dress(style, female);
            Hair(rig, bones, hair, beard, ink, sash, ground, height);
            float seed = ((look.id ?? look.hairStyle ?? sex).GetHashCode() & 255) / 255f;
            float stature = Mathf.Lerp(.95f, 1.05f, seed);
            float build = Mathf.Lerp(.93f, 1.08f, Mathf.Clamp01(look.build));
            figure.transform.localScale = new Vector3(build, stature, build);
            float normalize = (female ? 1.67f : 1.77f) / height;
            root.localScale *= normalize;
            root.localPosition = Vector3.down * ground * normalize;
            figure.AddComponent<HumanoidMotion>().Initialize(rig, idle,
                elder && formalWalk != null ? formalWalk : walk, elder, seed, ground,
                style == "ruqun" ? 7 : 3);
            return figure;
        }

        private static AnimationClip Clip(AnimationClip[] clips, string name) =>
            Array.Find(clips, clip => clip.isHumanMotion &&
                (clip.name == name || clip.name.EndsWith("|" + name, StringComparison.Ordinal)));

        private static void Skin(SkinnedMeshRenderer renderer, Matrix4x4 matrix, float neck, float neckWidth, float hands)
        {
            Mesh source = renderer.sharedMesh;
            if (!VisibleSkin.TryGetValue(source, out var mesh))
            {
                var positions = source.vertices;
                var keep = Array.ConvertAll(positions, vertex =>
                {
                    Vector3 p = matrix.MultiplyPoint3x4(vertex);
                    return (p.y >= neck && (p.y >= neck + neckWidth || Mathf.Abs(p.x) <= neckWidth * .75f)) ||
                        Mathf.Abs(p.x) >= hands;
                });
                mesh = UnityEngine.Object.Instantiate(source);
                mesh.name = source.name + " visible skin";
                for (int sub = 0; sub < source.subMeshCount; sub++)
                {
                    var indices = source.GetTriangles(sub);
                    var exposed = new List<int>();
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                        if (!keep[a] || !keep[b] || !keep[c]) continue;
                        exposed.Add(a);
                        exposed.Add(b);
                        exposed.Add(c);
                    }
                    mesh.SetTriangles(exposed, sub, false);
                }
                VisibleSkin.Add(source, mesh);
            }
            renderer.sharedMesh = mesh;
        }

        private static void Attach(Transform root, Dictionary<string, Transform> bones,
            string name, Material material, Color color, string fit = null)
        {
            var prefab = Resources.Load<GameObject>(Path + name);
            if (prefab == null) return;
            var original = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            if (original == null) return;
            var part = new GameObject(name).AddComponent<SkinnedMeshRenderer>();
            Transform t = part.transform, source = original.transform;
            t.SetParent(root, false);
            t.localPosition = prefab.transform.InverseTransformPoint(source.position);
            t.localRotation = Quaternion.Inverse(prefab.transform.rotation) * source.rotation;
            t.localScale = source.lossyScale;
            var originalMesh = original.sharedMesh;
            part.sharedMesh = fit == null ? originalMesh : FitAccessory(originalMesh,
                prefab.transform.worldToLocalMatrix * source.localToWorldMatrix, fit);
            part.bones = Array.ConvertAll(original.bones, bone => bones[bone.name]);
            part.rootBone = bones[original.rootBone.name];
            part.localBounds = original.localBounds;
            Set(part, material, color);
        }

        private static Mesh FitAccessory(Mesh source, Matrix4x4 toRoot, string fit)
        {
            if (FittedAccessories.TryGetValue((source, fit), out var fitted)) return fitted;
            var fromRoot = toRoot.inverse;
            var points = source.vertices;
            float lower = points.Min(p => toRoot.MultiplyPoint3x4(p).y);
            float upper = points.Max(p => toRoot.MultiplyPoint3x4(p).y);
            float beardLine = Mathf.Lerp(lower, upper, .44f);
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 p = toRoot.MultiplyPoint3x4(points[i]);
                if (fit == "cloth shoes" && p.y > .10f)
                    p.y = .10f + (p.y - .10f) * .4f;
                if (fit != "cloth shoes" && p.y < beardLine)
                {
                    float length = fit == "long" ? 1.78f : .68f;
                    p.y = beardLine + (p.y - beardLine) * length;
                }
                points[i] = fromRoot.MultiplyPoint3x4(p);
            }
            fitted = UnityEngine.Object.Instantiate(source);
            fitted.name = source.name + " " + fit;
            fitted.vertices = points;
            fitted.RecalculateNormals();
            fitted.RecalculateBounds();
            FittedAccessories.Add((source, fit), fitted);
            return fitted;
        }

        private static void Hair(Animator rig, Dictionary<string, Transform> bones, string style,
            string beard, Color ink, Color sash, float ground, float height)
        {
            Transform root = rig.transform, head = rig.GetBoneTransform(HumanBodyBones.Head);
            bool longHair = style == "loose" || style == "halfup" || style == "braids";
            string mesh = style == "twinbuns" ? "Hair_Buns" : longHair ? "Hair_Long" : "Hair_SimpleParted";
            var material = MaterialFor("Hair", null, .22f);
            Attach(root, bones, mesh, material, ink);
            if (beard != "none") Attach(root, bones, "Hair_Beard", material,
                beard == "stubble" ? Color.Lerp(ink, new Color(.27f,.19f,.16f), .75f) : ink,
                beard is "long" or "stubble" ? beard : null);
            if (style == "loose" || style == "twinbuns") return;

            // Accessories are placed in the avatar's rest coordinates, then follow the head bone.
            Vector3 crown = root.InverseTransformPoint(head.position);
            crown.y = ground + height + .012f;
            crown.z += height * .022f;
            if (style == "braids")
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 start = crown + new Vector3(side * height * .047f, -height * .065f, height * .015f);
                    for (int link = 0; link < 7; link++)
                    {
                        float t = link / 6f;
                        Vector3 point = start + new Vector3(side * height * .018f * t, -height * .11f * t,
                            height * .033f * t);
                        Ornament("Braided hair", point, Vector3.Lerp(new(.027f,.040f,.024f), new(.014f,.028f,.014f), t),
                            PrimitiveType.Sphere, ink, -16, (link % 2 == 0 ? 22 : -22) * side);
                        if (link == 6) Ornament("Braid tie", point, new(.018f,.010f,.017f), PrimitiveType.Sphere, sash);
                    }
                }
                return;
            }
            if (style == "bun") crown += new Vector3(0, -height * .022f, height * .022f);
            if (style == "ponytail")
            {
                Ornament("Bound tail", crown + new Vector3(0,-height*.083f,height*.036f),
                    new(.047f,.174f,.042f), PrimitiveType.Sphere, ink, 22);
                Ornament("Hair ribbon", crown + Vector3.down * height * .024f,
                    new(.048f,.016f,.049f), PrimitiveType.Sphere, sash);
            }
            Ornament("Gathered knot", crown, new(.076f,.074f,.081f), PrimitiveType.Sphere, ink);
            if (style == "crown" || style == "taoistbun")
                Ornament("Small crown", crown + Vector3.up * .018f, new(.063f,.048f,.065f),
                    PrimitiveType.Cylinder, Color.Lerp(sash, new Color(.38f,.34f,.22f), .6f));
            if (style != "halfup" && style != "ponytail")
                Ornament("Hairpin", crown + Vector3.up * .013f, new(.007f,.081f,.007f),
                    PrimitiveType.Cylinder, new Color(.47f,.36f,.23f), 0, 78);

            void Ornament(string name, Vector3 position, Vector3 size, PrimitiveType primitive, Color color,
                float pitch = 0, float roll = 0)
            {
                var part = CharacterFactory.Part(root, primitive, position, size * (height / 1.77f), color);
                part.name = name;
                part.transform.localRotation = Quaternion.Euler(pitch, 0, roll);
                part.transform.SetParent(head, true);
            }
        }

        private static Material MaterialFor(string label, string texture, float smoothness, bool doubleSided = false)
        {
            if (Materials.TryGetValue(label, out var material)) return material;
            material = new Material(Resources.Load<Material>("Materials/Solid")) { name = label };
            material.SetFloat("_Smoothness", smoothness);
            if (texture != null) material.SetTexture("_BaseMap", Resources.Load<Texture2D>(Path + texture));
            if (doubleSided)
            {
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                material.SetTexture("_BaseMap", FabricWeave());
            }
            Materials.Add(label, material);
            return material;
        }

        private static Texture2D FabricWeave()
        {
            const int size = 64;
            var weave = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Muted plain weave" };
            var texels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int fiber = ((x / 2 + y / 2) & 1) * 9;
                int variation = (x * 13 + y * 23 + x * y * 3) & 7;
                byte shade = (byte)(235 + fiber + variation);
                texels[y * size + x] = new Color32(shade, shade, shade, 255);
            }
            weave.SetPixels32(texels);
            weave.Apply(true, true);
            return weave;
        }

        private static void Set(Renderer renderer, Material material, Color color)
        {
            if (renderer is SkinnedMeshRenderer skin)
            {
                var slots = new Material[skin.sharedMesh.subMeshCount];
                Array.Fill(slots, material);
                renderer.sharedMaterials = slots;
            }
            else renderer.sharedMaterial = material;
            Paint(renderer, color);
        }

        internal static void Paint(Renderer renderer, Color color)
        {
            Properties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(Properties);
        }

        private static string Value(string text) => string.IsNullOrEmpty(text) ? null : text;

        private static Color Tint(string html, Color fallback) =>
            ColorUtility.TryParseHtmlString(html, out var result) ? result : fallback;
    }

    internal sealed class CharacterModelMeshes : MonoBehaviour
    {
        private readonly List<Mesh> generated = new();
        internal void Own(Mesh mesh) => generated.Add(mesh);
        private void OnDestroy()
        {
            foreach (Mesh mesh in generated) Destroy(mesh);
        }
    }
}
