using UnityEngine;

namespace Wuxia
{
    internal sealed class TangBuildingFactory
    {
        private readonly Courtyard world;
        private static readonly Color Stone = Courtyard.Tint("#B4AA97");
        private static readonly Color Red = Courtyard.Tint("#8E3B2E");
        private static readonly Color Wood = Courtyard.Tint("#645044");
        private static readonly Color Whitewash = Courtyard.Tint("#EBDFC5");
        private static readonly Color Clay = Courtyard.Tint("#656765");

        internal TangBuildingFactory(Courtyard world) => this.world = world;

        internal float Build(Transform root, string style, float width, float height, float depth)
        {
            Box(root, "Stone platform", 0, .18f, 0, width + .65f, .36f, depth + .55f, Stone, "Paving");
            Box(root, "Platform cornice", 0, .36f, 0, width + .76f, .13f, depth + .65f, Stone);
            foreach (int step in new[] { 0, 1, 2 })
                Box(root, "Stone stair", 0, .06f * (step + 1), -depth / 2 - .81f + step * .21f,
                    width * .44f, .12f * (step + 1), .42f, Stone, "Paving");

            float eave = height + .43f;
            bool open = style == "teahouse" || style == "gate" || style == "yard";
            int bays = width >= 6 ? 5 : 3;
            Storey(root, width, depth, .43f, height, bays, open);
            bool upper = style == "hall" || style == "library";
            bool gabled = style == "quarters" || style == "dorm";
            float rise = Mathf.Clamp(depth * .32f, .85f, 2.25f);

            Roof(root, width + 1.55f, depth + 1.48f, eave, upper ? rise * .65f : rise, gabled);
            if (upper)
            {
                eave += rise * .45f + height * .46f;
                float upperFloor = eave - height * .58f;
                Storey(root, width * .67f, depth * .56f, upperFloor, height * .58f, 3, false);
                if (style == "library")
                {
                    Box(root, "Pavilion balcony", 0, upperFloor + .16f, -depth * .31f,
                        width * .74f, .13f, .64f, Wood, "Timber");
                    Box(root, "Pavilion balustrade", 0, upperFloor + .55f, -depth * .36f,
                        width * .74f, .11f, .14f, Red);
                    for (int post = -3; post <= 3; post++)
                        Box(root, "Balustrade post", post * width * .116f, upperFloor + .34f,
                            -depth * .36f, .08f, .4f, .11f, Wood);
                }
                Roof(root, width * .75f + .8f, depth * .67f + .65f, eave, rise * .83f, false);
                rise *= .83f;
            }

            if (style == "kitchen")
            {
                Box(root, "Masonry chimney", width * .29f, eave + .5f, depth * .19f, .65f, 1.75f, .62f, Stone, "Paving");
                Box(root, "Chimney cap", width * .29f, eave + 1.4f, depth * .19f, .83f, .14f, .81f, Clay);
            }
            if (style == "infirmary")
            {
                float x = width * .31f, z = -depth / 2 - .76f;
                Rack(root, x, z);
                for (int bundle = -2; bundle <= 2; bundle++)
                    Part(root, "Drying herbs", PrimitiveType.Sphere, x + bundle * .21f, .94f, z,
                        .14f, .32f, .14f, Courtyard.Tint("#777D4A"));
            }
            if (style == "smithy")
            {
                float z = -depth / 2 - .78f;
                Rack(root, width * .31f, z);
                Box(root, "Anvil stump", -width * .32f, .27f, z, .47f, .54f, .44f, Wood, "Timber");
                Box(root, "Anvil", -width * .32f, .65f, z, .69f, .23f, .32f, Clay);
            }
            return eave + rise + .35f;
        }

        private void Storey(Transform root, float w, float d, float floor, float h, int bays, bool open)
        {
            if (!open)
            {
                Box(root, "Plastered brick walls", 0, floor + h * .47f, .12f, w * .92f, h * .94f, d * .85f, Whitewash, "Plaster");
                Box(root, "Stone skirting", 0, floor + .17f, .11f, w * .94f, .34f, d * .87f, Stone, "Paving");
            }
            foreach (int side in new[] { -1, 1 })
            {
                float z = side * d * .48f;
                Box(root, "Timber architrave", 0, floor + h * .93f, z, w + .2f, .22f, .25f, Red, "Timber");
                for (int i = 0; i <= bays; i++)
                {
                    float x = w * (i / (float)bays - .5f);
                    Part(root, "Round timber column", PrimitiveType.Cylinder, x, floor + h / 2, z, .28f, h / 2, .28f, Red);
                    Part(root, "Column stone plinth", PrimitiveType.Cylinder, x, floor + .085f, z, .42f, .085f, .42f, Stone);
                    Box(root, "Bracket block", x, floor + h - .29f, z, .43f, .16f, .38f, Wood);
                    Box(root, "Bracket arm", x, floor + h - .12f, z, .28f, .15f, .63f, Red, "Timber");
                    if (open || side > 0 || i == bays)
                        continue;
                    float centre = x + w / bays / 2;
                    float panelWidth = w / bays * .67f;
                    Box(root, "Lattice panel", centre, floor + h * .44f, z + .16f, panelWidth, h * .78f, .12f, Wood, "Timber");
                    for (int bar = -2; bar <= 2; bar++)
                        Box(root, "Lattice mullion", centre + panelWidth * bar / 6, floor + h * .57f,
                            z + .08f, .046f, h * .38f, .05f, Red);
                    for (int bar = 0; bar < 4; bar++)
                        Box(root, "Lattice rail", centre, floor + h * (.40f + bar * .105f), z + .075f,
                            panelWidth, .053f, .05f, Red);
                }
            }
            Box(root, "Eave soffit", 0, floor + h - .092f, 0, w + .72f, .12f, d + .72f, Wood, "Timber");
        }

        internal void Roof(Transform root, float width, float depth, float eave, float rise, bool gabled)
        {
            const int columns = 24;
            const int rows = 10;
            float halfWidth = width / 2;
            float halfDepth = depth / 2;
            float ridgeHalf = halfWidth * (gabled ? .95f : .52f);
            var vertices = new Vector3[4 * (columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[4 * columns * rows * 6];
            int vertex = 0, index = 0;

            // Walk clockwise around the eaves and interpolate towards the ridge.
            Vector2[] corners = { new(-halfWidth, -halfDepth), new(-halfWidth, halfDepth),
                new(halfWidth, halfDepth), new(halfWidth, -halfDepth) };
            for (int side = 0; side < 4; side++)
            {
                Vector2 a = corners[side], b = corners[(side + 1) % 4];
                int start = vertex;
                float length = Vector2.Distance(a, b);
                float run = Mathf.Abs(a.y - b.y) < .01f ? halfDepth : halfWidth - ridgeHalf;
                for (int row = 0; row <= rows; row++)
                {
                    float slope = row / (float)rows;
                    for (int col = 0; col <= columns; col++)
                    {
                        float along = col / (float)columns;
                        Vector2 edge = Vector2.Lerp(a, b, along);
                        float ridge = Mathf.Lerp(Mathf.Sign(a.x), Mathf.Sign(b.x), along) * ridgeHalf;
                        float corner = Mathf.Pow(Mathf.Abs(along * 2 - 1), 5) * Mathf.Pow(1 - slope, 3);
                        vertices[vertex] = new Vector3(Mathf.Lerp(edge.x, ridge, slope),
                            eave + rise * (slope * slope + corner * .25f), edge.y * (1 - slope));
                        uv[vertex++] = new Vector2(along * length / 1.35f, slope * Mathf.Sqrt(run * run + rise * rise) / 1.35f);
                        if (col == columns || row == rows)
                            continue;
                        int p = start + row * (columns + 1) + col;
                        triangles[index++] = p;
                        triangles[index++] = p + 1;
                        triangles[index++] = p + columns + 1;
                        triangles[index++] = p + 1;
                        triangles[index++] = p + columns + 2;
                        triangles[index++] = p + columns + 1;
                    }
                }
            }

            var mesh = new Mesh { name = "Concave tiled roof", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            var shell = new GameObject("Tiled roof", typeof(MeshFilter), typeof(MeshRenderer));
            shell.transform.SetParent(root, false);
            shell.GetComponent<MeshFilter>().sharedMesh = mesh;
            world.Paint(shell.GetComponent<Renderer>(), "Tile", Clay);
            var hit = shell.AddComponent<MeshCollider>();
            hit.sharedMesh = mesh;
            hit.enabled = false;
            Box(root, "Ridge cap", 0, eave + rise, 0, ridgeHalf * 2 + .34f, .19f, .24f, Clay);
            foreach (int end in new[] { -1, 1 })
            {
                var finial = Part(root, "Ridge finial", PrimitiveType.Sphere, end * ridgeHalf, eave + rise + .13f,
                    0, .43f, .53f, .25f, Clay);
                finial.transform.localRotation = Quaternion.Euler(0, 0, -end * 31);
            }
        }

        internal void Rack(Transform root, float x, float z)
        {
            foreach (int side in new[] { -1, 1 })
                Box(root, "Rack upright", x + side * .58f, .59f, z, .10f, 1.18f, .22f, Wood);
            Box(root, "Rack shelf", x, .65f, z, 1.28f, .10f, .43f, Wood, "Timber");
            Box(root, "Rack crossbar", x, 1.09f, z, 1.28f, .09f, .16f, Wood);
        }

        private GameObject Box(Transform root, string name, float x, float y, float z,
            float w, float h, float d, Color color, string surface = "Solid") =>
            Part(root, name, PrimitiveType.Cube, x, y, z, w, h, d, color, surface);

        private GameObject Part(Transform root, string name, PrimitiveType type, float x, float y, float z,
            float w, float h, float d, Color color, string surface = "Solid") =>
            world.Part(name, type, root, new Vector3(x, y, z), new Vector3(w, h, d), color, surface);
    }
}
