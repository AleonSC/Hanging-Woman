using System;
using System.Collections.Generic;
using UnityEngine;

namespace HangingWoman.EditorTools
{
    /// <summary>
    /// The hanging woman's body: one skinned mesh over the ragdoll bones (sculpted head, clouded
    /// eyes, torso, arms with fingers, legs with toes, dress bodice), plus cloth-simulated hair
    /// and skirt. All shapes are lofted rings authored in the ragdoll's rest pose; all
    /// textures are painted procedurally.
    /// </summary>
    public static partial class NightStreetSceneBuilder
    {
        // Bone indices in the array passed to BuildWomanBody.
        const int BPelvis = 0, BChest = 1, BHead = 2;
        static int BUpperArm(int i) => 3 + i * 2;
        static int BForearm(int i) => 4 + i * 2;
        static int BThigh(int i) => 7 + i * 2;
        static int BCalf(int i) => 8 + i * 2;

        // Submeshes of the body mesh.
        const int MFace = 0, MBody = 1, MLivid = 2, MNeck = 3, MEyes = 4, MDress = 5;

        /// <summary>Skull centre in head-bone space, and the skull's half-extents.</summary>
        static readonly Vector3 HeadCenter = new Vector3(0f, 0.115f, 0.015f);
        static readonly Vector3 HeadRadii = new Vector3(0.078f, 0.108f, 0.095f);
        static readonly Vector2 EyeOffset = new Vector2(0.031f, 0.014f);
        const float EyeRadius = 0.012f;

        static Material skinFace, skinBody, skinLivid, skinNeck, eyes, hairMat;

        static void CreateWomanMaterials()
        {
            skinFace = Mat("Skin Face", Color.white, 0.42f, 0f, tex: SaveTexture("skin_face", 512, (x, y) =>
            {
                float u = (x + 0.5f) / 512f, v = (y + 0.5f) / 512f;
                return FaceColor(HeadPoint(u, v, out _), u, v);
            }, false, TextureWrapMode.Clamp));
            skinBody = Mat("Skin Body", Color.white, 0.35f, 0f, tex: SaveTexture("skin_body", 512,
                (x, y) => BodySkinColor((x + 0.5f) / 512f, (y + 0.5f) / 512f, false), false, TextureWrapMode.Repeat));
            skinLivid = Mat("Skin Livid", Color.white, 0.35f, 0f, tex: SaveTexture("skin_livid", 512,
                (x, y) => BodySkinColor((x + 0.5f) / 512f, (y + 0.5f) / 512f, true), false, TextureWrapMode.Clamp));
            skinNeck = Mat("Skin Neck", Color.white, 0.4f, 0f, tex: SaveTexture("skin_neck", 256,
                (x, y) => NeckColor((x + 0.5f) / 256f, (y + 0.5f) / 256f), false, TextureWrapMode.Clamp));
            eyes = Mat("Eyes Clouded", Color.white, 0.9f, 0f, tex: SaveTexture("eye_clouded", 256,
                (x, y) => EyeColor((x + 0.5f) / 128f - 1f, (y + 0.5f) / 128f - 1f), false, TextureWrapMode.Clamp));
            hairMat = Cutout(Mat("Hair Wet Black", Color.white, 0.72f, 0f, tex: SaveTexture("hair_strands", 256,
                (x, y) => HairColor((x + 0.5f) / 256f, (y + 0.5f) / 256f), true, TextureWrapMode.Clamp, preserveCoverage: true)), 0.45f);
        }

        // ================================================================== Body assembly

        static void BuildWomanBody(Transform ragdoll, Transform[] bones)
        {
            var body = new SkinBuilder(6);
            Transform head = bones[BHead];
            Vector3 HeadToRoot(Vector3 p) => ragdoll.InverseTransformPoint(head.TransformPoint(p));

            BuildTorso(body);
            BuildNeck(body, HeadToRoot);
            BuildHead(body, HeadToRoot);
            for (int i = 0; i < 2; i++)
            {
                int s = i == 0 ? -1 : 1;
                BuildArm(body, i, s);
                BuildLeg(body, i, s);
            }
            BuildBodice(body);

            var bodyMesh = body.ToMesh("Woman Body");
            Skin("Body", ragdoll, bodyMesh, bones, new[] { skinFace, skinBody, skinLivid, skinNeck, eyes, dress });

            // Skirt: cloth pinned at the waist, colliding with her legs and hips.
            var skirt = new SkinBuilder(1);
            BuildSkirt(skirt);
            var skirtRenderer = Skin("Skirt (Cloth)", ragdoll, skirt.ToMesh("Woman Skirt"), bones, new[] { dress });
            var legColliders = new List<CapsuleCollider> { bones[BPelvis].GetComponent<CapsuleCollider>() };
            for (int i = 0; i < 2; i++)
            {
                legColliders.Add(bones[BThigh(i)].GetComponent<CapsuleCollider>());
                legColliders.Add(bones[BCalf(i)].GetComponent<CapsuleCollider>());
            }
            AddCloth(skirtRenderer, legColliders.ToArray(), null, stiffness: 0.9f, bending: 0.25f, damping: 0.25f);

            // Hair: cloth strands pinned at the scalp, draping over her head, back and shoulders.
            var hair = new SkinBuilder(1);
            BuildHair(hair, HeadToRoot);
            var hairRenderer = Skin("Hair (Cloth)", ragdoll, hair.ToMesh("Woman Hair"), bones, new[] { hairMat });
            var hairColliders = new List<CapsuleCollider> { bones[BChest].GetComponent<CapsuleCollider>() };
            for (int i = 0; i < 2; i++) hairColliders.Add(bones[BUpperArm(i)].GetComponent<CapsuleCollider>());
            AddCloth(hairRenderer, hairColliders.ToArray(), new[] { new ClothSphereColliderPair(head.GetComponent<SphereCollider>()) },
                stiffness: 0.95f, bending: 0.08f, damping: 0.3f);
        }

        static SkinnedMeshRenderer Skin(string name, Transform ragdoll, Mesh mesh, Transform[] bones, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(ragdoll, false);

            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * go.transform.localToWorldMatrix;
            mesh.bindposes = bind;
            mesh = SaveMesh(mesh.name, mesh);

            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones[BPelvis];
            smr.sharedMaterials = materials;
            // She gets dragged around; recompute bounds so she never pops out of view.
            smr.updateWhenOffscreen = true;
            return smr;
        }

        static void AddCloth(SkinnedMeshRenderer smr, CapsuleCollider[] capsules, ClothSphereColliderPair[] spheres,
            float stiffness, float bending, float damping)
        {
            try
            {
                var cloth = smr.gameObject.AddComponent<Cloth>();
                cloth.useGravity = true;
                cloth.stretchingStiffness = stiffness;
                cloth.bendingStiffness = bending;
                cloth.damping = damping;
                cloth.friction = 0.6f;
                cloth.collisionMassScale = 0.1f;
                cloth.worldVelocityScale = 0.4f;
                cloth.worldAccelerationScale = 0.8f;
                // Soaked and heavy: only the faintest stir from the wind.
                cloth.randomAcceleration = new Vector3(0.3f, 0f, 0.3f);
                cloth.capsuleColliders = Array.FindAll(capsules, c => c != null);
                if (spheres != null) cloth.sphereColliders = spheres;

                // Pins come from the mesh's second UV channel; applied again at runtime.
                ClothPinning.Apply(cloth, smr);
                smr.gameObject.AddComponent<ClothPinning>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Hanging Woman] Couldn't set up cloth on {smr.name}, it will be stiff instead: {e.Message}");
            }
        }

        // ================================================================== Torso, neck, dress

        struct TorsoRing
        {
            public float y, halfWidth, halfDepth, zOffset;
            public TorsoRing(float y, float w, float d, float z) { this.y = y; halfWidth = w; halfDepth = d; zOffset = z; }
        }

        // Rest-pose cross-sections of the torso, from crotch to the base of the neck.
        static readonly TorsoRing[] TorsoRings =
        {
            new TorsoRing(0.80f, 0.10f, 0.085f, -0.005f),
            new TorsoRing(0.85f, 0.165f, 0.11f, -0.01f),
            new TorsoRing(0.90f, 0.17f, 0.115f, -0.012f),
            new TorsoRing(0.96f, 0.155f, 0.10f, -0.005f),
            new TorsoRing(1.02f, 0.13f, 0.085f, 0f),
            new TorsoRing(1.08f, 0.135f, 0.088f, 0f),
            new TorsoRing(1.14f, 0.142f, 0.092f, 0.002f),
            new TorsoRing(1.20f, 0.148f, 0.10f, 0.008f),
            new TorsoRing(1.25f, 0.15f, 0.106f, 0.012f),
            new TorsoRing(1.30f, 0.155f, 0.095f, 0.004f),
            new TorsoRing(1.33f, 0.163f, 0.086f, -0.002f),
            new TorsoRing(1.36f, 0.145f, 0.075f, -0.007f),
            new TorsoRing(1.385f, 0.105f, 0.062f, -0.01f),
            new TorsoRing(1.41f, 0.06f, 0.052f, -0.01f),
        };

        static TorsoRing TorsoAt(float y)
        {
            if (y <= TorsoRings[0].y) return TorsoRings[0];
            for (int i = 1; i < TorsoRings.Length; i++)
            {
                if (y > TorsoRings[i].y) continue;
                var a = TorsoRings[i - 1];
                var b = TorsoRings[i];
                float t = Mathf.InverseLerp(a.y, b.y, y);
                return new TorsoRing(y, Mathf.Lerp(a.halfWidth, b.halfWidth, t), Mathf.Lerp(a.halfDepth, b.halfDepth, t), Mathf.Lerp(a.zOffset, b.zOffset, t));
            }
            return TorsoRings[TorsoRings.Length - 1];
        }

        static float AngleDelta(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg)) * Mathf.Deg2Rad;

        /// <summary>Bust and buttocks: extra radius at the right angles and heights. a=π/2 faces forward.</summary>
        static float TorsoShape(float y, float a)
        {
            float bust = 0f;
            foreach (float c in new[] { Mathf.PI * 0.5f - 0.5f, Mathf.PI * 0.5f + 0.5f })
                bust = Mathf.Max(bust, Gauss(AngleDelta(a, c), 0.33f));
            bust *= Gauss(y - 1.245f, 0.035f) * 0.09f;

            float seat = 0f;
            foreach (float c in new[] { Mathf.PI * 1.5f - 0.45f, Mathf.PI * 1.5f + 0.45f })
                seat = Mathf.Max(seat, Gauss(AngleDelta(a, c), 0.4f));
            seat *= Gauss(y - 0.885f, 0.035f) * 0.07f;
            return 1f + bust + seat;
        }

        static BoneWeight TorsoWeight(float y) => W(BPelvis, BChest, Mathf.InverseLerp(0.97f, 1.07f, y));

        static void BuildTorso(SkinBuilder b)
        {
            var rings = new List<Ring>();
            float y0 = TorsoRings[0].y, y1 = TorsoRings[TorsoRings.Length - 1].y;
            for (float y = y0; y <= y1 + 0.0001f; y += 0.02f)
            {
                var t = TorsoAt(y);
                rings.Add(new Ring(new Vector3(0f, y, t.zOffset), t.halfWidth, t.halfDepth, TorsoWeight(y), Mathf.InverseLerp(y0, y1, y)));
            }
            b.Loft(rings, Vector3.forward, 40, MBody, (i, a) => TorsoShape(rings[i].center.y, a));
        }

        static void BuildBodice(SkinBuilder b)
        {
            // A sleeved bodice a little proud of the torso, scooped at the neck.
            var rings = new List<Ring>();
            const float y0 = 0.93f, y1 = 1.37f;
            for (float y = y0; y <= y1 + 0.0001f; y += 0.022f)
            {
                var t = TorsoAt(y);
                rings.Add(new Ring(new Vector3(0f, y, t.zOffset), t.halfWidth + 0.013f, t.halfDepth + 0.013f, TorsoWeight(y), Mathf.InverseLerp(y0, y1, y)));
            }
            int last = rings.Count - 1;
            b.Loft(rings, Vector3.forward, 40, MDress, (i, a) => TorsoShape(rings[i].center.y, a), doubleSided: true,
                offset: (i, a) =>
                {
                    if (i != last) return Vector3.zero;
                    float front = Mathf.Max(0f, Mathf.Sin(a)), back = Mathf.Max(0f, -Mathf.Sin(a));
                    return Vector3.down * (0.075f * front * front + 0.025f * back * back);
                });

            // Short sleeves over the tops of the arms.
            for (int i = 0; i < 2; i++)
            {
                int s = i == 0 ? -1 : 1;
                var sleeve = ShoulderCap(s, 0.01f, UpperArmWeight(i, 0f));
                for (int k = 0; k <= 6; k++)
                {
                    float t = k / 6f * 0.36f;
                    Vector3 c = Vector3.Lerp(Shoulder(s), Elbow(s), t);
                    float r = Mathf.Lerp(0.05f, 0.045f, t / 0.36f) + 0.01f + (k == 6 ? 0.005f : 0f);
                    sleeve.Add(new Ring(c, r, r * 1.04f, UpperArmWeight(i, t), t));
                }
                b.Loft(sleeve, Vector3.forward, 20, MDress, doubleSided: true);
            }
        }

        static void BuildNeck(SkinBuilder b, Func<Vector3, Vector3> headToRoot)
        {
            Vector3 bottom = new Vector3(0f, 1.335f, -0.012f);
            Vector3 top = headToRoot(new Vector3(0f, 0.05f, -0.005f));
            var rings = new List<Ring>();
            for (int k = 0; k <= 14; k++)
            {
                float t = k / 14f;
                float rx = Mathf.Lerp(0.056f, 0.046f, Mathf.SmoothStep(0f, 1f, t));
                float rz = Mathf.Lerp(0.05f, 0.044f, Mathf.SmoothStep(0f, 1f, t));
                // The noose has bitten in here.
                float ligature = 1f - 0.11f * Gauss(t - 0.64f, 0.05f);
                rings.Add(new Ring(Vector3.Lerp(bottom, top, t), rx * ligature, rz * ligature,
                    W(BChest, BHead, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.85f, t))), t));
            }
            b.Loft(rings, Vector3.forward, 24, MNeck);
        }

        // ================================================================== Head and eyes

        /// <summary>
        /// The skull surface in head-centred space for spherical texture coordinates (u around
        /// from the back, v from bottom to top). Shared by the mesh and the face texture so the
        /// paint lines up with the sculpt.
        /// </summary>
        static Vector3 HeadPoint(float u, float v, out Vector3 normal)
        {
            float theta = (1f - v) * Mathf.PI;              // 0 at the crown
            float phi = (u - 0.5f) * Mathf.PI * 2f;         // 0 at the face
            var dir = new Vector3(Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Cos(phi));
            var p = Vector3.Scale(dir, HeadRadii);

            // Narrow the jaw, tuck in the back of the neck, fill out the back of the cranium.
            // Every factor blends smoothly so there are no creases.
            float jaw = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.03f, -0.105f, p.y));
            float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.01f, -0.05f, p.z));
            float upper = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.03f, 0.04f, p.y));
            p.x *= 1f - 0.26f * jaw;
            p.z *= 1f + back * (0.06f * upper - 0.3f * jaw);

            normal = new Vector3(p.x / (HeadRadii.x * HeadRadii.x), p.y / (HeadRadii.y * HeadRadii.y), p.z / (HeadRadii.z * HeadRadii.z)).normalized;
            return p + normal * FaceRelief(p);
        }

        static float Gauss(float d, float sigma) => Mathf.Exp(-(d * d) / (2f * sigma * sigma));
        static float Gauss(float dx, float dy, float sigma) => Mathf.Exp(-(dx * dx + dy * dy) / (2f * sigma * sigma));

        /// <summary>Displacement of the skull surface that sculpts the face and ears.</summary>
        static float FaceRelief(Vector3 p)
        {
            float x = p.x, ax = Mathf.Abs(x), y = p.y;
            float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.0f, 0.05f, p.z));
            float d = 0f;

            d -= 0.011f * Gauss(ax - EyeOffset.x, y - EyeOffset.y, 0.0115f);                         // eye sockets
            d += 0.0028f * Gauss(y - 0.033f, 0.006f) * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.035f, 0.055f, ax)); // brow ridge

            // Nose: a ridge growing from the bridge to the tip, then dropping away.
            float t = Mathf.Clamp01((0.032f - y) / 0.055f);
            float noseHeight = y >= -0.023f ? (y <= 0.032f ? 0.006f + 0.016f * Mathf.Pow(t, 1.5f) : 0f) : 0.022f * Gauss(y + 0.023f, 0.006f);
            d += noseHeight * Gauss(x, 0.006f + 0.007f * t);
            d += 0.006f * Gauss(ax - 0.011f, y + 0.022f, 0.005f);                                     // nostril wings
            d -= 0.004f * Gauss(ax - 0.007f, y + 0.027f, 0.0025f);                                    // nostrils

            // Slack mouth, corners turned down, lips full in the middle and thinning to the corners.
            float mouthY = MouthLine(x);
            float lipSpan = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.026f, 0.006f, ax));
            d += 0.003f * Gauss(y - mouthY - 0.0065f, 0.0042f) * lipSpan;                             // upper lip
            d += 0.0036f * Gauss(y - mouthY + 0.0075f, 0.0048f) * lipSpan;                            // lower lip
            d -= 0.0045f * Gauss(y - mouthY, 0.0018f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.021f, 0.006f, ax)); // parted mouth

            d += 0.004f * Gauss(x, y + 0.084f, 0.016f);                                               // chin
            d += 0.004f * Gauss(ax - 0.047f, y + 0.004f, 0.014f);                                     // cheekbones
            d -= 0.004f * Gauss(ax - 0.045f, y + 0.04f, 0.014f);                                      // hollow cheeks
            d -= 0.003f * Gauss(ax - 0.07f, y - 0.03f, 0.015f);                                       // temples
            d *= front;

            // Ears, on the sides.
            d += 0.009f * Gauss(y + 0.005f, 0.018f) * Gauss(p.z + 0.008f, 0.011f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.08f, ax));
            return d;
        }

        /// <summary>Height of the gap between the lips at x (corners droop).</summary>
        static float MouthLine(float x) => -0.046f - 0.0025f * (x / 0.02f) * (x / 0.02f);

        static Vector3 EyeCenter(int side)
        {
            float x = side * EyeOffset.x, y = EyeOffset.y;
            float z = HeadRadii.z * Mathf.Sqrt(Mathf.Max(0f, 1f - (x * x) / (HeadRadii.x * HeadRadii.x) - (y * y) / (HeadRadii.y * HeadRadii.y)));
            // Sunk into the socket so only the lid opening shows, not a bulging ball.
            return new Vector3(x, y, z - 0.0195f);
        }

        static void BuildHead(SkinBuilder b, Func<Vector3, Vector3> headToRoot)
        {
            // Dense enough that lips and nostrils (a few millimetres) are actually resolved.
            const int rows = 128, cols = 176;
            int start = b.Count;
            var w = W(BHead);
            for (int r = 0; r <= rows; r++)
            {
                float v = 1f - r / (float)rows;
                for (int c = 0; c <= cols; c++)
                {
                    float u = c / (float)cols;
                    var p = HeadPoint(u, v, out _);
                    b.Add(headToRoot(HeadCenter + p), new Vector2(u, v), w);
                }
            }
            b.Grid(start, rows + 1, cols + 1, MFace);

            // Eyeballs sit in the sockets; the lids are painted onto them, half closed.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 center = EyeCenter(side);
                int eyeStart = b.Count;
                const int er = 16, ec = 24;
                for (int r = 0; r <= er; r++)
                {
                    float theta = r / (float)er * Mathf.PI;
                    for (int c = 0; c <= ec; c++)
                    {
                        float phi = (c / (float)ec - 0.5f) * Mathf.PI * 2f;
                        var q = new Vector3(Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Cos(phi)) * EyeRadius;
                        // Planar projection from the front so the iris sits at the centre of the texture.
                        var uv = new Vector2(0.5f + q.x / (2f * EyeRadius), 0.5f + q.y / (2f * EyeRadius));
                        b.Add(headToRoot(HeadCenter + center + q), uv, w);
                    }
                }
                b.Grid(eyeStart, er + 1, ec + 1, MEyes);
            }
        }

        // ================================================================== Arms and hands

        static Vector3 Shoulder(int s) => new Vector3(s * 0.168f, 1.345f, -0.008f);
        static Vector3 Elbow(int s) => new Vector3(s * 0.183f, 1.075f, 0f);
        static Vector3 Wrist(int s) => new Vector3(s * 0.19f, 0.825f, 0.008f);

        /// <summary>Rings that dome over the top of the shoulder joint, ending where the arm starts.</summary>
        static List<Ring> ShoulderCap(int s, float thicken, BoneWeight w)
        {
            return new List<Ring>
            {
                new Ring(Shoulder(s) + Vector3.up * 0.034f, 0.008f + thicken * 0.5f, 0.008f + thicken * 0.5f, w, 0f),
                new Ring(Shoulder(s) + Vector3.up * 0.026f, 0.03f + thicken, 0.031f + thicken, w, 0f),
                new Ring(Shoulder(s) + Vector3.up * 0.013f, 0.044f + thicken, 0.046f + thicken, w, 0f),
            };
        }

        static BoneWeight UpperArmWeight(int i, float t)
        {
            if (t < 0.15f) return W(BChest, BUpperArm(i), 0.5f + 0.5f * t / 0.15f);
            if (t > 0.85f) return W(BUpperArm(i), BForearm(i), 0.5f * (t - 0.85f) / 0.15f);
            return W(BUpperArm(i));
        }

        static void BuildArm(SkinBuilder b, int i, int s)
        {
            // Upper arm.
            float[] ut = { 0f, 0.12f, 0.3f, 0.5f, 0.75f, 0.92f, 1f };
            float[] ux = { 0.05f, 0.048f, 0.045f, 0.042f, 0.038f, 0.035f, 0.034f };
            float[] uz = { 0.052f, 0.05f, 0.048f, 0.045f, 0.04f, 0.037f, 0.036f };
            // Rounded over the top of the shoulder rather than starting as an open tube.
            var upper = ShoulderCap(s, 0f, W(BChest, BUpperArm(i), 0.5f));
            for (int k = 0; k < ut.Length; k++)
                upper.Add(new Ring(Vector3.Lerp(Shoulder(s), Elbow(s), ut[k]) + Vector3.forward * 0.003f * Gauss(ut[k] - 0.4f, 0.15f),
                    ux[k], uz[k], UpperArmWeight(i, ut[k]), ut[k]));
            b.Loft(upper, Vector3.forward, 20, MBody);

            // Forearm.
            float[] ft = { 0f, 0.1f, 0.3f, 0.6f, 0.85f, 1f };
            float[] fx = { 0.034f, 0.036f, 0.037f, 0.031f, 0.025f, 0.02f };
            float[] fz = { 0.036f, 0.039f, 0.041f, 0.034f, 0.029f, 0.026f };
            var fore = new List<Ring>();
            for (int k = 0; k < ft.Length; k++)
                fore.Add(new Ring(Vector3.Lerp(Elbow(s), Wrist(s), ft[k]), fx[k], fz[k],
                    ft[k] < 0.12f ? W(BUpperArm(i), BForearm(i), 0.5f + 0.5f * ft[k] / 0.12f) : W(BForearm(i)), ft[k] * 0.4f));
            b.Loft(fore, Vector3.forward, 20, MLivid);

            BuildHand(b, i, s);
        }

        static void BuildHand(SkinBuilder b, int i, int s)
        {
            var w = W(BForearm(i));
            Vector3 medial = new Vector3(-s, 0f, 0f);
            Vector3 knuckles = new Vector3(s * 0.186f, 0.735f, 0.012f);

            // Palm: thin side to side, wide front to back (palms face her thighs).
            float[] pt = { 0f, 0.25f, 0.6f, 0.9f, 1f, 1.08f };
            float[] px = { 0.018f, 0.017f, 0.016f, 0.015f, 0.014f, 0.006f };
            float[] pz = { 0.026f, 0.033f, 0.039f, 0.04f, 0.038f, 0.02f };
            var palm = new List<Ring>();
            for (int k = 0; k < pt.Length; k++)
                palm.Add(new Ring(Vector3.LerpUnclamped(Wrist(s), knuckles, pt[k]), px[k], pz[k], w, 0.45f + 0.15f * Mathf.Min(pt[k], 1f)));
            b.Loft(palm, Vector3.forward, 16, MLivid);

            // Four fingers, relaxed and curling in toward the palm.
            float[] zOffsets = { 0.026f, 0.009f, -0.008f, -0.024f };
            float[][] lengths =
            {
                new[] { 0.038f, 0.024f, 0.019f }, new[] { 0.043f, 0.027f, 0.021f },
                new[] { 0.04f, 0.025f, 0.02f }, new[] { 0.031f, 0.019f, 0.017f },
            };
            float[] radii = { 0.0085f, 0.009f, 0.0085f, 0.0072f };
            for (int f = 0; f < 4; f++)
            {
                float curl = 1f + 0.12f * f;
                var dirs = new[]
                {
                    Curl(medial, 12f * curl), Curl(medial, 32f * curl), Curl(medial, 54f * curl),
                };
                Finger(b, knuckles + new Vector3(0f, 0.004f, zOffsets[f]), dirs, lengths[f], radii[f], w);
            }

            // Thumb, lying forward and in.
            Vector3 d1 = (new Vector3(0f, -0.75f, 0.5f) + medial * 0.35f).normalized;
            Vector3 d2 = (d1 + medial * 0.35f + Vector3.down * 0.2f).normalized;
            Vector3 d3 = (d2 + medial * 0.3f).normalized;
            Finger(b, new Vector3(s * 0.183f, 0.805f, 0.03f), new[] { d1, d2, d3 }, new[] { 0.035f, 0.028f, 0.024f }, 0.012f, w);
        }

        /// <summary>Straight down, rotated by `degrees` toward `toward`.</summary>
        static Vector3 Curl(Vector3 toward, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return (Vector3.down * Mathf.Cos(a) + toward * Mathf.Sin(a)).normalized;
        }

        /// <summary>A jointed digit: one segment per direction, tapering to a rounded tip.</summary>
        static void Finger(SkinBuilder b, Vector3 start, Vector3[] dirs, float[] lengths, float radius, BoneWeight w, int sub = MLivid)
        {
            var rings = new List<Ring>();
            Vector3 p = start;
            float total = 0f;
            foreach (float l in lengths) total += l;
            float travelled = 0f;
            for (int k = 0; k < dirs.Length; k++)
            {
                float r = radius * (1f - 0.12f * k);
                rings.Add(new Ring(p, r, r * 0.92f, w, 0.6f + 0.4f * travelled / total));
                Vector3 mid = p + dirs[k] * lengths[k] * 0.5f;
                rings.Add(new Ring(mid, r * 0.9f, r * 0.85f, w, 0.6f + 0.4f * (travelled + lengths[k] * 0.5f) / total));
                p += dirs[k] * lengths[k];
                travelled += lengths[k];
            }
            float tipR = radius * 0.7f;
            Vector3 last = dirs[dirs.Length - 1];
            rings.Add(new Ring(p - last * tipR * 0.6f, tipR, tipR * 0.85f, w, 1f));
            rings.Add(new Ring(p, tipR * 0.45f, tipR * 0.4f, w, 1f));
            rings.Add(new Ring(p + last * tipR * 0.35f, tipR * 0.05f, tipR * 0.05f, w, 1f));
            b.Loft(rings, Vector3.forward, 10, sub);
        }

        // ================================================================== Legs and feet

        static void BuildLeg(SkinBuilder b, int i, int s)
        {
            Vector3 hip = new Vector3(s * 0.088f, 0.935f, -0.005f);
            Vector3 knee = new Vector3(s * 0.082f, 0.48f, 0.008f);
            Vector3 ankle = new Vector3(s * 0.08f, 0.075f, -0.012f);

            float[] tt = { 0f, 0.1f, 0.3f, 0.55f, 0.8f, 0.95f, 1f };
            float[] tx = { 0.085f, 0.083f, 0.077f, 0.067f, 0.055f, 0.049f, 0.047f };
            float[] tz = { 0.088f, 0.087f, 0.082f, 0.07f, 0.058f, 0.052f, 0.05f };
            var thigh = new List<Ring>();
            for (int k = 0; k < tt.Length; k++)
            {
                float t = tt[k];
                var w = t < 0.15f ? W(BPelvis, BThigh(i), 0.4f + 0.6f * t / 0.15f)
                    : t > 0.88f ? W(BThigh(i), BCalf(i), 0.5f * (t - 0.88f) / 0.12f) : W(BThigh(i));
                thigh.Add(new Ring(Vector3.Lerp(hip, knee, t), tx[k], tz[k], w, t));
            }
            b.Loft(thigh, Vector3.forward, 24, MBody);

            float[] ct = { 0f, 0.08f, 0.25f, 0.4f, 0.6f, 0.8f, 0.92f, 1f };
            float[] cx = { 0.047f, 0.048f, 0.05f, 0.049f, 0.041f, 0.032f, 0.027f, 0.026f };
            float[] cz = { 0.05f, 0.052f, 0.058f, 0.056f, 0.045f, 0.034f, 0.029f, 0.028f };
            var calf = new List<Ring>();
            for (int k = 0; k < ct.Length; k++)
            {
                float t = ct[k];
                // The calf muscle bulges out at the back.
                Vector3 c = Vector3.Lerp(knee, ankle, t) + Vector3.back * 0.008f * Gauss(t - 0.32f, 0.12f);
                calf.Add(new Ring(c, cx[k], cz[k], t < 0.1f ? W(BThigh(i), BCalf(i), 0.5f + 0.5f * t / 0.1f) : W(BCalf(i)), t * 0.75f));
            }
            b.Loft(calf, Vector3.forward, 20, MLivid);

            BuildFoot(b, i, s);
        }

        static void BuildFoot(SkinBuilder b, int i, int s)
        {
            var w = W(BCalf(i));
            float x = s * 0.08f;
            // (z, centre height, half width, half height), heel to toes.
            float[,] f =
            {
                { -0.068f, 0.045f, 0.01f, 0.012f },
                { -0.06f, 0.045f, 0.026f, 0.03f },
                { -0.045f, 0.05f, 0.032f, 0.042f },
                { -0.015f, 0.055f, 0.036f, 0.048f },
                { 0.03f, 0.045f, 0.042f, 0.038f },
                { 0.08f, 0.033f, 0.046f, 0.027f },
                { 0.115f, 0.025f, 0.047f, 0.019f },
                { 0.132f, 0.02f, 0.043f, 0.014f },
                { 0.14f, 0.018f, 0.025f, 0.008f },
            };
            var rings = new List<Ring>();
            for (int k = 0; k < f.GetLength(0); k++)
                rings.Add(new Ring(new Vector3(x, f[k, 1], f[k, 0]), f[k, 2], f[k, 3], w, 0.8f + 0.2f * k / (f.GetLength(0) - 1f)));
            b.Loft(rings, Vector3.up, 20, MLivid);

            // Toes, big toe on the inside.
            Vector3 medial = new Vector3(-s, 0f, 0f);
            float[] offsets = { 0.026f, 0.008f, -0.008f, -0.021f, -0.032f };
            float[] toeR = { 0.011f, 0.0085f, 0.008f, 0.0075f, 0.007f };
            float[] toeL = { 0.035f, 0.03f, 0.027f, 0.024f, 0.02f };
            for (int t = 0; t < 5; t++)
            {
                Vector3 start = new Vector3(x, 0.017f, 0.118f - Mathf.Abs(offsets[t]) * 0.4f) + medial * offsets[t];
                Vector3 dir = new Vector3(0f, -0.15f, 1f).normalized;
                Finger(b, start, new[] { dir, dir }, new[] { toeL[t] * 0.55f, toeL[t] * 0.45f }, toeR[t], w);
            }
        }

        // ================================================================== Skirt and hair (cloth)

        static void BuildSkirt(SkinBuilder b)
        {
            const int segments = 44, rows = 16;
            var hem = new float[segments + 1];
            var rng2 = new System.Random(5);
            for (int j = 0; j < segments; j++) hem[j] = (float)rng2.NextDouble() * 0.07f;
            hem[segments] = hem[0];

            var rings = new List<Ring>();
            for (int k = 0; k <= rows; k++)
            {
                float t = k / (float)rows;
                float flare = Mathf.Pow(t, 0.9f);
                rings.Add(new Ring(new Vector3(0f, Mathf.Lerp(0.99f, 0.32f, t), 0.01f * t),
                    Mathf.Lerp(0.158f, 0.31f, flare), Mathf.Lerp(0.108f, 0.27f, flare), W(BPelvis), t));
            }
            // Pinned at the waistband, increasingly free further down.
            b.Loft(rings, Vector3.forward, segments, 0, (i, a) =>
                {
                    float t = i / (float)rows;
                    return 1f + t * (0.05f * Mathf.Sin(a * 7f + t * 2f) + 0.025f * Mathf.Sin(a * 13f));
                },
                doubleSided: true,
                offset: (i, a) => i == rows ? Vector3.down * hem[Mathf.RoundToInt(a / (Mathf.PI * 2f) * segments)] : Vector3.zero,
                pin: i => i <= 1 ? 0f : (i / (float)rows - 0.06f) * 0.7f);
        }

        static void BuildHair(SkinBuilder b, Func<Vector3, Vector3> headToRoot)
        {
            var w = W(BHead);
            Vector3 headCenterRoot = headToRoot(HeadCenter);
            var rng2 = new System.Random(77);
            float Rr(float lo, float hi) => lo + (float)rng2.NextDouble() * (hi - lo);

            var anchors = new List<(float theta, float phi, float length, float width)>();
            // Back and sides, down to the nape.
            for (int k = 0; k < 75; k++)
                anchors.Add((Rr(20f, 100f), Rr(55f, 305f), Rr(0.55f, 0.82f), Rr(0.045f, 0.075f)));
            // Crown, falling over the back and sides.
            for (int k = 0; k < 12; k++)
                anchors.Add((Rr(5f, 25f), Rr(0f, 360f), Rr(0.6f, 0.8f), Rr(0.05f, 0.07f)));
            // Long front strands that hang over her face.
            for (int k = 0; k < 7; k++)
                anchors.Add((Rr(38f, 55f), Rr(-32f, 32f), Rr(0.38f, 0.55f), Rr(0.03f, 0.045f)));

            const int segs = 12;
            foreach (var (theta, phi, length, width) in anchors)
            {
                float th = theta * Mathf.Deg2Rad, ph = phi * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Cos(ph));
                Vector3 anchor = headToRoot(HeadCenter + Vector3.Scale(dir, HeadRadii) * 1.04f);

                Vector3 radial = anchor - headCenterRoot;
                radial.y = 0f;
                radial = radial.sqrMagnitude > 1e-6f ? radial.normalized : Vector3.back;
                Vector3 across = Vector3.Cross(Vector3.up, radial);

                int start = b.Count;
                for (int k = 0; k <= segs; k++)
                {
                    float t = k / (float)segs;
                    Vector3 c = anchor + Vector3.down * length * t + radial * 0.01f * t;
                    c = OutsideTorso(c, 0.025f);
                    float halfWidth = width * 0.5f * Mathf.Lerp(1f, 0.55f, t);
                    float pin = k == 0 ? 0f : k == 1 ? 0.015f : 0.05f + 0.9f * t;
                    b.Add(c - across * halfWidth, new Vector2(0f, t), w, pin);
                    b.Add(c + across * halfWidth, new Vector2(1f, t), w, pin);
                }
                b.Grid(start, segs + 1, 2, 0, doubleSided: true);
            }
        }

        /// <summary>Pushes a point out of the torso so draped hair starts outside the body.</summary>
        static Vector3 OutsideTorso(Vector3 p, float margin)
        {
            if (p.y < TorsoRings[0].y || p.y > TorsoRings[TorsoRings.Length - 1].y + 0.02f) return p;
            var t = TorsoAt(p.y);
            float rx = t.halfWidth + margin, rz = t.halfDepth + margin;
            float dx = p.x, dz = p.z - t.zOffset;
            float e = (dx * dx) / (rx * rx) + (dz * dz) / (rz * rz);
            if (e >= 1f || e < 1e-6f) return p;
            float k = 1f / Mathf.Sqrt(e);
            return new Vector3(dx * k, p.y, t.zOffset + dz * k);
        }

        // ================================================================== Textures

        static Color FaceColor(Vector3 p, float u, float v)
        {
            float ax = Mathf.Abs(p.x), y = p.y;
            float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.0f, 0.04f, p.z));

            // Bloodless, faintly blue skin.
            Color c = Color.Lerp(new Color(0.82f, 0.78f, 0.75f), new Color(0.7f, 0.72f, 0.8f), 0.28f);
            c *= 0.9f + 0.14f * Fbm(u, v, 8, 4, 301);

            // Bruised, sunken eyes.
            float eyeRing = Gauss(ax - EyeOffset.x, y - EyeOffset.y + 0.003f, 0.017f) * front;
            c = Color.Lerp(c, new Color(0.36f, 0.27f, 0.33f), eyeRing * 0.8f);

            // Thin, uneven eyebrows.
            float browY = 0.03f + 0.004f * (1f - Mathf.Pow((ax - 0.031f) / 0.02f, 2f));
            float brow = Gauss(y - browY, 0.0025f) * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.045f, 0.052f, ax)) *
                         Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.01f, 0.016f, ax)) * front;
            if (Fbm(u * 3f, v * 3f, 64, 2, 17) > 0.4f) c = Color.Lerp(c, new Color(0.07f, 0.06f, 0.06f), brow * 0.75f);

            // Blue-grey lips, and the dark gap of the parted mouth.
            float mouthY = MouthLine(p.x);
            float lipSpan = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.026f, 0.008f, ax));
            float lips = Mathf.Max(Gauss(y - mouthY - 0.0065f, 0.004f), Gauss(y - mouthY + 0.0075f, 0.0045f)) * lipSpan * front;
            c = Color.Lerp(c, new Color(0.4f, 0.3f, 0.4f), lips * 0.85f);
            float mouth = Gauss(y - mouthY, 0.0016f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.008f, ax)) * front;
            c = Color.Lerp(c, new Color(0.06f, 0.02f, 0.03f), mouth);
            c = Color.Lerp(c, new Color(0.1f, 0.05f, 0.05f), Gauss(ax - 0.007f, y + 0.027f, 0.0025f) * front); // nostrils

            // Burst capillaries around the eyes and cheeks.
            float region = Mathf.Max(Gauss(ax - EyeOffset.x, y - EyeOffset.y, 0.03f), 0.4f * Gauss(ax - 0.04f, y + 0.01f, 0.03f)) * front;
            if (region > 0.2f && Hash01(Mathf.FloorToInt(u * 700f), Mathf.FloorToInt(v * 700f), 5) > 0.985f)
                c = Color.Lerp(c, new Color(0.5f, 0.05f, 0.06f), 0.8f);

            // A trickle of blood from one nostril and the corner of the mouth, down to the chin.
            float trickle = 0f;
            if (y < -0.026f && y > -0.1f)
            {
                float nx = -0.009f + 0.003f * Mathf.Sin(y * 90f);
                trickle = Mathf.Max(trickle, Gauss(p.x - nx, 0.0013f) * Mathf.SmoothStep(1f, 0.3f, Mathf.InverseLerp(-0.026f, -0.1f, y)));
            }
            if (y < -0.05f && y > -0.11f)
            {
                float mx = 0.018f + 0.004f * Mathf.Sin(y * 70f);
                trickle = Mathf.Max(trickle, Gauss(p.x - mx, 0.0016f) * Mathf.SmoothStep(1f, 0.2f, Mathf.InverseLerp(-0.047f, -0.11f, y)));
            }
            c = Color.Lerp(c, new Color(0.3f, 0.02f, 0.03f), trickle * front * 0.85f);

            // Grime.
            float dirt = Mathf.Clamp01((Fbm(u, v, 6, 4, 911) - 0.55f) * 2.5f);
            c = Color.Lerp(c, new Color(0.35f, 0.29f, 0.24f), dirt * 0.4f);

            // Ears a little darker and purpler.
            float ear = Gauss(y + 0.005f, 0.022f) * Gauss(p.z + 0.008f, 0.014f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.078f, ax));
            c = Color.Lerp(c, new Color(0.55f, 0.42f, 0.48f), ear * 0.5f);

            // Scalp under the hair: dark, with a ragged hairline.
            float hairline = Mathf.Lerp(0.052f, -0.075f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, -0.04f, p.z)));
            hairline += 0.006f * (Fbm(u * 4f, v * 4f, 16, 2, 23) - 0.5f);
            float scalp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hairline - 0.006f, hairline + 0.004f, y));
            if (ax > 0.06f && p.z > -0.02f && y < 0.0f) scalp *= 0.3f; // keep the ears clear
            c = Color.Lerp(c, new Color(0.025f, 0.024f, 0.028f), scalp);

            c.a = 1f;
            return c;
        }

        /// <summary>Pale skin with veins, bruises and dirt. Livid: purple mottling where blood has pooled, worst at v = 1.</summary>
        static Color BodySkinColor(float u, float v, bool livid)
        {
            Color c = Color.Lerp(new Color(0.82f, 0.78f, 0.75f), new Color(0.7f, 0.72f, 0.8f), 0.2f);
            c *= 0.9f + 0.14f * Fbm(u, v, 8, 4, 41);

            float vein = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(Fbm(u, v, 6, 3, 77) - 0.5f) / 0.018f);
            c = Color.Lerp(c, new Color(0.5f, 0.56f, 0.76f), vein * 0.32f);

            float bruise = Mathf.Clamp01((Fbm(u, v, 4, 3, 555) - 0.66f) * 4f);
            c = Color.Lerp(c, new Color(0.42f, 0.28f, 0.38f), bruise * 0.7f);

            float dirt = Mathf.Clamp01((Fbm(u, v, 5, 4, 909) - 0.55f) * 2f);
            c = Color.Lerp(c, new Color(0.33f, 0.26f, 0.2f), dirt * 0.45f);

            if (livid)
            {
                float mottle = Fbm(u, v, 10, 4, 1234);
                float amount = (0.3f + 0.7f * v) * (0.55f + 0.45f * mottle);
                c = Color.Lerp(c, new Color(0.5f, 0.26f, 0.35f), amount * 0.8f);
                // Filth and blood on the soles and fingertips.
                float grime = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, v)) * (0.6f + 0.4f * Fbm(u, v, 12, 3, 44));
                c = Color.Lerp(c, new Color(0.13f, 0.08f, 0.07f), grime * 0.75f);
                if (Hash01(Mathf.FloorToInt(u * 300f), Mathf.FloorToInt(v * 300f), 9) > 0.97f - 0.05f * v)
                    c = Color.Lerp(c, new Color(0.28f, 0.02f, 0.03f), 0.85f);
            }
            c.a = 1f;
            return c;
        }

        /// <summary>Neck skin with the raw ligature mark of the noose and congestion above it.</summary>
        static Color NeckColor(float u, float v)
        {
            Color c = BodySkinColor(u, v * 0.3f, false);
            float band = Gauss(v - 0.64f + 0.02f * Mathf.Sin(u * Mathf.PI * 2f), 0.035f);
            float raw = band * (0.7f + 0.3f * Fbm(u, v, 24, 3, 12));
            c = Color.Lerp(c, new Color(0.34f, 0.15f, 0.16f), raw);
            float edge = Gauss(Mathf.Abs(v - 0.64f) - 0.05f, 0.012f);
            c = Color.Lerp(c, new Color(0.55f, 0.18f, 0.2f), edge * 0.5f);

            float above = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 0.9f, v));
            c = Color.Lerp(c, new Color(0.5f, 0.35f, 0.45f), above * 0.35f);
            if (above > 0.1f && Hash01(Mathf.FloorToInt(u * 300f), Mathf.FloorToInt(v * 300f), 3) > 0.975f)
                c = Color.Lerp(c, new Color(0.5f, 0.05f, 0.06f), 0.8f);
            c.a = 1f;
            return c;
        }

        /// <summary>Front view of an eyeball, x and y in -1..1: drooping lids, bloodshot white, clouded iris.</summary>
        static Color EyeColor(float x, float y)
        {
            // Same bruised tone as the skin around the eyes, so the lids read as part of the face.
            Color lid = new Color(0.45f, 0.36f, 0.41f);
            float upper = 0.08f - 0.18f * x * x;
            float lower = -0.62f + 0.25f * x * x;
            if (y > upper || y < lower)
            {
                float lash = Mathf.Min(Mathf.Abs(y - upper), Mathf.Abs(y - lower));
                return lash < 0.07f ? new Color(0.06f, 0.04f, 0.05f, 1f) : lid;
            }

            Color sclera = new Color(0.86f, 0.8f, 0.7f);
            float vein = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(Mathf.PerlinNoise(x * 6f + 3f, y * 6f + 7f) - 0.5f) / 0.03f);
            sclera = Color.Lerp(sclera, new Color(0.7f, 0.15f, 0.12f), vein * 0.6f * Mathf.Clamp01(Mathf.Abs(x) * 1.4f));

            float r = new Vector2(x - 0.03f, y - 0.06f).magnitude;
            Color c = sclera;
            if (r < 0.4f)
            {
                // Milky, clouded over.
                c = Color.Lerp(new Color(0.42f, 0.47f, 0.5f), new Color(0.62f, 0.64f, 0.66f), Mathf.PerlinNoise(x * 20f, y * 20f) * 0.5f);
                if (r > 0.34f) c *= 0.7f;
                if (r < 0.15f) c = new Color(0.36f, 0.38f, 0.4f);
            }
            c.a = 1f;
            return c;
        }

        /// <summary>Hair card: clumped wet strands across u, thinning toward the tip at v = 1.</summary>
        static Color HairColor(float u, float v)
        {
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.18f, u)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.82f, u));
            float strands = Mathf.PerlinNoise(u * 38f, v * 1.5f) * 0.7f + Mathf.PerlinNoise(u * 90f, v * 3f) * 0.3f;
            float alpha = strands * edge > 0.3f + 0.28f * v * v ? 1f : 0f;
            float sheen = 0.02f + 0.05f * Mathf.PerlinNoise(u * 70f, v * 6f);
            return new Color(sheen, sheen * 0.95f, sheen * 1.05f, alpha);
        }

        static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093 ^ y * 19349663 ^ seed * 83492791);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        // ================================================================== Mesh building

        static BoneWeight W(int bone) => new BoneWeight { boneIndex0 = bone, weight0 = 1f };

        /// <summary>Blend from bone a (t = 0) to bone b (t = 1); the heavier bone goes first.</summary>
        static BoneWeight W(int a, int b, float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0.001f) return W(a);
            if (t >= 0.999f) return W(b);
            return t <= 0.5f
                ? new BoneWeight { boneIndex0 = a, weight0 = 1f - t, boneIndex1 = b, weight1 = t }
                : new BoneWeight { boneIndex0 = b, weight0 = t, boneIndex1 = a, weight1 = 1f - t };
        }

        struct Ring
        {
            public Vector3 center;
            public float side, front, v;
            public BoneWeight weight;

            public Ring(Vector3 center, float side, float front, BoneWeight weight, float v)
            {
                this.center = center;
                this.side = side;
                this.front = front;
                this.weight = weight;
                this.v = v;
            }
        }

        class SkinBuilder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<Vector2> pins = new List<Vector2>();
            readonly List<BoneWeight> weights = new List<BoneWeight>();
            readonly List<int>[] front;
            readonly List<int>[] back;

            public SkinBuilder(int submeshes)
            {
                front = new List<int>[submeshes];
                back = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++)
                {
                    front[i] = new List<int>();
                    back[i] = new List<int>();
                }
            }

            public int Count => verts.Count;

            /// <param name="pin">How far (m) cloth may move this vertex from its skinned position. Stored in UV2.</param>
            public int Add(Vector3 p, Vector2 uv, BoneWeight w, float pin = 0f)
            {
                verts.Add(p);
                uvs.Add(uv);
                pins.Add(new Vector2(pin, 0f));
                weights.Add(w);
                return verts.Count - 1;
            }

            /// <summary>
            /// Triangulates rows x cols vertices added in order. Rows run along the surface's
            /// length, columns around it; winding faces outward for lofts built by Loft().
            /// </summary>
            public void Grid(int start, int rows, int cols, int sub, bool doubleSided = false)
            {
                for (int r = 0; r < rows - 1; r++)
                {
                    for (int c = 0; c < cols - 1; c++)
                    {
                        int a = start + r * cols + c, bb = a + 1, cc = a + cols, d = cc + 1;
                        front[sub].AddRange(new[] { a, cc, bb, bb, cc, d });
                        if (doubleSided) back[sub].AddRange(new[] { a, bb, cc, bb, d, cc });
                    }
                }
            }

            /// <summary>
            /// Sweeps an ellipse through the rings. `frontHint` picks which way the ellipse's
            /// second axis faces; angle a = π/2 points along it.
            /// </summary>
            public void Loft(List<Ring> rings, Vector3 frontHint, int segments, int sub,
                Func<int, float, float> radialScale = null, bool doubleSided = false,
                Func<int, float, Vector3> offset = null, Func<int, float> pin = null)
            {
                int start = verts.Count;
                for (int i = 0; i < rings.Count; i++)
                {
                    Vector3 tangent = (rings[Mathf.Min(i + 1, rings.Count - 1)].center - rings[Mathf.Max(i - 1, 0)].center).normalized;
                    Vector3 f = (frontHint - tangent * Vector3.Dot(frontHint, tangent)).normalized;
                    Vector3 sideAxis = Vector3.Cross(tangent, f);
                    float ringPin = pin != null ? pin(i) : 0f;
                    for (int j = 0; j <= segments; j++)
                    {
                        float a = j / (float)segments * Mathf.PI * 2f;
                        float scale = radialScale != null ? radialScale(i, a) : 1f;
                        Vector3 p = rings[i].center + (sideAxis * Mathf.Cos(a) * rings[i].side + f * Mathf.Sin(a) * rings[i].front) * scale;
                        if (offset != null) p += offset(i, a);
                        Add(p, new Vector2(j / (float)segments, rings[i].v), rings[i].weight, ringPin);
                    }
                }
                Grid(start, rings.Count, segments + 1, sub, doubleSided);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetUVs(1, pins);
                mesh.boneWeights = weights.ToArray();
                mesh.subMeshCount = front.Length;

                // Normals from the outward faces only; inner faces reuse them.
                for (int i = 0; i < front.Length; i++) mesh.SetTriangles(front[i], i);
                mesh.RecalculateNormals();
                var normals = mesh.normals;
                for (int i = 0; i < front.Length; i++)
                {
                    var all = new List<int>(front[i]);
                    all.AddRange(back[i]);
                    mesh.SetTriangles(all, i);
                }
                mesh.normals = normals;
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
