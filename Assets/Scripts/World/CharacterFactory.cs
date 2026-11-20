using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wuxia
{
    [Serializable]
    public sealed class CharacterLook
    {
        public string gender, robeColor, sashColor, hairColor, hairStyle, role;
        public float skinTone = .5f, build = .5f;
    }

    public static class CharacterFactory
    {
        private static readonly Dictionary<string, Mesh> meshes = new();
        private static Material solid;
        private static Mesh sphere, cube;
        private static readonly MaterialPropertyBlock paint = new();

        public static CharacterLook ForStory(string id) => id switch
        {
            "master" => Look("男", "#777971", "#4B4840", "bun", "#D3C8B9", .43f, "師父"),
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

        internal static CharacterLook ForDisciple(DiscipleData data, int order)
        {
            string[] styles = data.gender == "女"
                ? new[] { "halfup", "twinbuns", "ponytail", "bun" }
                : new[] { "topknot", "bun", "crown", "halfup" };
            return new CharacterLook
            {
                gender = data.gender, robeColor = data.robeColor, role = data.title,
                hairStyle = styles[order % styles.Length], skinTone = .24f + order % 5 * .13f,
                build = .28f + order % 4 * .14f
            };
        }

        public static GameObject Build(CharacterLook look)
        {
            look ??= new CharacterLook();
            if (solid == null) solid = Resources.Load<Material>("Materials/Solid");
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
            var torso = Revolve("Cross-collar jacket", root, robe, 0,
                (.82f,.159f,.111f),(1.0f,.143f,.105f),(1.21f,.194f,.123f),(1.34f,.202f,.103f),(1.39f,.075f,.067f));
            var skirt = Revolve("Pleated ankle-length robe", root, robe, .075f,
                (.15f,.245f,.174f),(.32f,.219f,.154f),(.65f,.181f,.132f),(1.00f,.149f,.104f));
            Revolve("Woven sash", root, sash, 0, (.93f,.158f,.117f),(.99f,.152f,.112f),(1.015f,.147f,.105f));
            Strip("Overlapping collar", torso, lining, new(-.084f,1.379f,.073f), new(.097f,1.07f,.108f), .043f);
            Strip("Inner collar", torso, lining, new(.081f,1.377f,.073f), new(-.006f,1.237f,.126f), .037f);
            Strip("Hanging sash", root, sash, new(.11f,.98f,.102f), new(.14f,.67f,.130f), .047f);
            Strip("Sash tail", root, sash, new(.095f,.977f,.118f), new(.076f,.75f,.129f), .028f);
            Orb("Neck", torso, skin, new(0,1.405f,.006f), new(.104f,.144f,.104f));
            Orb("Face", torso, skin, new(0,1.532f,.018f), new(.184f,.232f,.171f));
            Orb("Hair cap", torso, ink, new(0,1.590f,-.020f), new(.197f,.172f,.181f));
            Orb("Nose", torso, skin, new(0,1.524f,.1f), new(.028f,.048f,.029f));
            Orb("Mouth", torso, Color.Lerp(skin,ink,.30f), new(0,1.485f,.096f), new(.038f,.006f,.008f));
            for (int side = -1; side <= 1; side += 2)
            {
                Orb("Ear", torso, skin, new(side*.090f,1.532f,.018f), new(.029f,.046f,.031f));
                Orb("Eye", torso, ink, new(side*.036f,1.55f,.098f), new(.026f,.008f,.009f));
                Orb("Brow", torso, ink, new(side*.036f,1.568f,.096f), new(.034f,.007f,.009f));
            }
            Hair(torso, look, ink, sash);
            if (look.role == "師父")
                Orb("White beard", torso, ink, new(0,1.448f,.077f), new(.115f,.152f,.080f));

            var limbs = body.AddComponent<CharacterMotion>();
            limbs.SetBody(torso, skirt);
            for (int side = -1; side <= 1; side += 2)
            {
                var arm = Joint(root, new(side*.18f,1.31f,0));
                arm.localRotation = Quaternion.Euler(0,0,side*13);
                Revolve("Wide sleeve", arm, robe, .025f,
                    (-.415f,.10f,.082f),(-.33f,.107f,.092f),(-.17f,.084f,.076f),(0,.071f,.065f));
                Revolve("Sleeve facing", arm, lining, 0,
                    (-.424f,.099f,.080f),(-.396f,.102f,.084f));
                Orb("Hand", arm, skin, new(0,-.443f,.022f), new(.072f,.102f,.055f));
                var leg = Joint(root, new(side*.096f,.73f,0));
                Orb("Trousers", leg, sash, new(0,-.35f,0), new(.13f,.67f,.13f));
                Orb("Cloth boot", leg, ink, new(0,-.652f,.045f), new(.126f,.150f,.244f));
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

        private static void Orb(string name, Transform parent, Color color, Vector3 point, Vector3 size)
        {
            var part = Piece(name,parent,sphere,color);
            part.localPosition = point;
            part.localScale = size;
        }

        private static void Strip(string name, Transform parent, Color color, Vector3 a, Vector3 b, float width)
        {
            var part = Piece(name,parent,cube,color);
            part.localPosition = (a+b)*.5f + Vector3.forward*.014f;
            part.localRotation = Quaternion.FromToRotation(Vector3.up,b-a);
            part.localScale = new Vector3(width,Vector3.Distance(a,b),.022f);
        }

        private static Transform Revolve(string name, Transform parent, Color color, float folds,
            params (float y, float x, float z)[] rings)
        {
            if (!meshes.TryGetValue(name, out var mesh))
            {
                const int sides = 32;
                var points = new Vector3[rings.Length * sides];
                var indices = new int[(rings.Length-1)*sides*6];
                for (int row=0; row<rings.Length; row++)
                for (int side=0; side<sides; side++)
                {
                    float angle = side * 2*Mathf.PI/sides;
                    float fold = 1 + folds*Mathf.Cos(angle*8);
                    var ring = rings[row];
                    int index = row*sides+side;
                    points[index] = new Vector3(Mathf.Cos(angle)*ring.x*fold,ring.y,Mathf.Sin(angle)*ring.z*fold);
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
                mesh = new Mesh { name = name, vertices = points, triangles = indices };
                mesh.RecalculateNormals();
                meshes.Add(name,mesh);
            }
            return Piece(name,parent,mesh,color);
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
