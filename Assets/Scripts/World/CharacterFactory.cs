using UnityEngine;

namespace Wuxia
{
    public static class CharacterFactory
    {
        public static GameObject Build(CharacterLook look)
        {
            var body = new GameObject("Robed figure");
            Color robe = Parse(look.robeColor, "#46525E");
            Color sash = Parse(look.sashColor, "#8E3B2E");
            Color hair = Parse(look.hairColor, "#302A25");
            Color skin = Color.Lerp(Parse(null, "#A77354"), Parse(null, "#E0B398"), Mathf.Clamp01(look.skinTone));
            float width = Mathf.Lerp(.88f, 1.18f, Mathf.Clamp01(look.build));
            Part(body.transform, PrimitiveType.Capsule, new(0, 1.01f, 0), new(.44f * width, .31f, .32f), robe);
            Part(body.transform, PrimitiveType.Capsule, new(0, .48f, 0), new(.59f * width, .44f, .36f), robe);
            Part(body.transform, PrimitiveType.Cylinder, new(0, .78f, 0), new(.43f * width, .031f, .34f), sash);
            Part(body.transform, PrimitiveType.Sphere, new(0, 1.41f, .02f), new(.28f, .34f, .27f), skin);
            Part(body.transform, PrimitiveType.Sphere, new(0, 1.49f, -.04f), new(.30f, .24f, .28f), hair);
            var style = look.hairStyle ?? "topknot";
            if (style == "twinbuns")
            {
                Part(body.transform, PrimitiveType.Sphere, new(-.12f, 1.58f, -.05f), Vector3.one * .13f, hair);
                Part(body.transform, PrimitiveType.Sphere, new(.12f, 1.58f, -.05f), Vector3.one * .13f, hair);
            }
            else if (style is "ponytail" or "halfup")
                Part(body.transform, PrimitiveType.Capsule, new(0, 1.28f, -.16f), new(.14f, .24f, .12f), hair);
            else
                Part(body.transform, PrimitiveType.Sphere, new(0, 1.63f, -.05f), new(.14f, .14f, .14f), style == "crown" ? sash : hair);

            for (int side = -1; side <= 1; side += 2)
            {
                var sleeve = Part(body.transform, PrimitiveType.Capsule, new(side * .25f * width, 1.02f, 0), new(.20f, .27f, .24f), robe);
                sleeve.transform.localEulerAngles = new Vector3(0, 0, side * 15);
                Part(body.transform, PrimitiveType.Sphere, new(side * .29f * width, .79f, 0), new(.09f, .13f, .10f), skin);
                Part(body.transform, PrimitiveType.Sphere, new(side * .12f, .07f, .065f), new(.15f, .13f, .27f), hair);
            }
            return body;
        }

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

        private static Color Parse(string value, string fallback)
        {
            if (!ColorUtility.TryParseHtmlString(value, out var color))
                ColorUtility.TryParseHtmlString(fallback, out color);
            return color;
        }
    }
}
