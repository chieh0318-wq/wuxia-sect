using UnityEngine;
using UnityEngine.Rendering;

namespace Wuxia
{
    public sealed partial class Courtyard
    {
        private Texture2D courtyardEarth;

        internal void Destroy()
        {
            foreach (var building in buildingLabels) ui.RemoveWorldLabel(building);
            foreach (var target in discipleTargets.Keys) ui.RemoveWorldLabel(target.transform.parent);
            HighlightPlots(false);
            root.gameObject.SetActive(false);
            Object.Destroy(root.gameObject);
            Object.Destroy(courtyardEarth);
        }

        private void BuildPackedEarth()
        {
            const int resolution = 1024;
            var earth = courtyardEarth = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true);
            earth.name = "Packed earth and gravel";
            earth.anisoLevel = 8;
            earth.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[resolution * resolution];
            var lanes = new[] { -14f, -4f, 6f, 14f };
            Color soil = Tint("#6D6760"), gravel = Tint("#8A8378"), damp = Tint("#4C4944");
            var random = new System.Random(72);
            for (int row = 0; row < resolution; row++)
            for (int column = 0; column < resolution; column++)
            {
                float x = yardWidth * (column + .5f) / resolution - yardWidth / 2;
                float z = 42 * (row + .5f) / resolution - 21;
                float patch = Mathf.PerlinNoise(x * .17f + 15, z * .17f + 23);
                float grain = (float)random.NextDouble();
                Color packed = soil * Mathf.Lerp(.93f, 1.08f, Mathf.PerlinNoise(x * 2.4f + 12, z * 2.4f + 31)) *
                    Mathf.Lerp(.87f, 1.09f, grain);
                Color pebbles = gravel * Mathf.Lerp(.8f, 1.22f, Mathf.Pow(grain, 2));
                float wear = Mathf.Abs(x - 5) - 1.4f;
                foreach (float lane in lanes)
                    wear = Mathf.Min(wear, Mathf.Abs(z - lane) - .7f);
                float pathWear = 1 - Mathf.Clamp01(wear / 1.5f + patch * .42f);
                Color color = Color.Lerp(packed, pebbles, Mathf.Clamp01(patch * .76f - .13f + pathWear * .37f));
                float edge = Mathf.Min(yardWidth / 2 - Mathf.Abs(x), 21 - Mathf.Abs(z));
                foreach (SlotData slot in slots)
                {
                    // The stone outlines on the three southern plots stay clear of foundation staining.
                    if (string.IsNullOrEmpty(slot.buildingId))
                        continue;
                    float footprint = Mathf.Max(Mathf.Abs(x - slot.x) - 2.85f, Mathf.Abs(z - slot.z) - 2.45f);
                    edge = Mathf.Min(edge, Mathf.Max(0, footprint) + .35f);
                }
                color = Color.Lerp(color, damp, (1 - Mathf.Clamp01(edge / 1.65f + patch * .34f)) * .72f);
                color.a = 1;
                pixels[column + row * resolution] = color;
            }
            earth.SetPixels32(pixels);
            earth.Apply(true, true);
            var plane = Part("Packed courtyard earth", PrimitiveType.Plane, root,
                new(0, -.055f, 0), new(yardWidth / 10, 1, 4.2f), Color.white);
            // Unity's plane UVs run in reverse world X and Z.
            PaintEarth(plane.GetComponent<Renderer>(), new Vector4(-1, -1, 1, 1), Color.white);
        }

        private void PaintEarth(Renderer surface, Vector4 uv, Color color)
        {
            paint.Clear();
            paint.SetColor("_BaseColor", color);
            paint.SetTexture("_BaseMap", courtyardEarth);
            paint.SetVector("_BaseMap_ST", uv);
            surface.SetPropertyBlock(paint);
        }

        private void BuildMountainRidges()
        {
            CreateRidge(130, -43, 45, 168, .023f, 59, "#9EAFB9");
            CreateRidge(86, -28, 31, 135, .039f, 21, "#82939D");
            CreateRidge(54, -13, 18, 105, .053f, 8, "#637783");
            CreateRidge(31, -3, 9, 76, .064f, 10, "#4E655D");
        }

        private void CreateRidge(float distance, float crestHeight, float relief,
            float halfWidth, float frequency, float seed, string tint)
        {
            const int columns = 152;
            const int rows = 14;
            var vertices = new Vector3[columns * rows];
            var uv = new Vector2[vertices.Length];
            var faces = new int[(columns - 1) * (rows - 1) * 6];
            int face = 0;
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                float x = Mathf.Lerp(-halfWidth, halfWidth, column / (float)(columns - 1));
                float slope = row / (float)(rows - 1);
                float broad = Mathf.PerlinNoise(x * frequency + seed, seed);
                float detail = Mathf.PerlinNoise(x * frequency * 3 + seed, slope * 2 + seed);
                float profile = (broad - .5f) * 1.75f + (detail - .5f) * .38f;
                float crest = Mathf.Sin(slope * Mathf.PI);
                float z = distance + (slope - .5f) * 20 + (broad - .5f) * 6;
                int index = column + row * columns;
                vertices[index] = new Vector3(x, crestHeight + (crest - 1) * 31 + profile * relief * crest, z);
                uv[index] = new Vector2(x / 7, (z + vertices[index].y * .8f) / 7);
                if (column == columns - 1 || row == rows - 1)
                    continue;
                faces[face++] = index;
                faces[face++] = index + columns;
                faces[face++] = index + 1;
                faces[face++] = index + 1;
                faces[face++] = index + columns;
                faces[face++] = index + columns + 1;
            }
            var mesh = new Mesh { name = "Weathered mountain ridge", vertices = vertices, uv = uv, triangles = faces };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            var ridge = new GameObject("Xiaoshan ridge", typeof(MeshFilter), typeof(MeshRenderer));
            ridge.transform.SetParent(root, false);
            ridge.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ridge.GetComponent<MeshRenderer>();
            Paint(renderer, "Plaster", Tint(tint));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private void PlantWallBases(System.Random random)
        {
            foreach (Vector2 grove in new[] { new Vector2(-26, 21), new(24, 25), new(-26, -17), new(28, -6), new(-15, 29) })
            {
                for (int i = 0; i < 7; i++)
                {
                    float x = grove.x + (float)random.NextDouble() * 7 - 3.5f;
                    float z = grove.y + (float)random.NextDouble() * 7 - 3.5f;
                    Tree(x, z, .65f + (float)random.NextDouble() * .78f, i % 3 == 0);
                }
            }

            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 23; i++)
            {
                float z = Mathf.Lerp(-19, 20, (float)i / 22) + (float)random.NextDouble() * .7f;
                float x = side * (21.8f + (float)random.NextDouble() * 1.2f);
                float y = MeadowHeight(x, z);
                float size = .22f + (float)random.NextDouble() * .62f;
                if (i % 3 == 0)
                    Vegetation.Build(this, root, new Vector3(x, y - size * .45f, z), size * .42f, true);
                for (int blade = 0; blade < 4; blade++)
                {
                    float angle = (float)random.NextDouble() * 360;
                    var grass = Part("Wall grass tuft", PrimitiveType.Cube, root,
                        new(x + (blade - 1.5f) * .075f, y + size * .3f, z),
                        new(.055f, size * .72f, .025f), Tint(blade % 2 == 0 ? "#727F69" : "#899582"));
                    grass.transform.localRotation = Quaternion.Euler(11 + blade * 7, angle, 0);
                    grass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }
    }
}
