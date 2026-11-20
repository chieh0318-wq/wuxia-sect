using System.Collections.Generic;
using UnityEngine;

namespace Wuxia
{
    public sealed class Courtyard
    {
        private readonly Transform root;
        private readonly SectUI ui;
        private readonly SlotData[] slots;
        private readonly Renderer[] plots;
        private readonly Dictionary<Collider, int> slotTargets = new();
        private readonly Dictionary<Collider, DiscipleData> discipleTargets = new();
        private readonly Dictionary<string, Material> surfaces = new();
        private readonly List<Vector3> entrances = new();
        private readonly Dictionary<string, (Transform root, float height)> landmarks = new();
        private readonly TangBuildingFactory halls;
        private readonly MaterialPropertyBlock paint = new();

        public Courtyard(Transform parent, SectUI ui, SlotData[] slots)
        {
            root = new GameObject("Courtyard").transform;
            root.SetParent(parent, false);
            this.ui = ui;
            this.slots = slots;
            plots = new Renderer[slots.Length];
            halls = new TangBuildingFactory(this);
            foreach (string name in new[] { "Solid", "Tile", "Paving", "Earth", "Plaster", "Timber", "Grass", "Fir", "Broadleaf" })
                surfaces.Add(name, Resources.Load<Material>("Materials/" + name));

            BuildMeadow();
            Part("Courtyard earth", PrimitiveType.Plane, root, new(0, -.055f, 0), new(4.2f, 1, 4.2f),
                Tint("#ACA693"), "Earth", new(19, 19));
            foreach (float lane in new[] { -14f, -4f, 6f, 14f })
                Path(0, lane, 40, 1.4f);
            Path(5, 0, 2.8f, 48);
            Path(0, 9.5f, 12, 5.1f);

            Wall(0, 21, 42, .55f);
            Wall(-21, 0, .55f, 42);
            Wall(21, 0, .55f, 42);
            Wall(-9.7f, -21, 22.6f, .55f);
            Wall(17.1f, -21, 7.8f, .55f);
            var gate = new GameObject("South mountain gate").transform;
            gate.SetParent(root, false);
            gate.localPosition = new Vector3(5, 0, -21);
            halls.Build(gate, "gate", 5.35f, 2.25f, 2.6f);

            for (int index = 0; index < slots.Length; index++)
            {
                float x = slots[index].x, z = slots[index].z;
                var outline = new GameObject("Building plot").transform;
                outline.SetParent(root, false);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Foundation edge", PrimitiveType.Cube, outline, new(x + side * 3.1f, .045f, z),
                        new(.17f, .09f, 5.8f), Tint("#8C8573"), "Paving");
                    Part("Foundation edge", PrimitiveType.Cube, outline, new(x, .045f, z + side * 2.9f),
                        new(6.3f, .09f, .17f), Tint("#8C8573"), "Paving");
                }
                var tile = Part("Plot selection", PrimitiveType.Cube, outline, new(x, .011f, z),
                    new(6.1f, .015f, 5.65f), Tint("#688752"));
                var collider = tile.GetComponent<Collider>();
                collider.enabled = true;
                slotTargets.Add(collider, index);
                plots[index] = tile.GetComponent<Renderer>();
                plots[index].enabled = false;
            }

            var random = new System.Random(18);
            for (int i = 0; i < 58; i++)
            {
                float x = (float)random.NextDouble() * 97 - 48;
                float z = (float)random.NextDouble() * 89 - 36;
                if (Mathf.Abs(x) < 23 && Mathf.Abs(z) < 23 || Mathf.Abs(x - 5) < 4 && z < -21)
                    continue;
                Tree(x, z, Mathf.Lerp(.65f, 1.6f, (float)random.NextDouble()), i % 3 == 0);
                if (i % 4 == 0)
                    Part("Weathered rock", PrimitiveType.Sphere, root,
                        new(x + 2, MeadowHeight(x + 2, z - 1) + .12f, z - 1),
                        new(1.45f, .76f, 1.1f), Tint("#777B66"), "Paving");
            }
            Tree(-18.5f, 17.5f, 1.25f, true);
            Tree(18, 18, 1.15f, false);
            Tree(-18.4f, -18, 1.15f, false);

            foreach (float x in new[] { 2.7f, 7.3f })
            {
                Part("Lantern pedestal", PrimitiveType.Cube, root, new(x, .35f, -17), new(.38f, .7f, .38f), Tint("#8A8674"));
                Part("Stone lantern", PrimitiveType.Cube, root, new(x, .77f, -17), new(.55f, .23f, .55f), Tint("#BAB19E"));
                Part("Lantern cap", PrimitiveType.Cube, root, new(x, .94f, -17), new(.65f, .12f, .65f), Tint("#8A8674"));
                Part("Banner pole", PrimitiveType.Cylinder, root, new(x, 1.48f, -20), new(.08f, 1.48f, .08f), Tint("#44382D"));
                Part("Sect banner", PrimitiveType.Cube, root, new(x + .28f, 2.18f, -20), new(.6f, .97f, .045f), Tint("#82463C"));
            }
            Part("Water vat", PrimitiveType.Cylinder, root, new(8.5f, .35f, -16.6f), new(.91f, .35f, .91f), Tint("#554A42"), "Timber");
            Part("Water surface", PrimitiveType.Cylinder, root, new(8.5f, .705f, -16.6f), new(.72f, .012f, .72f), Tint("#56675E"));
            StaticBatchingUtility.Combine(root.gameObject);
        }

        public bool TryGetSlot(Collider target, out int index) => slotTargets.TryGetValue(target, out index);
        public bool TryGetDisciple(Collider target, out DiscipleData disciple) => discipleTargets.TryGetValue(target, out disciple);
        public bool TryGetLandmark(string id, out (Transform root, float height) landmark) => landmarks.TryGetValue(id, out landmark);
        public void HighlightPlots(bool active)
        {
            foreach (var plot in plots)
                plot.enabled = active;
        }

        public void PlaceBuilding(int index, BuildingData data)
        {
            plots[index].transform.parent.gameObject.SetActive(false);
            var building = new GameObject(data.name).transform;
            building.SetParent(root, false);
            building.localPosition = new Vector3(slots[index].x, 0, slots[index].z);
            float w = data.size[0], h = data.size[1], d = data.size[2];
            float labelHeight;
            if (data.id == "yard")
            {
                Part("Packed earth training ground", PrimitiveType.Cube, building, new(0, .035f, 0),
                    new(w, .14f, d), Tint("#9F9580"), "Earth", new(3, 3));
                halls.Rack(building, -w * .30f, d * .29f);
                for (int i = 0; i < 3; i++)
                {
                    float x = -.25f + i * 1.05f;
                    Part("Wooden dummy", PrimitiveType.Cylinder, building, new(x, .78f, d * .24f),
                        new(.28f, .72f, .28f), Tint("#79583C"), "Timber");
                    Part("Training dummy arm", PrimitiveType.Cube, building, new(x, 1.08f, d * .24f),
                        new(.66f, .14f, .16f), Tint("#79583C"), "Timber");
                }
                labelHeight = 1.6f;
            }
            else
                labelHeight = halls.Build(building, data.id, w, h, d);
            foreach (var collider in building.GetComponentsInChildren<Collider>())
            {
                collider.enabled = true;
                slotTargets.Add(collider, index);
            }
            landmarks[data.id] = (building, labelHeight);
            entrances.Add(building.position + Vector3.back * (d / 2 + 1.25f));
            ui.AddWorldLabel(building, Vector3.up * labelHeight, data.name);
            StaticBatchingUtility.Combine(building.gameObject);
        }

        public void SpawnDisciple(DiscipleData data)
        {
            var person = new GameObject(data.name).transform;
            person.SetParent(root, false);
            int order = discipleTargets.Count;
            person.position = entrances.Count == 0 ? new Vector3(0, 0, -16) :
                entrances[order % entrances.Count] + Vector3.right * (order % 2 == 0 ? -.45f : .45f);
            var body = new GameObject("Robed figure").transform;
            body.SetParent(person, false);
            Color robe = Color.Lerp(Tint(data.robeColor), Tint("#777369"), .37f);
            Color skin = Tint("#C39572"), hair = Tint("#302A25");
            Part("Upper robe", PrimitiveType.Capsule, body, new(0, .99f, 0), new(.43f, .29f, .30f), robe);
            var skirt = new GameObject("Flared robe", typeof(MeshFilter), typeof(MeshRenderer));
            skirt.transform.SetParent(body, false);
            skirt.GetComponent<MeshFilter>().sharedMesh = SkirtMesh();
            Paint(skirt.GetComponent<Renderer>(), "Solid", robe);
            Part("Sash", PrimitiveType.Cylinder, body, new(0, .77f, 0), new(.35f, .047f, .31f), Tint("#4F4038"));
            Part("Head", PrimitiveType.Sphere, body, new(0, 1.34f, .018f), new(.26f, .32f, .25f), skin);
            Part("Hair", PrimitiveType.Sphere, body, new(0, 1.42f, -.033f), new(.28f, .22f, .24f), hair);
            Part("Topknot", PrimitiveType.Sphere, body, new(0, 1.57f, -.047f), new(.13f, .14f, .13f), hair);
            for (int side = -1; side <= 1; side += 2)
            {
                var sleeve = Part("Sleeve", PrimitiveType.Capsule, body, new(side * .245f, .96f, 0),
                    new(.19f, .26f, .23f), robe);
                sleeve.transform.localRotation = Quaternion.Euler(0, 0, side * 15);
                Part("Hand", PrimitiveType.Sphere, body, new(side * .28f, .745f, 0), new(.09f, .13f, .10f), skin);
                Part("Cloth shoe", PrimitiveType.Sphere, body, new(side * .11f, .08f, .06f), new(.14f, .13f, .27f), hair);
            }
            var hit = person.gameObject.AddComponent<CapsuleCollider>();
            hit.center = Vector3.up * .8f;
            hit.radius = .37f;
            hit.height = 1.65f;
            discipleTargets.Add(hit, data);
            person.gameObject.AddComponent<DiscipleWalker>().Initialize(entrances, body);
            ui.AddWorldLabel(person, Vector3.up * 1.82f, data.name);
        }

        private Mesh skirtMesh;
        private Mesh SkirtMesh()
        {
            if (skirtMesh != null)
                return skirtMesh;
            const int sides = 16;
            var vertices = new Vector3[sides * 2];
            var triangles = new int[sides * 6];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * 2 * Mathf.PI / sides;
                var outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                vertices[i] = outward * .29f + Vector3.up * .13f;
                vertices[i + sides] = outward * .17f + Vector3.up * .82f;
                int next = (i + 1) % sides, index = i * 6;
                triangles[index] = i;
                triangles[index + 1] = i + sides;
                triangles[index + 2] = next;
                triangles[index + 3] = next;
                triangles[index + 4] = i + sides;
                triangles[index + 5] = next + sides;
            }
            skirtMesh = new Mesh { name = "Flared cloth", vertices = vertices, triangles = triangles };
            skirtMesh.RecalculateNormals();
            return skirtMesh;
        }

        private void Path(float x, float z, float w, float d)
        {
            Part("Stone paving edge", PrimitiveType.Cube, root, new(x, -.009f, z), new(w + .19f, .06f, d + .19f), Tint("#7D796B"));
            Part("Stone paving", PrimitiveType.Cube, root, new(x, .018f, z), new(w, .06f, d),
                Tint("#D0C6B1"), "Paving", new(w / 2.2f, d / 2.2f));
        }

        private void Wall(float x, float z, float w, float d)
        {
            var wall = new GameObject("Plastered perimeter wall").transform;
            wall.SetParent(root, false);
            wall.localPosition = new(x, 0, z);
            Part("Stone footing", PrimitiveType.Cube, wall, new(0, .18f, 0), new(w + .13f, .36f, d + .13f), Tint("#888475"), "Paving");
            Part("Whitewashed wall", PrimitiveType.Cube, wall, new(0, .96f, 0), new(w, 1.56f, d),
                Tint("#C4B49A"), "Plaster", new(Mathf.Max(w, d) / 2f, 1));
            var coping = new GameObject("Tile coping").transform;
            coping.SetParent(wall, false);
            if (d > w)
                coping.localRotation = Quaternion.Euler(0, 90, 0);
            halls.Roof(coping, Mathf.Max(w, d) + .24f, Mathf.Min(w, d) + .24f, 1.73f, .18f, true);
        }

        private void BuildMeadow()
        {
            const int cells = 72;
            int side = cells + 1;
            var points = new Vector3[side * side];
            var uv = new Vector2[points.Length];
            var faces = new int[cells * cells * 6];
            int index = 0;
            for (int z = 0; z < side; z++)
            for (int x = 0; x < side; x++)
            {
                float worldX = (x - cells * .5f) * 2.5f, worldZ = (z - cells * .5f) * 2.5f;
                int p = x + z * side;
                points[p] = new Vector3(worldX, MeadowHeight(worldX, worldZ), worldZ);
                uv[p] = new Vector2(worldX, worldZ) / 2.4f;
                if (x == cells || z == cells)
                    continue;
                faces[index++] = p;
                faces[index++] = p + side;
                faces[index++] = p + 1;
                faces[index++] = p + 1;
                faces[index++] = p + side;
                faces[index++] = p + side + 1;
            }
            var mesh = new Mesh { name = "Undulating grassland", vertices = points, uv = uv, triangles = faces };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            var meadow = new GameObject("Outer meadow", typeof(MeshFilter), typeof(MeshRenderer));
            meadow.transform.SetParent(root, false);
            meadow.GetComponent<MeshFilter>().sharedMesh = mesh;
            Paint(meadow.GetComponent<MeshRenderer>(), "Grass", Tint("#ABC6B3"));
        }

        private static float MeadowHeight(float x, float z)
        {
            float margin = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float outside = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(22, 46, margin));
            if (z < -21)
                outside *= Mathf.InverseLerp(2, 8, Mathf.Abs(x - 5));
            return -.09f + outside * (Mathf.PerlinNoise(x * .075f + 12, z * .075f + 17) - .37f) * 2.1f;
        }

        private void Tree(float x, float z, float size, bool broadleaf)
        {
            float baseHeight = MeadowHeight(x, z);
            Part("Tree trunk", PrimitiveType.Cylinder, root, new(x, baseHeight + 1.64f * size, z),
                new Vector3(.27f, 1.65f, .30f) * size, Tint("#554638"), "Timber");
            Vegetation.Build(this, root, new Vector3(x, baseHeight, z), size, broadleaf);
        }

        internal GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 position,
            Vector3 size, Color color, string surface = "Solid", Vector2 tiling = default)
        {
            var piece = GameObject.CreatePrimitive(type);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = position;
            piece.transform.localScale = size;
            Paint(piece.GetComponent<Renderer>(), surface, color, tiling);
            piece.GetComponent<Collider>().enabled = false;
            return piece;
        }

        internal void Paint(Renderer renderer, string surface, Color color, Vector2 tiling = default)
        {
            renderer.sharedMaterial = surfaces[surface];
            paint.Clear();
            paint.SetColor("_BaseColor", color);
            if (tiling != default)
            {
                paint.SetVector("_BaseMap_ST", new Vector4(tiling.x, tiling.y, 0, 0));
                paint.SetVector("_BumpMap_ST", new Vector4(tiling.x, tiling.y, 0, 0));
            }
            renderer.SetPropertyBlock(paint);
        }

        internal static Color Tint(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var color) ? color : new Color(.55f, .52f, .44f);
    }
}
