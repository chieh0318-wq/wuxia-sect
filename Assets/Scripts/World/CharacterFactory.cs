using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    // CharacterLook lives in Data/CreationData.cs (id/label/portrait plus the appearance fields used here).
    public static class CharacterFactory
    {
        private static readonly Dictionary<string, Mesh> meshes = new();
        private static Material solid, cloth;
        private static Mesh sphere, cube;
        private static readonly (float y,float x,float z)[] JacketProfile =
        {
            (.82f,.159f,.111f),(1.0f,.143f,.105f),(1.21f,.194f,.123f),
            (1.34f,.202f,.103f),(1.39f,.075f,.067f)
        };
        private static readonly MaterialPropertyBlock paint = new();

        private static CharacterCatalog storyLooks;

        public static CharacterLook ForStory(string id)
        {
            storyLooks ??= M2Data.Load<CharacterCatalog>("characters");
            var entry = storyLooks.storyCharacters?.FirstOrDefault(person => person.id == id);
            var look = StoryPalette(id);
            look.id = id;
            if (entry == null) return look;
            look.age = entry.age;
            look.origin = entry.faction;
            look.model = entry.model;
            look.gender = entry.gender ?? look.gender;
            if (!StoryIds.Contains(id)) look.role = entry.role ?? look.role;
            return look;
        }

        private static CharacterLook StoryPalette(string id) => id switch
        {
            "master" => Look("男", "#777971", "#4B4840", "taoistbun", "#D3C8B9", .43f, "師父"),
            "friend_m" => Look("男", "#4D6C70", "#A68055", "crown", "#26272A", .37f, "客卿"),
            "friend_f" => Look("女", "#738579", "#794E52", "halfup", "#27252C", .34f, "客卿"),
            "elder_villain" => Look("男", "#7A4747", "#B58C55", "crown", "#4A4141", .65f, "掌門"),
            "warlord" => Look("男", "#403E47", "#9C5D44", "topknot", "#282423", .92f, "武將"),
            "official" => Look("男", "#674454", "#42414D", "crown", "#292829", .63f, "官員"),
            _ => Look("男", "#495766", "#545047", "ponytail", "#262626", .47f, "掌門")
        };

        private static CharacterLook Look(string gender, string robe, string sash, string style,
            string hair, float build, string role) => new CharacterLook
        {
            gender = gender, robeColor = robe, sashColor = sash, hairStyle = style,
            hairColor = hair, build = build, role = role
        };

        private static readonly string[] MaleStyles = { "topknot", "bun", "crown", "halfup" };
        private static readonly string[] FemaleStyles = { "halfup", "twinbuns", "ponytail", "bun" };
        private static readonly string[] RecruitRobes =
            { "#5B6670", "#6E5B48", "#4F6355", "#71594F", "#5A5468", "#7A6A50", "#44585E", "#6B4E4A" };
        private static readonly string[] RecruitSashes = { "#8E3B2E", "#545047", "#9C7A45", "#3F4A52", "#6D3F3A" };
        private static readonly string[] HairInks = { "#262220", "#302A25", "#1F1D1E", "#3A2E26" };

        /// <summary>Random look for a generated recruit, keyed by gender and role.</summary>
        internal static CharacterLook ForRecruit(string gender, string role)
        {
            bool female = gender == "女";
            var styles = female ? FemaleStyles : MaleStyles;
            return new CharacterLook
            {
                gender = gender, role = role,
                robeColor = RecruitRobes[UnityEngine.Random.Range(0, RecruitRobes.Length)],
                sashColor = RecruitSashes[UnityEngine.Random.Range(0, RecruitSashes.Length)],
                hairColor = HairInks[UnityEngine.Random.Range(0, HairInks.Length)],
                hairStyle = styles[UnityEngine.Random.Range(0, styles.Length)],
                skinTone = UnityEngine.Random.Range(.18f, .78f),
                build = UnityEngine.Random.Range(.2f, .8f)
            };
        }

        private static readonly HashSet<string> StoryIds = new()
            { "master", "friend_m", "friend_f", "elder_villain", "warlord", "official", "rival_head" };

        internal static CharacterLook ForDisciple(DiscipleData data, int order)
        {
            if (data.look != null && (data.look.model?.HasOverrides == true || !string.IsNullOrEmpty(data.look.hairStyle)))
            {
                data.look.age = data.age;
                data.look.origin = data.origin ?? data.trait;
                if (string.IsNullOrEmpty(data.look.gender)) data.look.gender = data.gender;
                if (string.IsNullOrEmpty(data.look.role)) data.look.role = data.title;
                if (string.IsNullOrEmpty(data.look.id)) data.look.id = data.id;
                if (string.IsNullOrEmpty(data.look.robeColor)) data.look.robeColor = data.robeColor;
                return data.look;
            }
            storyLooks ??= M2Data.Load<CharacterCatalog>("characters");
            if (StoryIds.Contains(data.id) || storyLooks.storyCharacters?.Any(person => person.id == data.id) == true)
                return ForStory(data.id);
            string[] styles = data.gender == "女"
                ? new[] { "halfup", "twinbuns", "ponytail", "bun" }
                : new[] { "topknot", "bun", "crown", "halfup" };
            return new CharacterLook
            {
                gender = data.gender, robeColor = data.robeColor, role = data.title, age = data.age, origin = data.origin ?? data.trait, id = data.id,
                hairStyle = styles[order % styles.Length], skinTone = .24f + order % 5 * .13f,
                build = .28f + order % 4 * .14f
            };
        }

        private const float StandeeHeight = 1.85f;
        private static readonly Dictionary<Texture2D, Sprite> standeeSprites = new();
        private static readonly Dictionary<(string id, string gender, bool portraitFallback), Texture2D> standeeTextures = new();
        private static Sprite standeeShadow;
        private static Dictionary<string, string> standeeFallbacks;

        private static Dictionary<string, string> LoadStandeeFallbacks()
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var source = Resources.Load<TextAsset>("Standees/fallbacks");
            if (source == null) return mapping;
            foreach (string line in source.text.Split('\n'))
            {
                string[] fields = line.Trim().Split('\t');
                if (fields.Length == 2)
                    mapping[fields[0]] = fields[1];
            }
            return mapping;
        }

        internal static Vector3 StandeeLabelOffset =>
            CameraController.CourtyardRotation * (Vector3.up * (StandeeHeight + .20f));

        internal static Texture2D StandeeTexture(string id, string gender, bool portraitFallback = false)
        {
            id = (PortraitService.StoryId(id, gender) ?? "").Trim().ToLowerInvariant();
            int expression = id.IndexOf("__", StringComparison.Ordinal);
            if (expression >= 0) id = id.Substring(0, expression);
            var key = (id, gender, portraitFallback);
            if (standeeTextures.TryGetValue(key, out var texture)) return texture;

            bool female = gender == "女" || id.StartsWith("hero_f") || id.StartsWith("npc_yf") ||
                id.StartsWith("friend_f");
            string sex = female ? "f" : "m";
            string role = id == "hero" || id.StartsWith("hero_") ? "hero" : "ally";
            standeeFallbacks ??= LoadStandeeFallbacks();
            standeeFallbacks.TryGetValue(id, out string fallback);
            texture = Resources.Load<Texture2D>("Standees/" + id) ??
                (fallback == null ? null : Resources.Load<Texture2D>(fallback)) ??
                (portraitFallback ? PortraitService.Resolve(id, gender) : Resources.Load<Texture2D>("Standees/hero_" + sex + "1")) ??
                Resources.Load<Texture2D>("Battle/" + role + "_" + sex);
            standeeTextures.Add(key, texture);
            return texture;
        }

        internal static Transform BuildStandee(DiscipleData data, Transform parent, out Collider hit,
            Quaternion? rotation = null)
        {
            var texture = StandeeTexture(PortraitService.IdOrDefault(data.portrait, data.id), data.gender);
            if (!standeeSprites.TryGetValue(texture, out var sprite))
            {
                sprite = StandeeSprite(texture);
                standeeSprites.Add(texture, sprite);
            }

            var body = new GameObject("Ink-wash standee").transform;
            body.SetParent(parent, false);
            body.localPosition = Vector3.up * .006f;
            body.rotation = rotation ?? CameraController.CourtyardRotation;
            body.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();

            float width = StandeeHeight * texture.width / texture.height;
            var center = Vector3.up * (StandeeHeight * .5f);
            AddStandeeLayer(body, "Figure", sprite, center, new(width, StandeeHeight), Color.white, 0);

            var box = body.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = new Vector3(Mathf.Min(width, .95f), StandeeHeight, .12f);
            hit = box;
            var shadow = AddStandeeLayer(parent, "Soft ground shadow", StandeeShadow, Vector3.up * .004f,
                new(.95f, .64f), new(.15f, .11f, .08f, .62f), -1);
            shadow.localRotation = Quaternion.Euler(90, 0, 0);
            return body;
        }

        private static Sprite StandeeSprite(Texture2D texture) => Sprite.Create(texture,
            new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f),
            texture.height, 0, SpriteMeshType.FullRect);

        private static Transform AddStandeeLayer(Transform parent, string name, Sprite sprite, Vector3 position,
            Vector2 size, Color color, int order)
        {
            var layer = new GameObject(name).AddComponent<SpriteRenderer>();
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = position;
            layer.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
            layer.sprite = sprite;
            layer.color = color;
            layer.sortingOrder = order;
            return layer.transform;
        }

        internal static Sprite StandeeShadow => standeeShadow ??= CreateStandeeShadow();

        private static Sprite CreateStandeeShadow()
        {
            const int size = 48;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var point = new Vector2(x + .5f, y + .5f) * (2f / size) - Vector2.one;
                float alpha = 1 - Mathf.SmoothStep(.06f, 1, point.magnitude);
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply(false, true);
            return StandeeSprite(texture);
        }

        internal static GameObject BuildFallback(CharacterLook look)
        {
            look ??= new CharacterLook();
            if (solid == null) solid = Resources.Load<Material>("Materials/Solid");
            if (cloth == null && Resources.Load<Shader>("FigureCloth") is Shader fabric)
                cloth = new Material(fabric);
            if (sphere == null) sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            if (cube == null) cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var body = new GameObject("Tang-robed figure");
            var root = body.transform;
            float breadth = Mathf.Lerp(.87f, 1.13f, look.build) * (look.gender == "女" ? .93f : 1);
            root.localScale = new Vector3(breadth, 1, 1);
            Color ink = Tint(look.hairColor, new Color(.12f, .095f, .086f));
            Color robe = Tint(look.robeColor, look.role == "雜役" ? new Color(.45f, .39f, .31f) : new Color(.28f, .38f, .42f));
            robe = Color.Lerp(robe, new Color(.45f, .42f, .37f), .22f);
            Color lining = Color.Lerp(robe, new Color(.79f, .68f, .53f), .64f);
            Color sash = Tint(look.sashColor, robe * .64f);
            Color skin = Color.Lerp(new Color(.84f,.66f,.51f), new Color(.54f,.32f,.20f), look.skinTone);
            var torso = Revolve("Cross-collar jacket",root,robe,0,JacketProfile);
            var skirt = Revolve("Pleated ankle-length robe", root, robe, .075f,
                (.15f,.245f,.174f),(.32f,.219f,.154f),(.65f,.181f,.132f),(1.00f,.149f,.104f));
            Revolve("Woven sash", root, sash, 0, (.925f,.169f,.126f),(.99f,.164f,.122f),(1.015f,.161f,.118f));
            Ribbon("Collar shadow",torso,robe * .63f,.053f,
                new(.064f,1.385f),new(.044f,1.319f),new(-.013f,1.231f),new(-.082f,1.133f));
            Ribbon("Crossed collar",torso,lining,.033f,
                new(.064f,1.385f),new(.044f,1.319f),new(-.013f,1.231f),new(-.082f,1.133f));
            Ribbon("Inner collar",torso,Color.Lerp(lining,robe,.2f),.029f,
                new(-.065f,1.385f),new(-.049f,1.317f),new(-.009f,1.234f));
            Strip("Hanging sash", root, sash, new(.11f,.98f,.102f), new(.14f,.67f,.130f), .047f);
            Strip("Sash tail", root, sash, new(.095f,.977f,.118f), new(.076f,.75f,.129f), .028f);
            Orb("Neck", torso, skin, new(0,1.405f,.006f), new(.104f,.144f,.104f));
            Orb("Face", torso, skin, new(0,1.532f,.018f), new(.184f,.232f,.171f));
            Orb("Hair cap", torso, ink, new(0,1.601f,-.018f), new(.194f,.164f,.214f));
            Orb("Swept hairline", torso, ink, new(-.018f,1.614f,.060f), new(.145f,.066f,.093f));
            Orb("Nose bridge", torso, Color.Lerp(skin,ink,.13f), new(0,1.537f,.103f), new(.024f,.048f,.025f));
            Orb("Nose tip", torso, skin, new(0,1.521f,.111f), new(.029f,.021f,.026f));
            Orb("Mouth", torso, Color.Lerp(skin,ink,.46f), new(0,1.486f,.102f), new(.039f,.006f,.009f));
            for (int side = -1; side <= 1; side += 2)
            {
                Orb("Ear", torso, skin, new(side*.090f,1.532f,.018f), new(.029f,.046f,.031f));
                Orb("Eye socket", torso, Color.Lerp(skin,ink,.30f), new(side*.037f,1.551f,.096f), new(.036f,.016f,.013f));
                Orb("Eye", torso, ink, new(side*.037f,1.551f,.102f), new(.024f,.008f,.009f));
                Orb("Brow", torso, ink, new(side*.037f,1.570f,.100f), new(.033f,.007f,.009f))
                    .localRotation = Quaternion.Euler(0, -side * 22, side * 9);
                Orb("Temple lock", torso, ink, new(side*.079f,1.577f,.054f), new(.032f,.090f,.037f))
                    .localRotation = Quaternion.Euler(0, 0, side * -12);
            }
            Hair(torso, look, ink, sash);
            if (look.role == "師父")
                Orb("White beard", torso, ink, new(0,1.448f,.077f), new(.115f,.152f,.080f));

            var limbs = body.AddComponent<CharacterMotion>();
            limbs.SetBody(torso, skirt);
            for (int side = -1; side <= 1; side += 2)
            {
                var arm = Joint(root, new(side*.165f,1.321f,0));
                arm.localRotation = Quaternion.Euler(0,0,side*13);
                Revolve("Wide sleeve", arm, robe, .055f,
                    (-.505f,.099f,.079f),(-.46f,.124f,.107f),(-.36f,.119f,.104f),
                    (-.22f,.089f,.083f),(-.065f,.076f,.067f),(.040f,.036f,.037f));
                Revolve("Sleeve facing", arm, lining, 0,
                    (-.510f,.102f,.082f),(-.482f,.115f,.097f));
                Orb("Sleeve opening", arm, robe * .50f, new(0,-.503f,0), new(.197f,.025f,.156f));
                Orb("Hand", arm, skin, new(0,-.526f,.025f), new(.064f,.083f,.043f));
                Orb("Fingers", arm, Color.Lerp(skin,ink,.08f), new(side*.004f,-.558f,.032f), new(.051f,.048f,.031f));
                Orb("Thumb", arm, skin, new(-side*.028f,-.527f,.044f), new(.019f,.044f,.022f))
                    .localRotation = Quaternion.Euler(0, 0, side * 18);
                var leg = Joint(root, new(side*.096f,.73f,0));
                Orb("Trousers", leg, sash, new(0,-.35f,0), new(.13f,.67f,.13f));
                Orb("Cloth boot", leg, ink, new(0,-.660f,.047f), new(.126f,.140f,.244f));
                limbs.SetLimb(side < 0 ? 0 : 1, arm, leg);
            }
            return body;
        }

        private static void Hair(Transform root, CharacterLook look, Color ink, Color tie)
        {
            switch (look.hairStyle)
            {
                case "twinbuns":
                    Orb("Left bun",root,ink,new(-.104f,1.622f,-.020f),new(.085f,.081f,.085f));
                    Orb("Right bun",root,ink,new(.104f,1.622f,-.020f),new(.085f,.081f,.085f));
                    break;
                case "ponytail":
                case "halfup":
                    Orb("Gathered bun",root,ink,new(0,1.601f,-.092f),new(.100f,.102f,.090f));
                    Orb("Long hair",root,ink,new(0,1.453f,-.109f),new(look.hairStyle == "halfup" ? .143f : .074f,.29f,.068f));
                    break;
                case "crown":
                    Orb("Hair crown",root,tie,new(0,1.676f,-.035f),new(.102f,.103f,.096f));
                    Strip("Crown pin",root,tie,new(-.093f,1.679f,-.037f),new(.093f,1.679f,-.037f),.012f);
                    break;
                case "bun":
                    Orb("Low bun",root,ink,new(0,1.569f,-.107f),new(.117f,.109f,.095f));
                    break;
                default:
                    Orb("Topknot",root,ink,new(0,1.681f,-.035f),new(.087f,.102f,.088f));
                    break;
            }
        }

        /// <summary>Primitive accessory helper for the legacy model factory.</summary>
        internal static GameObject Part(Transform parent, PrimitiveType shape, Vector3 position, Vector3 size, Color tint)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = Resources.Load<Material>("Materials/Solid");
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", tint);
            renderer.SetPropertyBlock(properties);
            part.GetComponent<Collider>().enabled = false;
            return part;
        }

        private static Color Tint(string value, Color fallback) =>
            ColorUtility.TryParseHtmlString(value, out var color) ? color : fallback;

        private static Transform Joint(Transform parent, Vector3 position)
        {
            var joint = new GameObject("Pose joint").transform;
            joint.SetParent(parent, false);
            joint.localPosition = position;
            return joint;
        }

        private static Transform Piece(string name, Transform parent, Mesh mesh, Color color)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = solid;
            paint.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(paint);
            return part.transform;
        }

        private static Transform Orb(string name, Transform parent, Color color, Vector3 point, Vector3 size)
        {
            var part = Piece(name,parent,sphere,color);
            part.localPosition = point;
            part.localScale = size;
            return part;
        }

        private static void Strip(string name, Transform parent, Color color, Vector3 a, Vector3 b, float width)
        {
            var part = Piece(name,parent,cube,color);
            part.localPosition = (a+b)*.5f + Vector3.forward*.014f;
            part.localRotation = Quaternion.FromToRotation(Vector3.up,b-a);
            part.localScale = new Vector3(width,Vector3.Distance(a,b),.022f);
        }

        private static void Ribbon(string name,Transform parent,Color color,float width,params Vector2[] line)
        {
            if (!meshes.TryGetValue(name,out var mesh))
            {
                const int subdivisions = 12;
                int samples = (line.Length-1)*subdivisions+1;
                var vertices = new Vector3[samples*2];
                var indices = new int[(samples-1)*6];
                for (int i=0;i<samples;i++)
                {
                    int segment = Mathf.Min(i/subdivisions,line.Length-2);
                    Vector2 p = Vector2.Lerp(line[segment],line[segment+1],(i-segment*subdivisions)/(float)subdivisions);
                    float lift = name == "Crossed collar" ? .004f : 0;
                    vertices[i*2] = CollarPoint(p.x-width*.5f,p.y) + Vector3.forward * lift;
                    vertices[i*2+1] = CollarPoint(p.x+width*.5f,p.y) + Vector3.forward * lift;
                    if (i == samples-1) continue;
                    int v=i*2, t=i*6;
                    indices[t]=v; indices[t+1]=v+2; indices[t+2]=v+1;
                    indices[t+3]=v+1; indices[t+4]=v+2; indices[t+5]=v+3;
                }
                mesh = new Mesh { name = name, vertices = vertices, triangles = indices };
                mesh.RecalculateNormals();
                meshes.Add(name,mesh);
            }
            Piece(name,parent,mesh,color);
        }

        private static Vector3 CollarPoint(float x,float y)
        {
            int upper = 1;
            while (upper < JacketProfile.Length-1 && JacketProfile[upper].y < y) upper++;
            var a = JacketProfile[upper-1];
            var b = JacketProfile[upper];
            float t = Mathf.InverseLerp(a.y,b.y,y);
            float rx = Mathf.Lerp(a.x,b.x,t);
            float rz = Mathf.Lerp(a.z,b.z,t);
            float z = rz*Mathf.Sqrt(Mathf.Max(0,1-(x/rx)*(x/rx)));
            return new Vector3(x,y,z+.005f);
        }

        private static Transform Revolve(string name, Transform parent, Color color, float folds,
            params (float y, float x, float z)[] rings)
        {
            if (!meshes.TryGetValue(name, out var mesh))
            {
                const int sides = 32;
                var points = new Vector3[rings.Length * sides];
                var vertexColors = new Color32[points.Length];
                var indices = new int[(rings.Length-1)*sides*6];
                for (int row=0; row<rings.Length; row++)
                for (int side=0; side<sides; side++)
                {
                    float angle = side * 2*Mathf.PI/sides;
                    float fold = 1 + folds*Mathf.Cos(angle*8);
                    var ring = rings[row];
                    int index = row*sides+side;
                    points[index] = new Vector3(Mathf.Cos(angle)*ring.x*fold,ring.y,Mathf.Sin(angle)*ring.z*fold);
                    float crease = (1 - Mathf.Cos(angle*8)) * .5f;
                    float seam = row == 0 ? .09f : 0;
                    byte shade = (byte)(255 * (1 - seam - crease * folds * 2));
                    vertexColors[index] = new Color32(shade, shade, shade, 255);
                    if (row == rings.Length-1) continue;
                    int next = row*sides+(side+1)%sides;
                    int offset = index*6;
                    indices[offset] = index;
                    indices[offset+1] = index+sides;
                    indices[offset+2] = next;
                    indices[offset+3] = next;
                    indices[offset+4] = index+sides;
                    indices[offset+5] = next+sides;
                }
                mesh = new Mesh { name = name, vertices = points, triangles = indices, colors32 = vertexColors };
                mesh.RecalculateNormals();
                meshes.Add(name,mesh);
            }
            var piece = Piece(name,parent,mesh,color);
            if (cloth != null) piece.GetComponent<MeshRenderer>().sharedMaterial = cloth;
            return piece;
        }
    }

    internal sealed class CharacterMotion : MonoBehaviour
    {
        private readonly Transform[] arms = new Transform[2], legs = new Transform[2];
        private Transform torso, robe;
        private float motion, phase;
        internal bool Walking { get; set; }
        internal void SetBody(Transform jacket, Transform skirt) { torso = jacket; robe = skirt; }
        internal void SetLimb(int i, Transform arm, Transform leg) { arms[i] = arm; legs[i] = leg; }
        private void LateUpdate()
        {
            motion = Mathf.MoveTowards(motion, Walking ? 1 : 0, Time.deltaTime*6);
            phase += Time.deltaTime*10;
            float swing = Mathf.Sin(phase)*motion;
            for (int i=0; i<2; i++)
            {
                int side = i == 0 ? -1 : 1;
                arms[i].localRotation = Quaternion.Euler(swing*side*18,0,side*13);
                legs[i].localRotation = Quaternion.Euler(-swing*side*19,0,0);
            }
            robe.localRotation = Quaternion.Euler(0,swing*2,swing*2);
            torso.localScale = new Vector3(1,1+Mathf.Sin(Time.time*2.5f)*.004f,1);
        }
    }
}
