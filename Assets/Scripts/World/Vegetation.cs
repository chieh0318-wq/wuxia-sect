using System.Collections.Generic;
using UnityEngine;

namespace Wuxia
{
    internal static class Vegetation
    {
        internal static void Build(Courtyard world, Transform root, Vector3 position, float size, bool broadleaf)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var atlas = broadleaf ? Rect.MinMaxRect(.55f, .344f, .833f, .956f) :
                Rect.MinMaxRect(.649f, .61f, .947f, .966f);
            const int tiers = 7;
            float seed = position.x * .42f + position.z * .16f;
            for (int tier = 0; tier < tiers; tier++)
            {
                float fraction = tier / (float)(tiers - 1);
                float height = Mathf.Lerp(broadleaf ? 1.75f : 1.35f, 4.65f, fraction);
                float reach = broadleaf ? Mathf.Lerp(.38f, 1.6f, Mathf.Sin(fraction * Mathf.PI)) :
                    Mathf.Lerp(1.87f, .16f, fraction);
                int branchCount = broadleaf ? 7 : 6;
                for (int branch = 0; branch < branchCount; branch++)
                {
                    float angle = seed + tier * 1.17f + branch * Mathf.PI * 2 / branchCount;
                    var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    var across = new Vector3(-radial.z, 0, radial.x);
                    Vector3 origin = Vector3.up * (height + Mathf.Sin(angle * 5) * .13f);
                    Vector3 tip = radial * reach + Vector3.up * (broadleaf ? .70f : .23f);
                    if (tier < tiers - 2 && branch % 2 == 0)
                    {
                        Vector3 trunkTip = tip * .75f;
                        var limb = world.Part("Tree limb", PrimitiveType.Cylinder, root,
                            position + (origin + trunkTip * .5f) * size,
                            new Vector3(.055f, trunkTip.magnitude * .5f, .055f) * size, Courtyard.Tint("#584A3C"));
                        limb.transform.localRotation = Quaternion.FromToRotation(Vector3.up, trunkTip);
                    }
                    AddSpray(origin + tip * .10f, across, tip * 1.12f, reach * .53f + .21f);
                    AddSpray(origin + tip * .34f, (across * .65f + Vector3.up).normalized,
                        tip * .92f + Vector3.up * .23f, reach * .42f + .16f);
                    if (broadleaf)
                        AddSpray(origin + tip * .22f, (across - Vector3.up).normalized,
                            tip * .85f - Vector3.up * .28f, reach * .36f + .17f);
                }
            }
            var mesh = new Mesh { name = "Layered foliage sprays" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            var normals = new Vector3[vertices.Count];
            System.Array.Fill(normals, Vector3.up);
            mesh.normals = normals;
            var crown = new GameObject(broadleaf ? "Broadleaf canopy" : "Fir canopy", typeof(MeshFilter), typeof(MeshRenderer));
            crown.transform.SetParent(root, false);
            crown.transform.localPosition = position;
            crown.transform.localScale = Vector3.one * size;
            crown.GetComponent<MeshFilter>().sharedMesh = mesh;
            world.Paint(crown.GetComponent<MeshRenderer>(), broadleaf ? "Broadleaf" : "Fir", Courtyard.Tint(broadleaf ? "#A6C1AB" : "#9DBAB5"));

            void AddSpray(Vector3 start, Vector3 across, Vector3 extension, float halfWidth)
            {
                int index = vertices.Count;
                vertices.Add(start - across * halfWidth);
                vertices.Add(start + across * halfWidth);
                vertices.Add(start - across * halfWidth + extension);
                vertices.Add(start + across * halfWidth + extension);
                uvs.Add(new Vector2(atlas.xMin, atlas.yMin));
                uvs.Add(new Vector2(atlas.xMax, atlas.yMin));
                uvs.Add(new Vector2(atlas.xMin, atlas.yMax));
                uvs.Add(new Vector2(atlas.xMax, atlas.yMax));
                triangles.Add(index);
                triangles.Add(index + 2);
                triangles.Add(index + 1);
                triangles.Add(index + 2);
                triangles.Add(index + 3);
                triangles.Add(index + 1);
            }
        }
    }
}
