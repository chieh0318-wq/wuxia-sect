using System.Collections.Generic;
using UnityEngine;

namespace Wuxia
{
    public sealed class Courtyard
    {
        private readonly Transform root;
        private readonly SectUI ui;
        private readonly SlotData[] slots;
        private readonly Renderer[] tiles;
        private readonly Dictionary<Collider, int> slotTargets = new Dictionary<Collider, int>();
        private readonly Dictionary<Collider, DiscipleData> discipleTargets = new Dictionary<Collider, DiscipleData>();
        private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        private readonly List<Vector3> entrances = new List<Vector3>();
        private Material primitiveMaterial;

        public Courtyard(Transform parent, SectUI ui, SlotData[] slots)
        {
            root = new GameObject("Courtyard").transform;
            root.SetParent(parent, false);
            this.ui = ui;
            this.slots = slots;
            tiles = new Renderer[slots.Length];

            var ground = Primitive("Ground", PrimitiveType.Plane, new Vector3(0, -0.05f, 0),
                new Vector3(8, 1, 8), Tint("#807557"), root);
            ground.GetComponent<Collider>().enabled = false;

            Color path = Tint("#BCA480");
            foreach (float lane in new[] { -14f, -4f, 6f, 14f })
                Cube("Stone path", new Vector3(0, -0.02f, lane), new Vector3(41, 0.08f, 1.35f), path, root);
            Cube("Main path", new Vector3(5, -0.02f, 0), new Vector3(1.5f, 0.08f, 42), path, root);

            Color wall = Tint("#807064");
            Wall(0, 21, 42, 0.8f, wall);
            Wall(-21, 0, 0.8f, 42, wall);
            Wall(21, 0, 0.8f, 42, wall);
            Wall(-11.5f, -21, 19, 0.8f, wall);
            Wall(11.5f, -21, 19, 0.8f, wall);
            for (int index = 0; index < slots.Length; index++)
            {
                var tile = Cube("Building plot", new Vector3(slots[index].x, 0.035f, slots[index].z),
                    new Vector3(6.4f, 0.07f, 5.8f), Tint("#A99A76"), root);
                var collider = tile.GetComponent<Collider>();
                collider.enabled = true;
                slotTargets.Add(collider, index);
                tiles[index] = tile.GetComponent<Renderer>();
            }
        }

        public bool TryGetSlot(Collider target, out int index) => slotTargets.TryGetValue(target, out index);

        public void DestroyMaterials()
        {
            foreach (var material in materials.Values)
                UnityEngine.Object.Destroy(material);
        }

        public bool TryGetDisciple(Collider target, out DiscipleData disciple) =>
            discipleTargets.TryGetValue(target, out disciple);

        public void HighlightPlots(bool active)
        {
            var material = MaterialFor(Tint(active ? "#76AF62" : "#A99A76"));
            foreach (var tile in tiles)
                tile.sharedMaterial = material;
        }

        public void PlaceBuilding(int index, BuildingData data)
        {
            var plot = tiles[index].gameObject;
            plot.SetActive(false);
            var building = new GameObject(data.name).transform;
            building.SetParent(root, false);
            building.position = new Vector3(slots[index].x, 0, slots[index].z);

            float width = data.size[0], height = data.size[1], depth = data.size[2];
            Color color = Tint(data.color);
            Vector3 position = building.position;
            var foundation = Cube("Foundation", position + Vector3.up * 0.13f,
                new Vector3(width + 0.35f, 0.26f, depth + 0.35f), Tint("#A69B87"), building);
            RegisterBuilding(foundation, index);
            var baseBlock = Cube("Walls", position + Vector3.up * (0.26f + height / 2),
                new Vector3(width, height, depth), color, building);
            RegisterBuilding(baseBlock, index);

            for (int tier = 0; tier < 3; tier++)
            {
                var roof = Cube("Roof", position + Vector3.up * (height + 0.39f + tier * 0.21f),
                    new Vector3(width + 0.8f - width * 0.12f * tier, 0.24f,
                        (depth + 0.8f) * (1f - tier * 0.24f)), Tint("#453D3A"), building);
                RegisterBuilding(roof, index);
            }

            var door = Cube("Door", position + new Vector3(0, height * 0.38f + 0.26f, -depth / 2 - 0.05f),
                new Vector3(width * 0.22f, height * 0.72f, 0.12f), Tint("#523D32"), building);
            RegisterBuilding(door, index);
            entrances.Add(position + Vector3.back * (depth / 2 + 1.2f));
            ui.AddWorldLabel(building, Vector3.up * (height + 1.15f), data.name);
        }

        public void SpawnDisciple(DiscipleData data)
        {
            var person = new GameObject(data.name);
            person.transform.SetParent(root, false);
            person.transform.position = entrances.Count == 0 ? new Vector3(0, 0, -16) :
                entrances[UnityEngine.Random.Range(0, entrances.Count)] +
                new Vector3(UnityEngine.Random.Range(-0.55f, 0.55f), 0, 0);

            var robe = Primitive("Robe", PrimitiveType.Capsule, person.transform.position + Vector3.up * 0.58f,
                new Vector3(0.57f, 0.56f, 0.57f), Tint(data.robeColor), person.transform);
            var head = Primitive("Head", PrimitiveType.Sphere, person.transform.position + Vector3.up * 1.22f,
                Vector3.one * 0.4f, Tint("#D5A57D"), person.transform);
            var hair = Primitive("Hair", PrimitiveType.Sphere,
                person.transform.position + new Vector3(0, 1.36f, -0.035f),
                new Vector3(0.33f, 0.2f, 0.34f), Tint("#45352C"), person.transform);
            foreach (var part in new[] { robe, head, hair })
            {
                var collider = part.GetComponent<Collider>();
                collider.enabled = true;
                discipleTargets.Add(collider, data);
            }

            person.AddComponent<DiscipleWalker>().Initialize(entrances);
            ui.AddWorldLabel(person.transform, Vector3.up * 1.72f, data.name);
        }

        private void RegisterBuilding(GameObject piece, int index)
        {
            var collider = piece.GetComponent<Collider>();
            collider.enabled = true;
            slotTargets.Add(collider, index);
        }

        private void Wall(float x, float z, float width, float depth, Color color)
        {
            Cube("Perimeter wall", new Vector3(x, 0.75f, z), new Vector3(width, 1.5f, depth), color, root);
            Cube("Wall coping", new Vector3(x, 1.53f, z),
                new Vector3(width + 0.18f, 0.16f, depth + 0.18f), Tint("#554841"), root);
        }

        private GameObject Cube(string name, Vector3 position, Vector3 size, Color color, Transform parent) =>
            Primitive(name, PrimitiveType.Cube, position, size, color, parent);

        private GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 size,
            Color color, Transform parent)
        {
            var piece = GameObject.CreatePrimitive(type);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.position = position;
            piece.transform.localScale = size;
            var renderer = piece.GetComponent<Renderer>();
            if (primitiveMaterial == null)
                primitiveMaterial = renderer.sharedMaterial;
            renderer.sharedMaterial = MaterialFor(color);
            piece.GetComponent<Collider>().enabled = false;
            return piece;
        }

        private Material MaterialFor(Color color)
        {
            if (!materials.TryGetValue(color, out var material))
            {
                material = new Material(primitiveMaterial) { color = color };
                materials.Add(color, material);
            }
            return material;
        }

        private static Color Tint(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var color) ? color : new Color(0.55f, 0.52f, 0.44f);
    }
}
