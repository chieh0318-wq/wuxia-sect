using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wuxia
{
    public sealed partial class Courtyard
    {
        private readonly Transform root;
        private readonly SectUI ui;
        private readonly SlotData[] slots;
        private readonly Renderer[] plots;
        private readonly Renderer[][] plotFrames;
        private readonly HashSet<int> plotLabels = new();
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
            plotFrames = new Renderer[slots.Length][];
            halls = new TangBuildingFactory(this);
            foreach (string name in new[] { "Solid", "Tile", "Paving", "Earth", "Plaster", "Timber", "Grass", "Fir", "Broadleaf" })
                surfaces.Add(name, Resources.Load<Material>("Materials/" + name));

            BuildMeadow();
            BuildPackedEarth();
            BuildMountainRidges();
            foreach (float lane in new[] { -14f, -4f, 6f, 14f })
                Path(0, lane, 40, 1.4f);
            Path(5, 0, 2.8f, 48, true);
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
                    Part("Foundation edge", PrimitiveType.Cube, outline, new(x + side * 3.1f, -.021f, z),
                        new(.14f, .055f, 5.8f), Tint("#77736D"));
                    Part("Foundation edge", PrimitiveType.Cube, outline, new(x, -.021f, z + side * 2.9f),
                        new(6.3f, .055f, .14f), Tint("#77736D"));
                }
                // M6: a thick, bright jade pad with a glowing pale-green frame so empty plots cannot be missed.
                var tile = Part("Plot selection", PrimitiveType.Cube, outline, new(x, .06f, z),
                    new(6.1f, .06f, 5.65f), Tint("#5CD669"));
                var collider = tile.GetComponent<Collider>();
                collider.enabled = true;
                slotTargets.Add(collider, index);
                plots[index] = tile.GetComponent<Renderer>();
                plots[index].enabled = false;
                var frame = new List<Renderer>();
                for (int side = -1; side <= 1; side += 2)
                {
                    frame.Add(Part("Plot glow frame", PrimitiveType.Cube, outline, new(x + side * 3.0f, .12f, z),
                        new(.22f, .1f, 5.85f), Tint("#C8FFB4")).GetComponent<Renderer>());
                    frame.Add(Part("Plot glow frame", PrimitiveType.Cube, outline, new(x, .12f, z + side * 2.78f),
                        new(6.22f, .1f, .22f), Tint("#C8FFB4")).GetComponent<Renderer>());
                }
                foreach (var bar in frame) bar.enabled = false;
                plotFrames[index] = frame.ToArray();
            }

            var random = new System.Random(18);
            for (int i = 0; i < 76; i++)
            {
                float x = (float)random.NextDouble() * 97 - 48;
                float z = (float)random.NextDouble() * 89 - 36;
                if (Mathf.Abs(x) < 23 && Mathf.Abs(z) < 23 || Mathf.Abs(x - 5) < 4 && z < -21)
                    continue;
                Tree(x, z, Mathf.Lerp(.65f, 1.6f, (float)random.NextDouble()), i % 3 == 0);
                if (i % 4 == 0)
                    Part("Weathered rock", PrimitiveType.Sphere, root,
                        new(x + 2, MeadowHeight(x + 2, z - 1) + .12f, z - 1),
                        new(1.45f, .76f, 1.1f), Tint("#777A79"), "Paving");
            }
            PlantWallBases(random);
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
        /// <summary>
        /// Lights every empty plot (bright pad + glow frame + a floating 「可建空地（點擊建造）」 tag); built plots stay hidden.
        /// </summary>
        public void HighlightPlots(bool active)
        {
            for (int index = 0; index < plots.Length; index++)
            {
                var plot = plots[index];
                bool empty = plot.transform.parent.gameObject.activeSelf;
                bool lit = active && empty;
                plot.enabled = lit;
                foreach (var bar in plotFrames[index] ?? System.Array.Empty<Renderer>())
                    bar.enabled = lit;
                bool labelled = plotLabels.Contains(index);
                if (lit && !labelled)
                {
                    ui.AddWorldLabel(plot.transform, Vector3.up * 2.8f, "可建空地（點擊建造）");
                    plotLabels.Add(index);
                }
                else if (!lit && labelled)
                {
                    ui.RemoveWorldLabel(plot.transform);
                    plotLabels.Remove(index);
                }
            }
        }

        public bool PlotsHighlighted => plotLabels.Count > 0;

        public void PlaceBuilding(int index, BuildingData data)
        {
            if (plotLabels.Remove(index))
                ui.RemoveWorldLabel(plots[index].transform);
            plots[index].transform.parent.gameObject.SetActive(false);
            var building = new GameObject(data.name).transform;
            building.SetParent(root, false);
            building.localPosition = new Vector3(slots[index].x, 0, slots[index].z);
            float w = data.size[0], h = data.size[1], d = data.size[2];
            float labelHeight;
            if (data.id == "yard")
            {
                var trainingGround = Part("Packed earth training ground", PrimitiveType.Cube, building, new(0, .035f, 0),
                    new(w, .14f, d), Color.white);
                PaintEarth(trainingGround.GetComponent<Renderer>(), new Vector4(.17f, .16f, .37f, .21f),
                    Tint("#ACA49A"));
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
            var card = CharacterFactory.BuildStandee(data, person, out var hit);
            discipleTargets.Add(hit, data);
            person.gameObject.AddComponent<DiscipleWalker>().Initialize(entrances, card);
            ui.AddWorldLabel(person, CharacterFactory.StandeeLabelOffset, data.name, false);
        }

        internal void RemoveDisciple(DiscipleData disciple)
        {
            var hit = discipleTargets.FirstOrDefault(entry => entry.Value == disciple).Key;
            if (hit == null) return;
            discipleTargets.Remove(hit);
            var person = hit.transform.parent;
            ui.RemoveWorldLabel(person);
            person.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(person.gameObject);
        }

        private void Path(float x, float z, float w, float d, bool raised = false)
        {
            float height = raised ? .12f : .027f;
            Part("Stone curb", PrimitiveType.Cube, root, new(x, height - .064f, z),
                new(w + .25f, .13f, d + .25f), Tint("#99988E"));
            Part("Stone paving", PrimitiveType.Cube, root, new(x, height, z), new(w, .045f, d),
                Tint("#B9B8B1"), "Paving", new(w / 2.2f, d / 2.2f));
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
            Paint(meadow.GetComponent<MeshRenderer>(), "Grass", Tint("#8AA9AC"));
        }

        private static float MeadowHeight(float x, float z)
        {
            float margin = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float outside = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(22, 46, margin));
            if (z < -21)
                outside *= Mathf.InverseLerp(2, 8, Mathf.Abs(x - 5));
            float valley = 23 * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(23, 49, z))
                + Mathf.Max(0, z - 43) * 1.2f;
            return -.09f - valley + outside * (Mathf.PerlinNoise(x * .075f + 12, z * .075f + 17) - .45f) * 4.8f;
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
