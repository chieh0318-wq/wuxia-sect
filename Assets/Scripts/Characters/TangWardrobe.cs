using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wuxia
{
    internal sealed class TangWardrobe
    {
        private readonly Transform root;
        private readonly Animator rig;
        private readonly float height;
        private readonly float ground;
        private readonly Color robe, lining, sash;

        private readonly struct Ring
        {
            internal readonly Vector3 center, width, depth;
            internal readonly BoneWeight weights;
            internal readonly float folds;

            internal Ring(Vector3 center, Vector3 width, Vector3 depth, int a, int b, float blend, float folds)
            {
                this.center = center;
                this.width = width;
                this.depth = depth;
                this.folds = folds;
                weights = new BoneWeight { boneIndex0 = a, boneIndex1 = b, weight0 = 1 - blend, weight1 = blend };
            }

            internal Ring(Vector3 center, Vector3 width, Vector3 depth, BoneWeight weights, float folds)
            {
                this.center = center;
                this.width = width;
                this.depth = depth;
                this.weights = weights;
                this.folds = folds;
            }

            internal Vector3 Point(float angle, int row)
            {
                float ripple = 1 + folds * (.72f * Mathf.Cos(angle * 9 + .10f * row) +
                    .28f * Mathf.Sin(angle * 13 - .13f * row));
                return center + ripple * (width * Mathf.Cos(angle) + depth * Mathf.Sin(angle));
            }
        }

        internal TangWardrobe(Animator rig, float ground, float height, Color robe, Color lining, Color sash)
        {
            this.rig = rig;
            root = rig.transform;
            this.height = height;
            this.ground = ground;
            this.robe = robe;
            this.lining = lining;
            this.sash = sash;
        }

        private Transform Bone(HumanBodyBones name) => rig.GetBoneTransform(name);
        private Vector3 Point(Transform bone) => root.InverseTransformPoint(bone.position);

        internal void Dress(string style, bool female)
        {
            Transform hips = Bone(HumanBodyBones.Hips), spine = Bone(HumanBodyBones.Spine);
            Transform chest = Bone(HumanBodyBones.Chest) ?? spine, neck = Bone(HumanBodyBones.Neck) ?? Bone(HumanBodyBones.Head);
            Transform left = Bone(HumanBodyBones.LeftUpperLeg), right = Bone(HumanBodyBones.RightUpperLeg);
            Vector3 pelvis = Point(hips), collar = Point(neck);
            float shoulder = Mathf.Abs(Point(Bone(HumanBodyBones.LeftUpperArm)).x -
                Point(Bone(HumanBodyBones.RightUpperArm)).x) * .5f;
            float waist = Mathf.Lerp(pelvis.y, collar.y, style == "ruqun" ? .62f : .28f);
            float thigh = Mathf.Abs(Point(left).x - Point(right).x) * .5f;
            float width = Mathf.Max(shoulder * .88f, thigh + height * .056f);
            float depth = height * (female ? .064f : .075f);
            float z = pelvis.z + height * .008f;
            Transform[] torsoBones = { hips, spine, chest };
            Ring Torso(float y, float x, float d, int a, int b, float blend, float folds = .015f) =>
                new(new Vector3(0, y, z), Vector3.right * x, Vector3.forward * d, a, b, blend, folds);

            Ring[] jacket =
            {
                Torso(pelvis.y - height * .025f, width, depth, 0, 1, .12f),
                Torso(waist, width * .93f, depth * .96f, 0, 1, .62f),
                Torso(Mathf.Lerp(waist, collar.y, .54f), shoulder * 1.04f, depth * 1.13f, 1, 2, .7f),
                Torso(collar.y - height * .026f, shoulder * 1.06f, depth * .83f, 2, 2, 0),
                new Ring(collar + Vector3.up * height * .003f, Vector3.right * height * .041f,
                    Vector3.forward * height * .038f, 2, 2, 0, 0)
            };
            // The high waist of a ruqun hides the lower jacket.
            if (style == "ruqun") jacket[0] = Torso(waist - height * .038f, width, depth, 0, 1, .45f);
            Tube("Woven jacket", torsoBones, jacket, robe);

            float hem = ground + height * (style == "armor" ? .29f : .105f);
            float skirtWidth = width * (style == "armor" ? 1.75f : style == "ruqun" ? 2.18f : 1.91f);
            Ring[] skirt =
            {
                Torso(hem, skirtWidth * .95f, depth * 2.14f, 0, 1, 0, .15f),
                Torso(hem + height * .018f, skirtWidth * .961f, depth * 2.10f, 0, 1, 0, .15f),
                Torso(Mathf.Lerp(hem, waist,.29f), skirtWidth * .92f, depth * 1.90f, 0, 1, 0, .13f),
                Torso(Mathf.Lerp(hem, waist,.54f), skirtWidth * .80f, depth * 1.65f, 0, 1, 0, .11f),
                Torso(Mathf.Lerp(hem, waist,.82f), width * 1.21f, depth * 1.30f, 0, 1, .12f, .073f),
                Torso(waist + height * .005f, width * .94f, depth, 0, 1, .62f)
            };
            Tube("Pleated wrap skirt", new[] { hips, spine, chest, left, right }, skirt,
                style == "ruqun" ? lining : robe, true);
            Tube("Bound skirt hem", new[] { hips, spine, chest, left, right },
                new[] { skirt[0], skirt[1] }, Color.Lerp(robe, lining, .38f), true, .003f);
            Tube("Woven waist sash", torsoBones, new[]
            {
                Torso(waist - height * .024f, width * 1.13f, depth * 1.145f, 0, 1, .51f, 0),
                Torso(waist + height * .014f, width * 1.06f, depth * 1.117f, 0, 1, .65f, 0)
            }, sash);

            if (style == "yuanling" || style == "armor")
            {
                const float nearNeck = .81f;
                var shoulderRing = jacket[3];
                var neckRing = jacket[4];
                var baseRing = new Ring(Vector3.Lerp(shoulderRing.center, neckRing.center, nearNeck),
                    Vector3.Lerp(shoulderRing.width, neckRing.width, nearNeck),
                    Vector3.Lerp(shoulderRing.depth, neckRing.depth, nearNeck), 2, 2, 0, 0);
                Tube("Round collar facing", torsoBones, new[] { baseRing, neckRing }, lining, false, .009f);
            }
            else
            {
                Facing(jacket, torsoBones, "Crossed lapel", +1, .74f, lining);
                Facing(jacket, torsoBones, "Inner lapel", -1, .44f, Color.Lerp(lining, robe, .24f));
            }
            if (style == "armor")
            {
                Tube("Leather armor vest", torsoBones, new[] { jacket[1], jacket[2], jacket[3] },
                    Color.Lerp(sash, new Color(.13f,.15f,.16f), .65f), false, .014f);
                for (int row = 0; row < 3; row++)
                {
                    float t = .17f + row * .28f;
                    Ring ArmorBand(float position) => new(
                        Vector3.Lerp(jacket[1].center, jacket[2].center, position),
                        Vector3.Lerp(jacket[1].width, jacket[2].width, position),
                        Vector3.Lerp(jacket[1].depth, jacket[2].depth, position),
                        new BoneWeight { boneIndex0 = 0, boneIndex1 = 1, boneIndex2 = 2,
                            weight0 = .38f * (1 - position), weight1 = Mathf.Lerp(.62f, .30f, position),
                            weight2 = .70f * position }, .015f);
                    Tube("Leather vest binding", torsoBones,
                        new[] { ArmorBand(t), ArmorBand(t + .058f) }, Color.Lerp(sash, robe, .68f), false, .029f);
                }
            }
            Sleeve(true, style, chest);
            Sleeve(false, style, chest);
            if (style != "armor")
            {
                Trouser(true);
                Trouser(false);
            }
        }

        private void Facing(Ring[] jacket, Transform[] bones, string name, int side, float length, Color color)
        {
            const int steps = 24;
            var vertices = new List<Vector3>();
            var weights = new List<BoneWeight>();
            var indices = new List<int>();
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float sample = 4 - t * length * 4;
                int row = Mathf.Min(3, Mathf.FloorToInt(sample));
                float blend = sample - row;
                float center = -Mathf.PI * .5f + side * Mathf.Lerp(1.37f, side > 0 ? -.61f : -.08f, t);
                for (int edge = 0; edge < 2; edge++)
                {
                    float angle = center + (edge == 0 ? -.13f : .13f);
                    vertices.Add(Vector3.Lerp(jacket[row].Point(angle, row),
                        jacket[row + 1].Point(angle, row + 1), blend) + Vector3.back * height * .009f);
                    var lower = jacket[row].weights;
                    var upper = jacket[row + 1].weights;
                    float hip = Mathf.Lerp(WeightOn(lower, 0), WeightOn(upper, 0), blend);
                    float spine = Mathf.Lerp(WeightOn(lower, 1), WeightOn(upper, 1), blend);
                    weights.Add(new BoneWeight { boneIndex0 = 0, boneIndex1 = 1, boneIndex2 = 2,
                        weight0 = hip, weight1 = spine, weight2 = 1 - hip - spine });
                }
                if (i != steps) Quad(indices, i * 2, i * 2 + 1, i * 2 + 2, i * 2 + 3);
            }
            Mesh(name, bones, vertices, indices, weights, color);
        }

        private static float WeightOn(BoneWeight weight, int bone) =>
            (weight.boneIndex0 == bone ? weight.weight0 : 0) +
            (weight.boneIndex1 == bone ? weight.weight1 : 0);

        private void Sleeve(bool left, string style, Transform chest)
        {
            Transform upper = Bone(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Transform lower = Bone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Transform hand = Bone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Vector3 shoulder = Point(upper), elbow = Point(lower), wrist = Point(hand);
            Vector3 along = (wrist - shoulder).normalized;
            Vector3 drop = Vector3.ProjectOnPlane(Vector3.down, along).normalized;
            Vector3 cross = Vector3.Cross(drop, along);
            float fullness = style == "armor" ? .67f : style == "taoist" ? 1.17f : 1;
            Ring At(Vector3 center, float radius, float depth, int a, int b, float blend, float folds) =>
                new(center + drop * radius * fullness * height * .22f, drop * (radius * fullness * height),
                    cross * (depth * fullness * height), a, b, blend, folds);
            Ring[] rings =
            {
                At(shoulder - along * height * .044f, .039f, .054f, 3, 3, 0, .015f),
                At(Vector3.Lerp(shoulder, elbow, .18f), .046f, .052f, 3, 0, .63f, .031f),
                At(Vector3.Lerp(shoulder, elbow, .50f), .050f, .047f, 0, 0, 0, .043f),
                At(elbow, .063f, .049f, 0, 1, .53f, .051f),
                At(Vector3.Lerp(elbow,wrist,.69f), .074f, .051f, 1, 1, 0, .062f),
                At(Vector3.Lerp(elbow,wrist,.89f), .067f, .047f, 1, 2, .08f, .060f),
                At(Vector3.Lerp(elbow,wrist,.97f), .064f, .045f, 1, 2, .14f, .057f)
            };
            Transform[] bones = { upper, lower, hand, chest };
            Tube("Wide sleeve", bones, rings, robe);
            Tube("Sleeve facing", bones, new[] { rings[^2], rings[^1] }, lining, false, .003f);
            Tube("Sleeve lining", bones, new[] { rings[^1], At(Vector3.Lerp(elbow,wrist,.85f),
                .027f, .025f, 1, 2, .08f, 0) }, Color.Lerp(robe, Color.black, .34f));
        }

        private void Trouser(bool left)
        {
            Transform calf = Bone(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            Transform foot = Bone(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            Vector3 knee = Point(calf), ankle = Point(foot);
            Ring Leg(Vector3 p, float width, float depth, float blend) =>
                new(p, Vector3.right * (height * width), Vector3.forward * (height * depth), 0, 1, blend, .035f);
            Tube("Undergarment trousers", new[] { calf, foot }, new[]
            {
                Leg(ankle, .023f, .025f, .24f),
                Leg(Vector3.Lerp(ankle, knee, .23f), .025f, .027f, 0),
                Leg(Vector3.Lerp(ankle, knee, .45f), .027f, .028f, 0)
            }, Color.Lerp(sash, robe, .44f));
        }

        private void Tube(string name, Transform[] bones, Ring[] rings, Color color, bool skirt = false, float offset = 0)
        {
            const int sides = 72;
            var vertices = new List<Vector3>();
            var weights = new List<BoneWeight>();
            var indices = new List<int>();
            for (int row = 0; row < rings.Length; row++)
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2 / sides;
                Ring ring = rings[row];
                Vector3 p = ring.Point(angle, row);
                Vector3 surface = p + (p - ring.center).normalized * offset;
                if (skirt) surface.y += height * .007f * (1 - row / (float)rings.Length) * Mathf.Sin(angle * 5);
                vertices.Add(surface);
                BoneWeight weight = ring.weights;
                if (skirt)
                {
                    float influence = Mathf.Lerp(.79f, 0, Mathf.InverseLerp(ground + height * .26f,
                        Point(bones[0]).y + height * .075f, ring.center.y));
                    float left = Mathf.InverseLerp(-.37f, .37f, Mathf.Cos(angle));
                    if (influence > 0)
                        weight = new BoneWeight
                        {
                            boneIndex0 = 0, boneIndex1 = 3, boneIndex2 = 4,
                            weight0 = 1 - influence, weight1 = influence * left, weight2 = influence * (1 - left)
                        };
                }
                weights.Add(weight);
                if (row == rings.Length - 1) continue;
                int here = row * sides + side;
                int next = row * sides + (side + 1) % sides;
                Quad(indices, here, here + sides, next, next + sides);
            }
            Mesh(name, bones, vertices, indices, weights, color);
        }

        private static void Quad(List<int> indices, int a, int b, int c, int d)
        {
            indices.Add(a); indices.Add(b); indices.Add(c);
            indices.Add(c); indices.Add(b); indices.Add(d);
        }

        private void Mesh(string name, Transform[] bones, List<Vector3> vertices, List<int> indices,
            List<BoneWeight> weights, Color color)
        {
            var uv = vertices.ConvertAll(p => new Vector2(p.x + p.z, p.y - p.z * .34f) * 4);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(indices, 0);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = Array.ConvertAll(bones, bone => bone.worldToLocalMatrix * root.localToWorldMatrix);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var renderer = new GameObject(name).AddComponent<SkinnedMeshRenderer>();
            renderer.transform.SetParent(root, false);
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = root;
            renderer.localBounds = new Bounds(Vector3.up * (ground + height * .5f), Vector3.one * height * 2);
            renderer.sharedMaterial = CharacterModelFactory.Cloth;
            CharacterModelFactory.Paint(renderer, color);
            root.GetComponent<CharacterModelMeshes>().Own(mesh);
        }
    }
}
