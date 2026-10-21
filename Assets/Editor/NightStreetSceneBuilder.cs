using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HangingWoman.EditorTools
{
    /// <summary>
    /// Builds the night street scene from primitives and generated textures, then saves it to
    /// Assets/Scenes/NightStreet.unity. Run it from the "Hanging Woman" menu.
    /// Re-running rebuilds the scene from scratch (materials are updated in place).
    ///
    /// Layout (metres): the street runs along +Z. The player starts on the east (+X) sidewalk
    /// and walks north past a row of businesses. A blood trail starts on the sidewalk near the
    /// pharmacy and turns east into an alley at z = 70..75.
    /// </summary>
    public static class NightStreetSceneBuilder
    {
        const string Root = "Assets";
        const string MatDir = Root + "/Generated/Materials";
        const string TexDir = Root + "/Generated/Textures";
        const string SceneDir = Root + "/Scenes";
        public const string ScenePath = SceneDir + "/NightStreet.unity";

        const float StreetStart = -24f;
        const float StreetEnd = 144f;
        const float RoadHalfWidth = 4f;
        const float CurbWidth = 0.25f;
        const float FacadeX = 8f;
        const float SidewalkTop = 0.15f;
        const float AlleyZMin = 70f;
        const float AlleyZMax = 75f;
        const float AlleyDepth = 22f;
        const float TileLength = 6f;

        static readonly Color FogColor = new Color(0.035f, 0.042f, 0.058f);
        static readonly Color Sodium = new Color(1f, 0.68f, 0.36f);

        static System.Random rng;
        static bool urp;
        static Font font;
        static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
        static int decalIndex;

        // Shared materials
        static Material asphalt, sidewalk, curb, roadPaint, trim, windowDark, windowWarm, windowCool, windowTv;
        static Material shutterMetal, doorWood, lampPole, lampHead, puddle, alleyGround, dumpsterGreen, trashBag;
        static Material cardboard, pipeMetal, carGlass, tire, hydrantRed, alleyLampMat;
        static Material bloodDrop, bloodSplat, bloodSmear, bloodPool;
        static Material[] wallPalette;

        [MenuItem("Hanging Woman/Build Night Street Scene", priority = 0)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        [MenuItem("Hanging Woman/Open Night Street Scene", priority = 1)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { BuildFromMenu(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Build()
        {
            rng = new System.Random(1971);
            decalIndex = 0;
            matCache.Clear();
            urp = GraphicsSettings.currentRenderPipeline != null &&
                  GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal");
            font = LoadFont();

            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(TexDir);
            Directory.CreateDirectory(SceneDir);
            AssetDatabase.Refresh();

            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupRenderSettings();

            var env = new GameObject("Environment").transform;
            BuildStreet(Group("Street", env));
            BuildBuildings(Group("Buildings", env));
            BuildStreetLamps(Group("Street Lamps", env));
            BuildAlley(Group("Alley", env));
            BuildProps(Group("Props", env));
            BuildPuddles(Group("Puddles", env));
            BuildBoundaries(Group("Boundaries", env));
            BuildReflectionProbes(Group("Reflection Probes", env));
            BuildBloodTrail(Group("Blood Trail", null));

            var sky = BuildSkyLight();
            var player = BuildPlayer();
            BuildWeather(player.transform, sky);
            new GameObject("Game Settings").AddComponent<QualityBootstrap>();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            Selection.activeGameObject = player;
            Debug.Log($"[Hanging Woman] Built and saved {ScenePath} ({(urp ? "URP" : "Built-in")} materials). Press Play to walk.");
        }

        // ------------------------------------------------------------------ Render settings

        static void SetupRenderSettings()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.056f, 0.075f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.032f;
            RenderSettings.fogColor = FogColor;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 1f;
        }

        static Light BuildSkyLight()
        {
            var go = new GameObject("Moonlight (Storm Sky)");
            go.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(0.4f, 0.48f, 0.68f);
            l.intensity = 0.08f;
            l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        // ------------------------------------------------------------------ Street

        static void BuildStreet(Transform parent)
        {
            for (float z = StreetStart; z < StreetEnd; z += TileLength)
            {
                float zc = z + TileLength * 0.5f;
                Box("Road", parent, new Vector3(0f, -0.05f, zc), new Vector3(RoadHalfWidth * 2f, 0.1f, TileLength), asphalt);

                foreach (int side in new[] { 1, -1 })
                {
                    float inner = RoadHalfWidth + CurbWidth;
                    float width = FacadeX - inner;
                    Box("Sidewalk", parent, new Vector3(side * (inner + width * 0.5f), (SidewalkTop - 0.1f) * 0.5f, zc),
                        new Vector3(width, SidewalkTop + 0.1f, TileLength), sidewalk);
                }

                // Dashed center line.
                var dash = Box("Center Line", parent, new Vector3(0f, 0.002f, zc), new Vector3(0.12f, 0.004f, 3f), roadPaint, collider: false);
                NoShadows(dash);
            }

            float length = StreetEnd - StreetStart;
            float mid = (StreetStart + StreetEnd) * 0.5f;
            foreach (int side in new[] { 1, -1 })
            {
                Box("Curb", parent, new Vector3(side * (RoadHalfWidth + CurbWidth * 0.5f), (SidewalkTop - 0.1f) * 0.5f, mid),
                    new Vector3(CurbWidth, SidewalkTop + 0.1f, length), curb);
                NoShadows(Box("Edge Line", parent, new Vector3(side * (RoadHalfWidth - 0.35f), 0.002f, mid),
                    new Vector3(0.1f, 0.004f, length), roadPaint, collider: false));
            }
        }

        // ------------------------------------------------------------------ Buildings

        class Shop
        {
            public string name;
            public Color neon;
            public bool open = true;
            public Color interior = new Color(1f, 0.85f, 0.6f);
            public Color? awning;
            public LightFlicker.Mode flicker = LightFlicker.Mode.Gentle;
            public bool shutter;
            public bool signLit = true;
            public string blade;
        }

        static void BuildBuildings(Transform parent)
        {
            // East side (+X): the side the player walks on.
            const int E = 1;
            Building(parent, E, StreetStart, 0f, 14f);
            ShopFront(parent, E, 0f, 12f, 11f, 14f, new Shop
            {
                name = "LAUNDROMAT", neon = new Color(0.3f, 0.9f, 1f), interior = new Color(0.75f, 0.9f, 1f),
                flicker = LightFlicker.Mode.Gentle
            });
            ShopFront(parent, E, 12f, 22f, 15f, 14f, new Shop
            {
                name = "FOR LEASE", neon = new Color(0.6f, 0.6f, 0.6f), open = false, shutter = true, signLit = false
            });
            ShopFront(parent, E, 22f, 34f, 13f, 14f, new Shop
            {
                name = "LUCKY NOODLE", neon = new Color(1f, 0.15f, 0.1f), interior = new Color(1f, 0.7f, 0.4f),
                awning = new Color(0.45f, 0.05f, 0.04f), flicker = LightFlicker.Mode.NeonBuzz, blade = "BAR"
            });
            ShopFront(parent, E, 34f, 44f, 10f, 14f, new Shop
            {
                name = "PAWN", neon = new Color(1f, 0.8f, 0.2f), open = false, shutter = true,
                flicker = LightFlicker.Mode.Broken
            });
            ShopFront(parent, E, 44f, 56f, 16f, 14f, new Shop
            {
                name = "24HR PHARMACY", neon = new Color(0.2f, 1f, 0.4f), interior = new Color(0.9f, 1f, 0.95f),
                flicker = LightFlicker.Mode.Gentle
            });
            ShopFront(parent, E, 56f, AlleyZMin, 12f, AlleyDepth, new Shop
            {
                name = "LIQUOR", neon = new Color(1f, 0.2f, 0.6f), interior = new Color(1f, 0.6f, 0.7f),
                awning = new Color(0.08f, 0.08f, 0.1f), flicker = LightFlicker.Mode.NeonBuzz
            });
            // (alley gap)
            ShopFront(parent, E, AlleyZMax, 90f, 20f, AlleyDepth, new Shop
            {
                name = "HOTEL MIRADOR", neon = new Color(1f, 0.45f, 0.15f), interior = new Color(1f, 0.65f, 0.35f),
                flicker = LightFlicker.Mode.NeonBuzz, blade = "HOTEL"
            });
            ShopFront(parent, E, 90f, 104f, 12f, 14f, new Shop
            {
                name = "TAILOR", neon = new Color(0.5f, 0.5f, 0.55f), open = false, signLit = false
            });
            Building(parent, E, 104f, 118f, 17f);
            Building(parent, E, 118f, StreetEnd, 13f);

            // West side (-X), across the street.
            const int W = -1;
            Building(parent, W, StreetStart, -4f, 12f);
            Building(parent, W, -4f, 10f, 18f);
            ShopFront(parent, W, 10f, 24f, 12f, 14f, new Shop
            {
                name = "CHECK CASHING", neon = new Color(1f, 0.85f, 0.3f), interior = new Color(1f, 0.95f, 0.8f),
                flicker = LightFlicker.Mode.Gentle, awning = new Color(0.1f, 0.15f, 0.35f)
            });
            ShopFront(parent, W, 24f, 40f, 14f, 14f, new Shop
            {
                name = "", neon = Color.gray, open = false, shutter = true, signLit = false
            });
            ShopFront(parent, W, 40f, 54f, 11f, 14f, new Shop
            {
                name = "VIDEO", neon = new Color(0.9f, 0.2f, 1f), open = false, flicker = LightFlicker.Mode.NeonBuzz
            });
            Building(parent, W, 54f, 70f, 19f);
            ShopFront(parent, W, 70f, 86f, 13f, 14f, new Shop
            {
                name = "LAST CALL", neon = new Color(0.3f, 0.5f, 1f), interior = new Color(0.6f, 0.45f, 0.9f),
                awning = new Color(0.05f, 0.06f, 0.12f), flicker = LightFlicker.Mode.NeonBuzz, blade = "BAR"
            });
            ShopFront(parent, W, 86f, 100f, 10f, 14f, new Shop
            {
                name = "DRY CLEANING", neon = new Color(0.85f, 0.9f, 1f), open = false, shutter = true,
                flicker = LightFlicker.Mode.Broken
            });
            Building(parent, W, 100f, 120f, 16f);
            Building(parent, W, 120f, StreetEnd, 12f);
        }

        static Quaternion FacadeRotation(int side) => Quaternion.Euler(0f, side * 90f, 0f);

        /// <summary>x coordinate of a point standing `offset` metres in front of a facade.</summary>
        static float FrontX(int side, float offset) => side * (FacadeX - offset);

        static Transform Building(Transform parent, int side, float z0, float z1, float height, float depth = 14f)
        {
            var b = Group($"Building z{z0:0}-{z1:0}", parent);
            float w = z1 - z0;
            float zc = (z0 + z1) * 0.5f;
            var wall = wallPalette[rng.Next(wallPalette.Length)];

            Box("Block", b, new Vector3(side * (FacadeX + depth * 0.5f), height * 0.5f, zc), new Vector3(depth, height, w), wall);
            Box("Cornice", b, new Vector3(FrontX(side, 0.15f), height - 0.25f, zc), new Vector3(0.3f, 0.35f, w), trim);
            Box("Floor Ledge", b, new Vector3(FrontX(side, 0.08f), 4.3f, zc), new Vector3(0.16f, 0.15f, w), trim);

            // Upper floor windows; most are dark at this hour.
            int columns = Mathf.Max(1, Mathf.FloorToInt((w - 1f) / 2.6f));
            float spacing = (w - 1f) / columns;
            for (float y = 5.6f; y < height - 1.6f; y += 3.1f)
            {
                for (int c = 0; c < columns; c++)
                {
                    float z = z0 + 0.5f + spacing * (c + 0.5f);
                    double roll = rng.NextDouble();
                    Material m = roll < 0.12 ? windowWarm : roll < 0.16 ? windowCool : roll < 0.18 ? windowTv : windowDark;
                    var q = Quad("Window", b, new Vector3(FrontX(side, 0.01f), y, z), FacadeRotation(side), new Vector2(1.1f, 1.6f), m);
                    NoShadows(q);
                    Box("Sill", b, new Vector3(FrontX(side, 0.06f), y - 0.85f, z), new Vector3(0.12f, 0.08f, 1.3f), trim, collider: false);
                    if (m == windowTv)
                    {
                        var tv = PointLight("TV Glow", b, new Vector3(FrontX(side, 0.6f), y, z), new Color(0.5f, 0.65f, 1f), 0.6f, 4f);
                        Flicker(tv.gameObject, LightFlicker.Mode.Gentle, new[] { tv });
                    }
                }
            }
            return b;
        }

        static void ShopFront(Transform parent, int side, float z0, float z1, float height, float depth, Shop shop)
        {
            var b = Building(parent, side, z0, z1, height, depth);
            b.name = string.IsNullOrEmpty(shop.name) ? b.name : shop.name;
            float y0 = SidewalkTop;

            // Display window runs from the south end to just before the door at the north end.
            float winZ0 = z0 + 0.7f, winZ1 = z1 - 2.6f;
            float winZc = (winZ0 + winZ1) * 0.5f, winW = winZ1 - winZ0;
            Material glass = shop.open ? Mat("Interior " + shop.name, shop.interior * 0.15f, 0.95f, 0f, shop.interior, 0.9f) : windowDark;
            NoShadows(Quad("Display Window", b, new Vector3(FrontX(side, 0.02f), y0 + 1.45f, winZc), FacadeRotation(side),
                new Vector2(winW, 2.1f), glass));
            Box("Window Frame Top", b, new Vector3(FrontX(side, 0.05f), y0 + 2.55f, winZc), new Vector3(0.1f, 0.1f, winW + 0.2f), trim);
            Box("Window Base", b, new Vector3(FrontX(side, 0.05f), y0 + 0.2f, winZc), new Vector3(0.1f, 0.4f, winW + 0.2f), trim);

            Material doorMat = shop.open ? Mat("Door " + shop.name, shop.interior * 0.1f, 0.95f, 0f, shop.interior, 0.5f) : doorWood;
            NoShadows(Quad("Door", b, new Vector3(FrontX(side, 0.02f), y0 + 1.15f, z1 - 1.5f), FacadeRotation(side),
                new Vector2(1.1f, 2.3f), doorMat));
            Box("Door Frame", b, new Vector3(FrontX(side, 0.05f), y0 + 2.35f, z1 - 1.5f), new Vector3(0.1f, 0.1f, 1.3f), trim);

            if (shop.shutter)
            {
                Box("Roller Shutter", b, new Vector3(FrontX(side, 0.12f), y0 + 1.3f, winZc), new Vector3(0.06f, 2.5f, winW + 0.3f), shutterMetal);
                Box("Shutter Housing", b, new Vector3(FrontX(side, 0.16f), y0 + 2.7f, winZc), new Vector3(0.25f, 0.3f, winW + 0.4f), shutterMetal);
            }

            if (shop.awning.HasValue)
            {
                var awningMat = Mat("Awning " + shop.name, shop.awning.Value, 0.55f);
                Box("Awning", b, new Vector3(FrontX(side, 0.8f), 3.05f, (z0 + z1) * 0.5f), new Vector3(1.6f, 0.06f, z1 - z0 - 0.8f),
                    awningMat, euler: new Vector3(0f, 0f, side * 16f));
            }

            if (shop.open)
            {
                PointLight("Window Spill", b, new Vector3(FrontX(side, 0.9f), 1.7f, winZc), shop.interior, 1.1f, 6f);
            }

            if (!string.IsNullOrEmpty(shop.name)) BuildSign(b, side, z0, z1, shop);
            if (!string.IsNullOrEmpty(shop.blade)) BuildBladeSign(b, side, z1 - 0.6f, height, shop);
        }

        static void BuildSign(Transform parent, int side, float z0, float z1, Shop shop)
        {
            float zc = (z0 + z1) * 0.5f;
            float y = shop.awning.HasValue ? 3.9f : 3.4f;
            var sign = Group("Sign " + shop.name, parent);
            Box("Sign Board", sign, new Vector3(FrontX(side, 0.05f), y, zc), new Vector3(0.1f, 0.85f, z1 - z0 - 1.6f), trim);

            Color textColor = shop.signLit ? shop.neon : shop.neon * 0.35f;
            float maxWidth = z1 - z0 - 2.4f;
            float charSize = Mathf.Min(0.055f, maxWidth / (shop.name.Length * 6.2f));
            var text = Text(shop.name, sign, new Vector3(FrontX(side, 0.11f), y, zc), FacadeRotation(side), charSize, textColor);

            if (!shop.signLit) return;
            var glow = PointLight("Neon Glow", sign, new Vector3(FrontX(side, 0.7f), y, zc), shop.neon, 1.4f, 6.5f);
            if (shop.flicker != LightFlicker.Mode.Steady)
                Flicker(sign.gameObject, shop.flicker, new[] { glow }, texts: new[] { text });
        }

        static void BuildBladeSign(Transform parent, int side, float z, float buildingHeight, Shop shop)
        {
            var sign = Group("Blade Sign " + shop.blade, parent);
            float height = 0.75f * shop.blade.Length + 0.4f;
            float y = Mathf.Min(buildingHeight - height * 0.5f - 0.6f, 4.9f + height * 0.5f);
            float x = FrontX(side, 0.75f);
            Box("Bracket", sign, new Vector3(FrontX(side, 0.35f), y, z), new Vector3(0.7f, 0.08f, 0.08f), lampPole);
            Box("Board", sign, new Vector3(x, y, z), new Vector3(0.75f, height, 0.14f), trim);

            string vertical = string.Join("\n", shop.blade.ToCharArray());
            var front = Text(vertical, sign, new Vector3(x, y, z - 0.08f), Quaternion.identity, 0.06f, shop.neon);
            var back = Text(vertical, sign, new Vector3(x, y, z + 0.08f), Quaternion.Euler(0f, 180f, 0f), 0.06f, shop.neon);
            front.lineSpacing = back.lineSpacing = 0.95f;

            var glow = PointLight("Blade Glow", sign, new Vector3(x, y, z - 0.6f), shop.neon, 1.2f, 6f);
            Flicker(sign.gameObject, shop.flicker == LightFlicker.Mode.Steady ? LightFlicker.Mode.Gentle : shop.flicker,
                new[] { glow }, texts: new[] { front, back });
        }

        // ------------------------------------------------------------------ Street lamps

        static void BuildStreetLamps(Transform parent)
        {
            for (float z = 6f; z < 130f; z += 16f)
            {
                // The lamp just before the alley is dying.
                bool broken = Mathf.Approximately(z, 70f);
                StreetLamp(parent, 1, broken ? 66f : z, broken ? LightFlicker.Mode.Broken : LightFlicker.Mode.Steady);
            }
            for (float z = 14f; z < 130f; z += 16f)
                StreetLamp(parent, -1, z, Mathf.Approximately(z, 94f) ? LightFlicker.Mode.Broken : LightFlicker.Mode.Steady);
        }

        static void StreetLamp(Transform parent, int side, float z, LightFlicker.Mode mode)
        {
            var lamp = Group($"Street Lamp {(side > 0 ? "E" : "W")} z{z:0}", parent);
            float x = side * (RoadHalfWidth + 0.6f);
            Prim(PrimitiveType.Cylinder, "Pole", lamp, new Vector3(x, SidewalkTop + 3f, z), new Vector3(0.14f, 3f, 0.14f), lampPole);
            Prim(PrimitiveType.Cylinder, "Base", lamp, new Vector3(x, SidewalkTop + 0.3f, z), new Vector3(0.26f, 0.3f, 0.26f), lampPole);
            Box("Arm", lamp, new Vector3(x - side * 0.7f, SidewalkTop + 6.05f, z), new Vector3(1.5f, 0.09f, 0.09f), lampPole, collider: false);
            var head = Box("Head", lamp, new Vector3(x - side * 1.4f, SidewalkTop + 5.95f, z), new Vector3(0.65f, 0.16f, 0.35f), lampHead, collider: false);
            NoShadows(head);

            var spot = new GameObject("Lamp Light").AddComponent<Light>();
            spot.transform.SetParent(lamp, false);
            spot.transform.localPosition = new Vector3(x - side * 1.4f, SidewalkTop + 5.8f, z);
            spot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            spot.type = LightType.Spot;
            spot.spotAngle = 115f;
            spot.range = 14f;
            spot.intensity = 2.6f;
            spot.color = Sodium;
            spot.shadows = LightShadows.None;
            spot.lightmapBakeType = LightmapBakeType.Realtime;

            if (mode != LightFlicker.Mode.Steady)
            {
                var f = Flicker(lamp.gameObject, mode, new[] { spot }, new Renderer[] { head.GetComponent<Renderer>() });
                f.emissionColor = Sodium * 2f;
            }
        }

        // ------------------------------------------------------------------ Alley

        static void BuildAlley(Transform parent)
        {
            float zc = (AlleyZMin + AlleyZMax) * 0.5f;
            float width = AlleyZMax - AlleyZMin;
            float end = FacadeX + AlleyDepth;

            Box("Alley Ground", parent, new Vector3(FacadeX + AlleyDepth * 0.5f, (SidewalkTop - 0.1f) * 0.5f, zc),
                new Vector3(AlleyDepth, SidewalkTop + 0.1f, width), alleyGround);
            Box("Back Wall", parent, new Vector3(end + 0.25f, 5f, zc), new Vector3(0.5f, 10f, width + 0.2f), wallPalette[0]);
            Box("Drain Channel", parent, new Vector3(FacadeX + AlleyDepth * 0.5f, SidewalkTop + 0.002f, zc), new Vector3(AlleyDepth, 0.004f, 0.25f), pipeMetal, collider: false);

            // Dumpster against the north wall, the trail passes right by it.
            var dumpster = Group("Dumpster", parent);
            Box("Bin", dumpster, new Vector3(18.5f, SidewalkTop + 0.65f, AlleyZMax - 0.62f), new Vector3(1.9f, 1.3f, 1.1f), dumpsterGreen);
            Box("Lid", dumpster, new Vector3(18.5f, SidewalkTop + 1.34f, AlleyZMax - 0.68f), new Vector3(1.95f, 0.06f, 1.2f), dumpsterGreen,
                euler: new Vector3(-8f, 0f, 0f));

            // Trash bags and boxes.
            Vector3[] bags =
            {
                new Vector3(20f, 0.32f, AlleyZMax - 0.45f), new Vector3(20.6f, 0.28f, AlleyZMax - 0.9f),
                new Vector3(16.9f, 0.3f, AlleyZMax - 0.4f), new Vector3(12.5f, 0.27f, AlleyZMin + 0.45f)
            };
            foreach (var p in bags)
                Prim(PrimitiveType.Sphere, "Trash Bag", parent, p + Vector3.up * SidewalkTop * 0.5f,
                    new Vector3(R(0.6f, 0.8f), R(0.5f, 0.65f), R(0.55f, 0.75f)), trashBag);

            Box("Box", parent, new Vector3(23.5f, SidewalkTop + 0.3f, AlleyZMin + 0.45f), new Vector3(0.7f, 0.6f, 0.6f), cardboard, euler: new Vector3(0f, 12f, 0f));
            Box("Box", parent, new Vector3(23.6f, SidewalkTop + 0.8f, AlleyZMin + 0.42f), new Vector3(0.5f, 0.4f, 0.5f), cardboard, euler: new Vector3(0f, -20f, 0f));
            Box("Box (Flattened)", parent, new Vector3(14.5f, SidewalkTop + 0.01f, AlleyZMin + 1.2f), new Vector3(1.1f, 0.02f, 0.7f), cardboard, euler: new Vector3(0f, 30f, 0f));

            // Pipes and an AC unit on the walls.
            Prim(PrimitiveType.Cylinder, "Drain Pipe", parent, new Vector3(13f, 5f, AlleyZMin + 0.12f), new Vector3(0.12f, 5f, 0.12f), pipeMetal);
            Prim(PrimitiveType.Cylinder, "Drain Pipe", parent, new Vector3(25f, 5f, AlleyZMax - 0.12f), new Vector3(0.12f, 5f, 0.12f), pipeMetal);
            Box("AC Unit", parent, new Vector3(16f, 3.6f, AlleyZMin + 0.35f), new Vector3(0.9f, 0.6f, 0.7f), shutterMetal);

            // Back door and the single caged lamp at the end of the alley.
            Box("Back Door", parent, new Vector3(26.2f, SidewalkTop + 1.1f, AlleyZMax - 0.04f), new Vector3(1.1f, 2.2f, 0.08f), shutterMetal);
            var lampBox = Box("Wall Lamp", parent, new Vector3(26.2f, 3.0f, AlleyZMax - 0.12f), new Vector3(0.4f, 0.22f, 0.22f), alleyLampMat, collider: false);
            NoShadows(lampBox);
            var light = PointLight("Alley Light", parent, new Vector3(26.2f, 2.85f, AlleyZMax - 0.5f), new Color(0.75f, 0.85f, 0.75f), 1.3f, 9f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            var f = Flicker(lampBox, LightFlicker.Mode.NeonBuzz, new[] { light }, new Renderer[] { lampBox.GetComponent<Renderer>() });
            f.emissionColor = new Color(0.75f, 0.85f, 0.75f) * 1.5f;

            // Trigger volume + an anchor marking where the trail ends, for the next scene beat.
            var trigger = new GameObject("Alley Trigger");
            trigger.layer = 2;
            trigger.transform.SetParent(parent, false);
            trigger.transform.position = new Vector3(FacadeX + 0.5f + (AlleyDepth - 0.5f) * 0.5f, 1.5f, zc);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(AlleyDepth - 0.5f, 3f, width);
            trigger.AddComponent<AlleyTrigger>();

            var anchor = new GameObject("Trail End (Event Anchor)");
            anchor.transform.SetParent(parent, false);
            anchor.transform.position = new Vector3(27.3f, SidewalkTop, 73.3f);
        }

        // ------------------------------------------------------------------ Blood trail

        static void BuildBloodTrail(Transform parent)
        {
            // Sparse drips on the sidewalk outside the pharmacy, getting heavier as the
            // trail turns into the alley and ends in a pool by the back door.
            Vector2[] path =
            {
                new Vector2(6.3f, 53.5f), new Vector2(6.8f, 58f), new Vector2(6.4f, 62.5f), new Vector2(7.0f, 67f),
                new Vector2(7.6f, 70.6f), new Vector2(9.6f, 72.2f), new Vector2(13.5f, 72.7f), new Vector2(17f, 72.1f),
                new Vector2(20.5f, 72.5f), new Vector2(24f, 73.0f), new Vector2(26.6f, 73.4f)
            };

            float total = 0f;
            for (int i = 1; i < path.Length; i++) total += Vector2.Distance(path[i - 1], path[i]);

            float travelled = 0f, nextDrop = 0f, nextSmear = 0f;
            for (int i = 1; i < path.Length; i++)
            {
                Vector2 a = path[i - 1], b = path[i];
                Vector2 dir = (b - a).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x);
                float seg = Vector2.Distance(a, b);

                for (float d = 0f; d < seg; d += 0.05f)
                {
                    float t = (travelled + d) / total;
                    Vector2 p = a + dir * d;
                    bool inAlley = p.x > FacadeX + 0.5f;

                    if (travelled + d >= nextDrop)
                    {
                        Vector2 jitter = perp * R(-0.18f, 0.18f) + dir * R(-0.1f, 0.1f);
                        float size = Mathf.Lerp(0.07f, 0.16f, t) * R(0.7f, 1.3f);
                        FloorDecal(parent, p + jitter, size, R(0f, 360f), bloodDrop);

                        if (rng.NextDouble() < Mathf.Lerp(0.04f, 0.35f, t))
                            FloorDecal(parent, p + perp * R(-0.3f, 0.3f), R(0.25f, 0.5f) * Mathf.Lerp(0.8f, 1.4f, t), R(0f, 360f), bloodSplat);

                        nextDrop = travelled + d + Mathf.Lerp(1.1f, 0.3f, t) * R(0.6f, 1.4f);
                    }

                    // Once in the alley something is being dragged.
                    if (inAlley && travelled + d >= nextSmear)
                    {
                        float length = R(0.6f, 1.1f);
                        Vector2 along = dir + perp * R(-0.15f, 0.15f);
                        FloorDecal(parent, p + perp * R(-0.08f, 0.08f), new Vector2(length, R(0.22f, 0.35f)), along, bloodSmear);
                        nextSmear = travelled + d + Mathf.Lerp(1.6f, 0.6f, t) * R(0.7f, 1.3f);
                    }
                }
                travelled += seg;
            }

            Vector2 endPos = path[path.Length - 1];
            FloorDecal(parent, endPos, 1.7f, R(0f, 360f), bloodPool);
            FloorDecal(parent, endPos + new Vector2(0.5f, -0.4f), 0.8f, R(0f, 360f), bloodSplat);
            for (int i = 0; i < 10; i++)
                FloorDecal(parent, endPos + new Vector2(R(-1.2f, 1.2f), R(-1.2f, 1.0f)), R(0.06f, 0.15f), R(0f, 360f), bloodDrop);

            // Smeared down the wall beside the back door, like someone slid against it.
            var wallSmear = Quad("Blood Smear (Wall)", parent, new Vector3(27.4f, SidewalkTop + 0.85f, AlleyZMax - 0.008f),
                Quaternion.Euler(0f, 0f, 84f), new Vector2(1.5f, 0.42f), bloodSmear);
            NoShadows(wallSmear);
            var handprint = Quad("Blood Splat (Wall)", parent, new Vector3(27.5f, SidewalkTop + 1.55f, AlleyZMax - 0.009f),
                Quaternion.Euler(0f, 0f, R(0f, 360f)), new Vector2(0.35f, 0.35f), bloodSplat);
            NoShadows(handprint);
            // A few drops leading off the curb onto the sidewalk where it starts, easy to miss.
            FloorDecal(parent, new Vector2(5.4f, 51.8f), 0.07f, 0f, bloodDrop);
            FloorDecal(parent, new Vector2(5.9f, 52.7f), 0.09f, 0f, bloodDrop);
        }

        static void FloorDecal(Transform parent, Vector2 xz, float size, float yaw, Material m) =>
            FloorDecal(parent, xz, new Vector2(size, size), Quaternion.Euler(90f, yaw, 0f), m);

        static void FloorDecal(Transform parent, Vector2 xz, Vector2 size, Vector2 alongXZ, Material m)
        {
            // After Euler(90, yaw, 0) a quad's local X points along (cos yaw, 0, -sin yaw).
            float yaw = Mathf.Atan2(-alongXZ.y, alongXZ.x) * Mathf.Rad2Deg;
            FloorDecal(parent, xz, size, Quaternion.Euler(90f, yaw, 0f), m);
        }

        static void FloorDecal(Transform parent, Vector2 xz, Vector2 size, Quaternion rotation, Material m)
        {
            // Stagger heights a hair so overlapping decals don't z-fight.
            float y = SidewalkTop + 0.003f + (decalIndex++ % 16) * 0.0004f;
            var q = Quad(m.name, parent, new Vector3(xz.x, y, xz.y), rotation, size, m);
            NoShadows(q);
        }

        // ------------------------------------------------------------------ Props

        static void BuildProps(Transform parent)
        {
            ParkedCar(parent, new Vector3(-2.9f, 0f, 30f), Mat("Car Paint Navy", new Color(0.04f, 0.06f, 0.12f), 0.9f, 0.6f));
            ParkedCar(parent, new Vector3(-2.9f, 0f, 97f), Mat("Car Paint Maroon", new Color(0.18f, 0.03f, 0.04f), 0.9f, 0.6f));
            ParkedCar(parent, new Vector3(2.9f, 0f, 112f), Mat("Car Paint Grey", new Color(0.2f, 0.2f, 0.21f), 0.85f, 0.6f), 180f);

            Hydrant(parent, new Vector3(RoadHalfWidth + 0.75f, SidewalkTop, 47f));
            Hydrant(parent, new Vector3(-(RoadHalfWidth + 0.75f), SidewalkTop, 84f));

            var canMat = Mat("Trash Can", new Color(0.12f, 0.13f, 0.12f), 0.6f, 0.7f);
            Prim(PrimitiveType.Cylinder, "Trash Can", parent, new Vector3(RoadHalfWidth + 0.8f, SidewalkTop + 0.45f, 11f), new Vector3(0.55f, 0.45f, 0.55f), canMat);
            Prim(PrimitiveType.Cylinder, "Trash Can", parent, new Vector3(-(RoadHalfWidth + 0.8f), SidewalkTop + 0.45f, 26f), new Vector3(0.55f, 0.45f, 0.55f), canMat);

            // Newspaper box outside the pharmacy.
            Box("Newspaper Box", parent, new Vector3(7.3f, SidewalkTop + 0.55f, 45.2f), new Vector3(0.5f, 1.1f, 0.5f), Mat("Newspaper Box", new Color(0.05f, 0.15f, 0.35f), 0.7f));
        }

        static void ParkedCar(Transform parent, Vector3 pos, Material paint, float yaw = 0f)
        {
            var car = Group("Parked Car", parent);
            car.position = pos;
            car.rotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Body", car, new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.65f, 4.4f), paint);
            Box("Cabin", car, new Vector3(0f, 1.18f, -0.25f), new Vector3(1.6f, 0.55f, 2.2f), carGlass);
            Box("Roof", car, new Vector3(0f, 1.47f, -0.25f), new Vector3(1.55f, 0.05f, 2.0f), paint, collider: false);
            var tail = Mat("Tail Light", new Color(0.3f, 0.02f, 0.02f), 0.9f, 0f, new Color(0.6f, 0.02f, 0.02f), 0.4f);
            Box("Tail Light L", car, new Vector3(-0.65f, 0.7f, -2.21f), new Vector3(0.35f, 0.12f, 0.03f), tail, collider: false);
            Box("Tail Light R", car, new Vector3(0.65f, 0.7f, -2.21f), new Vector3(0.35f, 0.12f, 0.03f), tail, collider: false);
            foreach (float x in new[] { -0.85f, 0.85f })
                foreach (float z in new[] { -1.35f, 1.35f })
                    Prim(PrimitiveType.Cylinder, "Wheel", car, new Vector3(x, 0.33f, z), new Vector3(0.66f, 0.12f, 0.66f), tire,
                        euler: new Vector3(0f, 0f, 90f), collider: false);
        }

        static void Hydrant(Transform parent, Vector3 pos)
        {
            var h = Group("Hydrant", parent);
            Prim(PrimitiveType.Cylinder, "Body", h, pos + Vector3.up * 0.35f, new Vector3(0.24f, 0.35f, 0.24f), hydrantRed);
            Prim(PrimitiveType.Sphere, "Cap", h, pos + Vector3.up * 0.72f, new Vector3(0.24f, 0.18f, 0.24f), hydrantRed, collider: false);
            Prim(PrimitiveType.Cylinder, "Nozzle", h, pos + Vector3.up * 0.48f, new Vector3(0.1f, 0.2f, 0.1f), hydrantRed,
                euler: new Vector3(90f, 0f, 0f), collider: false);
        }

        static void BuildPuddles(Transform parent)
        {
            // Road puddles, with long gutter puddles along both curbs.
            for (int i = 0; i < 34; i++)
                Puddle(parent, new Vector3(R(-3.4f, 3.4f), 0.002f, R(StreetStart + 4f, StreetEnd - 10f)), R(0.8f, 2.8f), R(0.6f, 1.8f));
            for (int i = 0; i < 16; i++)
            {
                int side = i % 2 == 0 ? 1 : -1;
                Puddle(parent, new Vector3(side * (RoadHalfWidth - 0.35f), 0.003f, R(StreetStart + 4f, StreetEnd - 10f)), 0.6f, R(2f, 5f), 0f);
            }
            for (int i = 0; i < 12; i++)
            {
                int side = i % 2 == 0 ? 1 : -1;
                Puddle(parent, new Vector3(side * R(5f, 7.5f), SidewalkTop + 0.002f, R(StreetStart + 4f, StreetEnd - 10f)), R(0.5f, 1.3f), R(0.4f, 1f));
            }
            for (int i = 0; i < 6; i++)
                Puddle(parent, new Vector3(R(10f, 28f), SidewalkTop + 0.002f, R(AlleyZMin + 1f, AlleyZMax - 1f)), R(0.7f, 1.8f), R(0.5f, 1.2f));
        }

        static void Puddle(Transform parent, Vector3 pos, float sx, float sz, float? yaw = null)
        {
            var p = Prim(PrimitiveType.Cylinder, "Puddle", parent, pos, new Vector3(sx, 0.001f, sz), puddle,
                euler: new Vector3(0f, yaw ?? R(0f, 180f), 0f), collider: false);
            NoShadows(p);
        }

        static void BuildBoundaries(Transform parent)
        {
            InvisibleWall("Boundary South", parent, new Vector3(0f, 2f, -6f), new Vector3(FacadeX * 2f, 4f, 0.5f));
            InvisibleWall("Boundary North", parent, new Vector3(0f, 2f, 128f), new Vector3(FacadeX * 2f, 4f, 0.5f));
        }

        static void BuildReflectionProbes(Transform parent)
        {
            for (float z = -6f; z <= 130f; z += 20f)
                Probe(parent, new Vector3(0f, 3f, z), new Vector3(FacadeX * 2f + 2f, 14f, 22f));
            Probe(parent, new Vector3(FacadeX + AlleyDepth * 0.5f, 3f, (AlleyZMin + AlleyZMax) * 0.5f), new Vector3(AlleyDepth, 10f, 5.5f));
        }

        static void Probe(Transform parent, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("Reflection Probe");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Realtime;
            p.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            p.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            p.size = size;
            p.resolution = 128;
            p.hdr = true;
            p.boxProjection = true;
            p.backgroundColor = FogColor;
            p.clearFlags = ReflectionProbeClearFlags.SolidColor;
        }

        // ------------------------------------------------------------------ Player & weather

        static GameObject BuildPlayer()
        {
            var player = new GameObject("Player");
            player.tag = "Player";
            player.layer = 2; // Ignore Raycast: keeps rain from splashing on the player's own capsule.
            player.transform.position = new Vector3(6.1f, SidewalkTop + 0.02f, 2f);

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;

            var root = new GameObject("Camera Root").transform;
            root.SetParent(player.transform, false);
            root.localPosition = new Vector3(0f, 1.65f, 0f);

            var camGo = new GameObject("Player Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(root, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 160f;
            cam.fieldOfView = 68f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();

            var flash = new GameObject("Flashlight").AddComponent<Light>();
            flash.transform.SetParent(camGo.transform, false);
            flash.transform.localPosition = new Vector3(0.18f, -0.18f, 0.1f);
            flash.type = LightType.Spot;
            flash.range = 22f;
            flash.spotAngle = 55f;
            flash.innerSpotAngle = 25f;
            flash.intensity = 1.8f;
            flash.color = new Color(1f, 0.95f, 0.85f);
            flash.shadows = LightShadows.Soft;
            flash.lightmapBakeType = LightmapBakeType.Realtime;
            flash.enabled = false;

            var fpc = player.AddComponent<FirstPersonController>();
            fpc.cameraRoot = root;
            fpc.playerCamera = cam;
            fpc.flashlight = flash;
            return player;
        }

        static void BuildWeather(Transform player, Light sky)
        {
            var weather = new GameObject("Weather");
            var rain = weather.AddComponent<RainSystem>();
            rain.follow = player;
            rain.rainMaterial = ParticleMat("Rain Streak", SoftDotTexture("rain_drop"), new Color(0.6f, 0.65f, 0.75f, 0.35f));
            rain.splashMaterial = ParticleMat("Rain Splash", SoftDotTexture("rain_drop"), new Color(0.7f, 0.75f, 0.85f, 0.45f));
            var storm = weather.AddComponent<LightningStorm>();
            storm.skyLight = sky;
        }

        // ------------------------------------------------------------------ Materials & textures

        static void CreateMaterials()
        {
            var asphaltTex = NoiseTexture("asphalt", 256, 0.55f, 1f, 24, specks: true);
            var slabTex = SlabTexture("sidewalk_slab", 256);

            asphalt = Mat("Asphalt Wet", new Color(0.07f, 0.07f, 0.075f), 0.86f, 0f, tex: asphaltTex, tiling: new Vector2(4f, 3f));
            sidewalk = Mat("Sidewalk Wet", new Color(0.22f, 0.22f, 0.23f), 0.68f, 0f, tex: slabTex, tiling: new Vector2(3f, 4f));
            alleyGround = Mat("Alley Concrete Wet", new Color(0.12f, 0.12f, 0.12f), 0.78f, 0f, tex: asphaltTex, tiling: new Vector2(6f, 1.5f));
            curb = Mat("Curb", new Color(0.3f, 0.3f, 0.3f), 0.55f);
            roadPaint = Mat("Road Paint", new Color(0.55f, 0.45f, 0.15f), 0.75f);
            trim = Mat("Trim Dark", new Color(0.04f, 0.04f, 0.045f), 0.45f, 0.2f);

            wallPalette = new[]
            {
                Mat("Wall Brick Red", new Color(0.24f, 0.1f, 0.08f), 0.35f),
                Mat("Wall Brick Brown", new Color(0.17f, 0.12f, 0.09f), 0.35f),
                Mat("Wall Concrete", new Color(0.2f, 0.2f, 0.21f), 0.4f),
                Mat("Wall Stucco", new Color(0.23f, 0.21f, 0.17f), 0.3f),
                Mat("Wall Green", new Color(0.1f, 0.14f, 0.12f), 0.35f),
            };

            windowDark = Mat("Window Dark", new Color(0.015f, 0.017f, 0.02f), 0.96f, 0.1f);
            windowWarm = Mat("Window Lit Warm", new Color(0.1f, 0.07f, 0.04f), 0.9f, 0f, new Color(1f, 0.72f, 0.42f), 0.9f);
            windowCool = Mat("Window Lit Cool", new Color(0.06f, 0.07f, 0.1f), 0.9f, 0f, new Color(0.65f, 0.75f, 1f), 0.6f);
            windowTv = Mat("Window TV", new Color(0.03f, 0.04f, 0.08f), 0.9f, 0f, new Color(0.4f, 0.55f, 1f), 0.5f);

            shutterMetal = Mat("Shutter Metal", new Color(0.25f, 0.25f, 0.26f), 0.55f, 0.7f);
            doorWood = Mat("Door Dark", new Color(0.06f, 0.04f, 0.03f), 0.5f);
            lampPole = Mat("Lamp Pole", new Color(0.06f, 0.07f, 0.06f), 0.6f, 0.8f);
            lampHead = Mat("Lamp Head", new Color(0.3f, 0.2f, 0.1f), 0.7f, 0f, Sodium, 2f);
            alleyLampMat = Mat("Alley Lamp", new Color(0.3f, 0.35f, 0.3f), 0.7f, 0f, new Color(0.75f, 0.85f, 0.75f), 1.5f);
            puddle = Mat("Puddle", new Color(0.01f, 0.011f, 0.014f), 0.985f, 0f);
            dumpsterGreen = Mat("Dumpster", new Color(0.05f, 0.14f, 0.07f), 0.6f, 0.5f);
            trashBag = Mat("Trash Bag", new Color(0.015f, 0.015f, 0.015f), 0.9f);
            cardboard = Mat("Cardboard Wet", new Color(0.25f, 0.17f, 0.09f), 0.3f);
            pipeMetal = Mat("Pipe Metal", new Color(0.12f, 0.12f, 0.12f), 0.65f, 0.8f);
            carGlass = Mat("Car Glass", new Color(0.02f, 0.025f, 0.03f), 0.97f, 0.3f);
            tire = Mat("Tire", new Color(0.02f, 0.02f, 0.02f), 0.3f);
            hydrantRed = Mat("Hydrant", new Color(0.4f, 0.04f, 0.03f), 0.7f, 0.2f);

            // Blood: near-black red, very glossy because it's fresh and rained on.
            var bloodColor = new Color(0.32f, 0.01f, 0.012f);
            bloodDrop = Cutout(Mat("Blood Drop", bloodColor, 0.92f, 0f, tex: BloodTexture("blood_drop", BloodShape.Drop, 11)));
            bloodSplat = Cutout(Mat("Blood Splatter", bloodColor, 0.9f, 0f, tex: BloodTexture("blood_splatter", BloodShape.Splatter, 23)));
            bloodSmear = Cutout(Mat("Blood Smear", bloodColor * 0.85f, 0.88f, 0f, tex: BloodTexture("blood_smear", BloodShape.Smear, 37)));
            bloodPool = Cutout(Mat("Blood Pool", bloodColor * 0.8f, 0.96f, 0f, tex: BloodTexture("blood_pool", BloodShape.Pool, 51)));
        }

        static Material Mat(string name, Color albedo, float smoothness, float metallic = 0f, Color? emission = null,
            float emissionIntensity = 1f, Texture2D tex = null, Vector2? tiling = null)
        {
            string safe = SafeName(name);
            if (matCache.TryGetValue(safe, out var cached)) return cached;

            string path = $"{MatDir}/{safe}.mat";
            var shader = urp ? Shader.Find("Universal Render Pipeline/Lit") : null;
            if (shader == null) shader = Shader.Find("Standard");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader);
            else mat.shader = shader;
            mat.name = safe;

            SetColor(mat, albedo);
            mat.SetFloat(urp ? "_Smoothness" : "_Glossiness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (tex != null)
            {
                string texProp = urp ? "_BaseMap" : "_MainTex";
                mat.SetTexture(texProp, tex);
                mat.SetTextureScale(texProp, tiling ?? Vector2.one);
            }
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value * emissionIntensity);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            matCache[safe] = mat;
            return mat;
        }

        static void SetColor(Material mat, Color c)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }

        static Material Cutout(Material m, float cutoff = 0.5f)
        {
            m.SetFloat("_Cutoff", cutoff);
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.EnableKeyword("_ALPHATEST_ON");
            if (urp)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Surface", 0f);
            }
            else
            {
                m.SetFloat("_Mode", 1f);
                m.SetInt("_SrcBlend", (int)BlendMode.One);
                m.SetInt("_DstBlend", (int)BlendMode.Zero);
                m.SetInt("_ZWrite", 1);
                m.DisableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material ParticleMat(string name, Texture2D tex, Color tint)
        {
            string safe = SafeName(name);
            string path = $"{MatDir}/{safe}.mat";
            Shader shader = urp ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader);
            else mat.shader = shader;

            if (urp)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", tint);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", tint);
                else mat.color = tint;
            }

            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D SaveTexture(string name, int size, Func<int, int, Color> pixel, bool alpha, TextureWrapMode wrap)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = pixel(x, y);
            t.SetPixels(px);
            t.Apply();

            string path = $"{TexDir}/{name}.png";
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = alpha;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D NoiseTexture(string name, int size, float min, float max, int period, bool specks)
        {
            var specRng = new System.Random(name.GetHashCode());
            var speckSet = new HashSet<int>();
            if (specks)
                for (int i = 0; i < size * size / 60; i++) speckSet.Add(specRng.Next(size * size));

            return SaveTexture(name, size, (x, y) =>
            {
                float u = x / (float)size, v = y / (float)size;
                float n = Fbm(u, v, period, 4, 3);
                float value = Mathf.Lerp(min, max, n);
                if (speckSet.Contains(y * size + x)) value *= 1.35f;
                return new Color(value, value, value, 1f);
            }, false, TextureWrapMode.Repeat);
        }

        static Texture2D SlabTexture(string name, int size)
        {
            return SaveTexture(name, size, (x, y) =>
            {
                float u = x / (float)size, v = y / (float)size;
                float n = Mathf.Lerp(0.7f, 1f, Fbm(u, v, 16, 4, 9));
                int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                if (edge < 3) n *= 0.35f;
                else if (edge < 6) n *= 0.75f;
                return new Color(n, n, n, 1f);
            }, false, TextureWrapMode.Repeat);
        }

        static Texture2D SoftDotTexture(string name)
        {
            const int size = 64;
            return SaveTexture(name, size, (x, y) =>
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                a = a * a;
                return new Color(1f, 1f, 1f, a);
            }, true, TextureWrapMode.Clamp);
        }

        enum BloodShape { Drop, Splatter, Smear, Pool }

        struct Blob
        {
            public Vector2 c;
            public Vector2 r;
            public float angle;
        }

        static Texture2D BloodTexture(string name, BloodShape shape, int seed)
        {
            const int size = 256;
            var r = new System.Random(seed);
            float Rf(float a, float b) => a + (float)r.NextDouble() * (b - a);
            var blobs = new List<Blob>();

            switch (shape)
            {
                case BloodShape.Drop:
                    blobs.Add(new Blob { c = new Vector2(0.5f, 0.5f), r = new Vector2(0.3f, 0.28f) });
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Rf(0f, Mathf.PI * 2f), d = Rf(0.33f, 0.45f), s = Rf(0.015f, 0.04f);
                        blobs.Add(new Blob { c = new Vector2(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d), r = new Vector2(s, s) });
                    }
                    break;
                case BloodShape.Splatter:
                    blobs.Add(new Blob { c = new Vector2(0.5f, 0.5f), r = new Vector2(0.2f, 0.17f), angle = Rf(0f, 3f) });
                    for (int i = 0; i < 5; i++)
                        blobs.Add(new Blob { c = new Vector2(Rf(0.38f, 0.62f), Rf(0.38f, 0.62f)), r = new Vector2(Rf(0.06f, 0.12f), Rf(0.05f, 0.1f)) });
                    for (int i = 0; i < 14; i++)
                    {
                        // Elongated streaks flung outward, plus droplets at their tips.
                        float a = Rf(0f, Mathf.PI * 2f), d = Rf(0.22f, 0.36f);
                        Vector2 c = new Vector2(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d);
                        blobs.Add(new Blob { c = c, r = new Vector2(Rf(0.05f, 0.1f), Rf(0.008f, 0.02f)), angle = a });
                        float tip = d + Rf(0.08f, 0.13f), s = Rf(0.008f, 0.025f);
                        blobs.Add(new Blob { c = new Vector2(0.5f + Mathf.Cos(a) * tip, 0.5f + Mathf.Sin(a) * tip), r = new Vector2(s, s) });
                    }
                    break;
                case BloodShape.Smear:
                    // Built from overlapping ellipses along u; the streak pattern is added below.
                    for (float u = 0.08f; u < 0.92f; u += 0.04f)
                    {
                        float taper = Mathf.Sin(Mathf.InverseLerp(0.05f, 0.95f, u) * Mathf.PI);
                        blobs.Add(new Blob { c = new Vector2(u, 0.5f + Rf(-0.03f, 0.03f)), r = new Vector2(0.06f, Mathf.Lerp(0.12f, 0.36f, taper) * Rf(0.85f, 1.1f)) });
                    }
                    break;
                case BloodShape.Pool:
                    blobs.Add(new Blob { c = new Vector2(0.5f, 0.5f), r = new Vector2(0.3f, 0.26f), angle = Rf(0f, 3f) });
                    for (int i = 0; i < 9; i++)
                    {
                        float a = Rf(0f, Mathf.PI * 2f), d = Rf(0.1f, 0.25f);
                        blobs.Add(new Blob { c = new Vector2(0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d), r = new Vector2(Rf(0.08f, 0.16f), Rf(0.07f, 0.13f)), angle = a });
                    }
                    break;
            }

            float noiseOffset = seed * 0.37f;
            return SaveTexture(name, size, (x, y) =>
            {
                Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                // Wobble the edges with noise so nothing looks like a perfect circle.
                float wobble = (Fbm(p.x + noiseOffset, p.y, 8, 3, seed) - 0.5f) * 0.35f;
                float best = float.MaxValue;
                foreach (var b in blobs)
                {
                    Vector2 d = p - b.c;
                    float cs = Mathf.Cos(-b.angle), sn = Mathf.Sin(-b.angle);
                    Vector2 rd = new Vector2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
                    float e = Mathf.Sqrt((rd.x * rd.x) / (b.r.x * b.r.x) + (rd.y * rd.y) / (b.r.y * b.r.y));
                    best = Mathf.Min(best, e);
                }
                float field = best + wobble;
                float alpha = field < 1f ? 1f : 0f;

                if (shape == BloodShape.Smear)
                {
                    // Drag streaks: thin dry lines running along the smear.
                    float streak = Mathf.PerlinNoise(p.x * 3f + noiseOffset, p.y * 60f);
                    if (streak < 0.38f) alpha = 0f;
                }

                // Darker, thicker in the middle, a little lighter at the rim.
                float shade = Mathf.Lerp(1f, 0.7f, Mathf.Clamp01(1f - field));
                return new Color(shade, shade, shade, alpha);
            }, true, TextureWrapMode.Clamp);
        }

        static float Fbm(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int p = period;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(u * p, v * p, p, seed + o * 31);
                norm += amp;
                amp *= 0.5f;
                p *= 2;
            }
            return sum / norm;
        }

        /// <summary>Tileable value noise with the given integer period.</summary>
        static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, period, seed), b = Hash(x0 + 1, y0, period, seed);
            float c = Hash(x0, y0 + 1, period, seed), d = Hash(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        // ------------------------------------------------------------------ Object helpers

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            return t;
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m,
            Vector3 euler = default, bool collider = true) =>
            Prim(PrimitiveType.Cube, name, parent, pos, size, m, euler, collider);

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m,
            Vector3 euler = default, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Quad(string name, Transform parent, Vector3 pos, Quaternion rotation, Vector2 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rotation;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void NoShadows(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void InvisibleWall(string name, Transform parent, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.layer = 2; // Ignore Raycast, so rain doesn't splash on the invisible wall's top.
            go.AddComponent<BoxCollider>().size = size;
        }

        static Light PointLight(string name, Transform parent, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        static TextMesh Text(string text, Transform parent, Vector3 pos, Quaternion rotation, float charSize, Color color)
        {
            var go = new GameObject("Text " + text.Replace("\n", ""));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = rotation;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 100;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            return tm;
        }

        static LightFlicker Flicker(GameObject host, LightFlicker.Mode mode, Light[] lights, Renderer[] renderers = null, TextMesh[] texts = null)
        {
            var f = host.AddComponent<LightFlicker>();
            f.mode = mode;
            f.lights = lights;
            f.emissiveRenderers = renderers ?? new Renderer[0];
            f.textMeshes = texts ?? new TextMesh[0];
            return f;
        }

        static Font LoadFont()
        {
            foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    var f = Resources.GetBuiltinResource<Font>(name);
                    if (f != null) return f;
                }
                catch (Exception)
                {
                    // Font name differs between Unity versions; try the next one.
                }
            }
            return null;
        }

        static float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static string SafeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    /// <summary>Builds the scene automatically the first time the project is opened.</summary>
    [InitializeOnLoad]
    static class NightStreetAutoBuild
    {
        const string SessionKey = "HangingWoman.AutoBuildChecked";

        static NightStreetAutoBuild()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(NightStreetSceneBuilder.ScenePath)) return;
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                NightStreetSceneBuilder.Build();
            };
        }
    }
}
