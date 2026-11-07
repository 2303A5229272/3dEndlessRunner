using System.Collections.Generic;
using UnityEngine;

namespace DessertRunner
{
    public enum GState { Menu, Instructions, Playing, Paused, GameOver }

    class Obst { public GameObject go; public string key; public int lane; public float z, halfD, h; public bool gate; }
    class Pick { public GameObject go; public int kind; public Vector3 pos; public bool magnetized; }
    class Tile { public GameObject go; public float z0; }
    struct Stage { public string name; public Color sky, fog, light, sand, side; public float palmW, rockW, cactusW, bushW; }

    /// <summary>Main game controller: world generation, track recycling, player, obstacles, pickups, score, UI.</summary>
    public class GameManager : MonoBehaviour
    {
        // ---------- tuning ----------
        const float LaneW = 3f, TileLen = 30f, Gravity = -34f, JumpV = 12.5f;
        const int TileCount = 12;
        static readonly float[] BaseSpeed = { 10f, 13f, 16f };
        static readonly float[] Accel = { 0.20f, 0.30f, 0.42f };
        static readonly float[] ObstP = { 0.45f, 0.6f, 0.75f };
        const float MaxSpeed = 34f;

        // ---------- state ----------
        GState state = GState.Menu;
        int difficulty = 1, stageIdx = 0;
        float playerZ, playerX, playerY, velY, slideT, runTime, speed, nextSpawnZ, deadTimer, shakeT;
        int lane = 1;
        bool grounded = true;
        int coins, gems; float score;
        int totalCoins, totalGems; float bestDistance;
        float magnetT, boostT, x2T; bool shield;

        Builders.PlayerRig rig;
        Camera cam; Light sun;
        Transform worldRoot, poolRoot, backdrop;
        Material sandMat, sideMat;
        readonly List<Tile> tiles = new List<Tile>();
        readonly List<Obst> obsts = new List<Obst>();
        readonly List<Pick> picks = new List<Pick>();
        readonly Dictionary<string, Stack<GameObject>> pool = new Dictionary<string, Stack<GameObject>>();
        AudioSource sfx; AudioClip cCoin, cGem, cHit, cJump, cPower;

        static readonly Stage[] Stages = new Stage[]
        {
            new Stage{ name="Golden Dunes", sky=new Color(0.45f,0.7f,0.95f), fog=new Color(0.93f,0.85f,0.68f), light=new Color(1f,0.95f,0.82f), sand=new Color(0.86f,0.7f,0.45f), side=new Color(0.9f,0.74f,0.47f), palmW=1, rockW=1, cactusW=1, bushW=1 },
            new Stage{ name="Oasis Palms",  sky=new Color(0.4f,0.75f,0.95f), fog=new Color(0.82f,0.88f,0.75f), light=new Color(1f,0.97f,0.88f), sand=new Color(0.84f,0.72f,0.48f), side=new Color(0.62f,0.72f,0.4f), palmW=3, rockW=0.5f, cactusW=0.3f, bushW=3 },
            new Stage{ name="Rocky Canyon", sky=new Color(0.5f,0.68f,0.88f), fog=new Color(0.86f,0.72f,0.6f), light=new Color(1f,0.9f,0.8f),  sand=new Color(0.78f,0.55f,0.36f), side=new Color(0.72f,0.45f,0.3f), palmW=0.3f, rockW=4, cactusW=1.5f, bushW=0.4f },
            new Stage{ name="Sunset Trail", sky=new Color(0.95f,0.55f,0.35f), fog=new Color(0.98f,0.72f,0.5f), light=new Color(1f,0.7f,0.45f),  sand=new Color(0.9f,0.62f,0.4f), side=new Color(0.88f,0.58f,0.38f), palmW=1.2f, rockW=1.2f, cactusW=1.5f, bushW=0.8f },
        };

        // ================= lifecycle =================
        void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            totalCoins = PlayerPrefs.GetInt("COINSAVE", 0);
            totalGems = PlayerPrefs.GetInt("GEMSAVE", 0);
            bestDistance = PlayerPrefs.GetFloat("DISTANCESAVE", 0f);
            SetupScene();
            MakeAudio();
            BuildTrack();
            ApplyStage();
        }

        void SetupScene()
        {
            // Camera
            cam = Camera.main;
            if (cam == null)
            {
                var cg = new GameObject("Main Camera"); cg.tag = "MainCamera";
                cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.farClipPlane = 600f; cam.fieldOfView = 62f;
            // Lights: turn off any existing ones, add our sun
            Light[] ls;
#if UNITY_2023_1_OR_NEWER
            ls = FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            ls = FindObjectsOfType<Light>();
#endif
            foreach (var l in ls) l.enabled = false;
            var lg = new GameObject("Sun");
            sun = lg.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
            lg.transform.rotation = Quaternion.Euler(48f, -32f, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 90f; RenderSettings.fogEndDistance = 430f;

            worldRoot = new GameObject("World").transform;
            poolRoot = new GameObject("Pool").transform; poolRoot.gameObject.SetActive(false);
            backdrop = new GameObject("Backdrop").transform;

            // player
            rig = Builders.BuildPlayer();

            // far mountains (follow the player)
            var mc = new[] { new Color(0.6f, 0.5f, 0.55f), new Color(0.66f, 0.5f, 0.4f), new Color(0.55f, 0.45f, 0.5f) };
            for (int i = 0; i < 14; i++)
            {
                float side = (i % 2 == 0) ? -1 : 1;
                float x = side * Random.Range(70f, 260f);
                float s = Random.Range(60f, 140f);
                Builders.Prim(PrimitiveType.Sphere, backdrop, new Vector3(x, s * 0.12f, Random.Range(340f, 470f)), new Vector3(s * 1.6f, s * 0.55f, s * 1.2f), Builders.Mat(mc[i % 3], 0.02f));
            }
            for (int i = 0; i < 5; i++)
                Builders.Prim(PrimitiveType.Sphere, backdrop, new Vector3(Random.Range(-150f, 150f), 10f, Random.Range(360f, 480f)), new Vector3(200f, 60f, 90f), Builders.Mat(new Color(0.8f, 0.64f, 0.42f), 0.02f));
        }

        void ApplyStage()
        {
            var st = Stages[stageIdx];
            cam.backgroundColor = st.sky;
            RenderSettings.fogColor = st.fog;
            RenderSettings.ambientLight = Color.Lerp(st.fog, Color.white, 0.25f) * 0.75f;
            if (sun != null) sun.color = st.light;
            if (sandMat != null) { sandMat.color = st.sand; if (sandMat.HasProperty("_BaseColor")) sandMat.SetColor("_BaseColor", st.sand); }
            if (sideMat != null) { sideMat.color = st.side; if (sideMat.HasProperty("_BaseColor")) sideMat.SetColor("_BaseColor", st.side); }
        }

        void MakeAudio()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            cCoin = Tone(880f, 1320f, 0.14f); cGem = Tone(660f, 1500f, 0.3f); cJump = Tone(300f, 560f, 0.16f);
            cPower = Tone(440f, 1100f, 0.4f); cHit = Noise(0.45f);
        }
        static AudioClip Tone(float f0, float f1, float dur)
        {
            int sr = 22050, n = (int)(sr * dur); var d = new float[n]; float ph = 0;
            for (int i = 0; i < n; i++) { float t = (float)i / n; ph += 2f * Mathf.PI * Mathf.Lerp(f0, f1, t) / sr; d[i] = Mathf.Sin(ph) * 0.35f * (1f - t); }
            var c = AudioClip.Create("tone", n, 1, sr, false); c.SetData(d, 0); return c;
        }
        static AudioClip Noise(float dur)
        {
            int sr = 22050, n = (int)(sr * dur); var d = new float[n];
            for (int i = 0; i < n; i++) { float t = (float)i / n; d[i] = (Random.value * 2f - 1f) * 0.5f * (1f - t) * (1f - t); }
            var c = AudioClip.Create("hit", n, 1, sr, false); c.SetData(d, 0); return c;
        }
        void Play(AudioClip c) { if (sfx != null && c != null) sfx.PlayOneShot(c); }

        // ================= pooling =================
        GameObject PoolGet(string key, System.Func<GameObject> make)
        {
            Stack<GameObject> s;
            GameObject g;
            if (pool.TryGetValue(key, out s) && s.Count > 0) { g = s.Pop(); }
            else { g = make(); }
            g.transform.SetParent(worldRoot, false);
            g.SetActive(true);
            return g;
        }
        void PoolRelease(string key, GameObject g)
        {
            Stack<GameObject> s;
            if (!pool.TryGetValue(key, out s)) { s = new Stack<GameObject>(); pool[key] = s; }
            g.SetActive(false); g.transform.SetParent(poolRoot, false); s.Push(g);
        }

        // ================= track (recycled tiles) =================
        void BuildTrack()
        {
            sandMat = Builders.SandMat(Stages[0].sand);
            sideMat = Builders.SandMat(Stages[0].side, 60f, 4f);
            for (int i = 0; i < TileCount; i++)
            {
                var t = new Tile();
                t.go = new GameObject("Tile");
                t.go.transform.SetParent(worldRoot, false);
                Builders.Prim(PrimitiveType.Cube, t.go.transform, new Vector3(0, -0.5f, TileLen * 0.5f), new Vector3(10.4f, 1f, TileLen), sandMat);
                Builders.Prim(PrimitiveType.Cube, t.go.transform, new Vector3(-205.2f, -0.55f, TileLen * 0.5f), new Vector3(400f, 1f, TileLen), sideMat);
                Builders.Prim(PrimitiveType.Cube, t.go.transform, new Vector3(205.2f, -0.55f, TileLen * 0.5f), new Vector3(400f, 1f, TileLen), sideMat);
                var lineM = Builders.Mat(new Color(1f, 0.95f, 0.8f), 0.05f);
                Builders.Prim(PrimitiveType.Cube, t.go.transform, new Vector3(-1.5f, 0.01f, TileLen * 0.5f), new Vector3(0.07f, 0.02f, TileLen), lineM);
                Builders.Prim(PrimitiveType.Cube, t.go.transform, new Vector3(1.5f, 0.01f, TileLen * 0.5f), new Vector3(0.07f, 0.02f, TileLen), lineM);
                var edgeM = Builders.Mat(new Color(0.55f, 0.4f, 0.25f), 0.05f);
                Builders.Prim(PrimitiveType.Cube, t.go.transform, new Vector3(-5.1f, 0.03f, TileLen * 0.5f), new Vector3(0.3f, 0.06f, TileLen), edgeM);
                Builders.Prim(PrimitiveType.Cube, t.go.transform, new Vector3(5.1f, 0.03f, TileLen * 0.5f), new Vector3(0.3f, 0.06f, TileLen), edgeM);
                var props = new GameObject("Props"); props.transform.SetParent(t.go.transform, false);
                tiles.Add(t);
            }
            LayoutTiles(-TileLen * 2f);
        }

        void LayoutTiles(float startZ)
        {
            for (int i = 0; i < tiles.Count; i++) { tiles[i].z0 = startZ + i * TileLen; tiles[i].go.transform.position = new Vector3(0, 0, tiles[i].z0); Populate(tiles[i]); }
        }

        void Populate(Tile t)
        {
            var props = t.go.transform.Find("Props");
            for (int i = props.childCount - 1; i >= 0; i--) Destroy(props.GetChild(i).gameObject);
            var st = Stages[stageIdx];
            float tot = st.palmW + st.rockW + st.cactusW + st.bushW;
            int n = 9;
            for (int i = 0; i < n; i++)
            {
                float sgn = Random.value < 0.5f ? -1f : 1f;
                float x = sgn * Random.Range(6.5f, 34f);
                float z = Random.Range(1f, TileLen - 1f);
                Vector3 p = new Vector3(x, 0, z);
                float r = Random.value * tot;
                if ((r -= st.palmW) < 0) { if (Random.value < 0.35f) Builders.Acacia(props, p, Random.Range(2.5f, 3.5f)); else Builders.Palm(props, p, Random.Range(4f, 7f)); }
                else if ((r -= st.rockW) < 0) Builders.SideRock(props, p, Random.Range(0.8f, 2.2f), Color.Lerp(new Color(0.5f, 0.38f, 0.28f), new Color(0.62f, 0.5f, 0.38f), Random.value));
                else if ((r -= st.cactusW) < 0) Builders.SideCactus(props, p, Random.Range(0.8f, 1.4f));
                else Builders.Bush(props, p, Random.Range(0.8f, 1.6f));
            }
            if (Random.value < 0.4f) Builders.SideLog(props, new Vector3(Random.Range(6.5f, 14f) * (Random.value < 0.5f ? -1 : 1), 0, Random.Range(2f, 28f)));
            if (Random.value < 0.6f)
            {
                float sgn = Random.value < 0.5f ? -1f : 1f;
                Builders.Dune(props, new Vector3(sgn * Random.Range(28f, 48f), -2f, Random.Range(0f, TileLen)), new Vector3(Random.Range(20f, 40f), Random.Range(6f, 12f), Random.Range(18f, 30f)), st.side * Random.Range(0.95f, 1.08f));
            }
        }

        void RecycleTiles()
        {
            var first = tiles[0];
            if (first.z0 + TileLen < playerZ - 25f)
            {
                tiles.RemoveAt(0);
                var last = tiles[tiles.Count - 1];
                first.z0 = last.z0 + TileLen;
                first.go.transform.position = new Vector3(0, 0, first.z0);
                Populate(first);
                tiles.Add(first);
            }
        }

        // ================= run control =================
        void ResetRun()
        {
            foreach (var o in obsts) PoolRelease(o.key, o.go); obsts.Clear();
            foreach (var p in picks) PoolRelease("Pickup" + p.kind, p.go); picks.Clear();
            playerZ = 0; playerX = 0; playerY = 0; velY = 0; lane = 1; slideT = 0; grounded = true;
            coins = 0; gems = 0; score = 0; runTime = 0; magnetT = boostT = x2T = 0; shield = false; deadTimer = 0; shakeT = 0;
            speed = BaseSpeed[difficulty];
            ApplyStage();
            LayoutTiles(-TileLen * 2f);
            nextSpawnZ = 40f;
        }

        void StartGame() { ResetRun(); state = GState.Playing; }

        void EndGame()
        {
            state = GState.GameOver; deadTimer = 0f; shakeT = 0.5f; Play(cHit);
            totalCoins += coins; totalGems += gems;
            if (playerZ > bestDistance) bestDistance = playerZ;
            PlayerPrefs.SetInt("COINSAVE", totalCoins); PlayerPrefs.SetInt("GEMSAVE", totalGems);
            PlayerPrefs.SetFloat("DISTANCESAVE", bestDistance); PlayerPrefs.Save();
        }

        void GoMenu() { ResetRun(); state = GState.Menu; }

        // ================= spawning =================
        void SpawnObstacle(string key, int ln, float z)
        {
            var g = PoolGet(key, () => { float a, b; bool c; return Builders.BuildObstacle(key, out a, out b, out c); });
            var ob = new Obst { go = g, key = key, lane = ln, z = z };
            Dims(key, out ob.halfD, out ob.h, out ob.gate);
            g.transform.position = new Vector3((ln - 1) * LaneW, 0, z);
            obsts.Add(ob);
        }

        static void Dims(string key, out float hd, out float h, out bool gate)
        {
            gate = false; hd = 0.7f; h = 1f;
            switch (key)
            {
                case "Rock": hd = 0.7f; h = 0.95f; break;
                case "BigRock": hd = 0.95f; h = 1.3f; break;
                case "Log": hd = 0.5f; h = 0.75f; break;
                case "Block": hd = 0.75f; h = 1.2f; break;
                case "Cactus": hd = 0.5f; h = 1.7f; break;
                case "Mound": hd = 0.9f; h = 0.7f; break;
                case "Barrier": hd = 0.35f; h = 0.9f; break;
                case "Gate": hd = 0.3f; h = 1.2f; gate = true; break;
            }
        }

        void SpawnPickup(int kind, int ln, float z, float y)
        {
            var g = PoolGet("Pickup" + kind, () => Builders.BuildPickup(kind));
            var p = new Pick { go = g, kind = kind, pos = new Vector3((ln - 1) * LaneW, y, z) };
            g.transform.position = p.pos;
            picks.Add(p);
        }

        static readonly string[] LowKeys = { "Rock", "BigRock", "Log", "Block", "Cactus", "Mound" };

        void SpawnRow(float z)
        {
            float diff = Mathf.Clamp01((speed - BaseSpeed[difficulty]) / 18f);
            float r = Random.value;
            if (r < 0.16f + 0.08f * diff)
            {
                // full-width wall: jump a barrier or slide under a gate
                bool gate = Random.value < 0.5f;
                for (int l = 0; l < 3; l++) SpawnObstacle(gate ? "Gate" : "Barrier", l, z);
                for (int l = 0; l < 3; l++) { SpawnPickup(0, l, z - 3f, 1f); SpawnPickup(0, l, z + 3f, 1f); }
                if (!gate) for (int l = 0; l < 3; l++) SpawnPickup(0, l, z, 2.3f);
                return;
            }
            int safe = Random.Range(0, 3);
            for (int l = 0; l < 3; l++)
            {
                if (l == safe) continue;
                if (Random.value < ObstP[difficulty] + 0.2f * diff)
                {
                    string key = LowKeys[Random.Range(0, LowKeys.Length)];
                    SpawnObstacle(key, l, z + Random.Range(-1f, 1f));
                    if (Random.value < 0.35f) { SpawnPickup(0, l, z - 1.2f, 2.3f); SpawnPickup(0, l, z, 2.5f); SpawnPickup(0, l, z + 1.2f, 2.3f); }
                }
            }
            // reward on the free lane
            float q = Random.value;
            if (q < 0.55f) { int n = Random.Range(3, 7); for (int i = 0; i < n; i++) SpawnPickup(0, safe, z + (i - n / 2f) * 1.8f, 1f); }
            else if (q < 0.72f) SpawnPickup(1, safe, z, 1.1f);
            else if (q < 0.80f) SpawnPickup(Random.Range(2, 6), safe, z, 1.2f);
        }

        // ================= update =================
        void Update()
        {
            Inp.Poll();
            switch (state)
            {
                case GState.Menu:
                    playerZ += 8f * Time.deltaTime; runTime += Time.deltaTime; RecycleTiles();
                    playerX = Mathf.Lerp(playerX, 0, 8 * Time.deltaTime); AnimatePlayer(8f, false);
                    if (Inp.Submit) StartGame();
                    break;
                case GState.Instructions:
                    playerZ += 8f * Time.deltaTime; runTime += Time.deltaTime; RecycleTiles(); AnimatePlayer(8f, false);
                    if (Inp.Back || Inp.Submit) state = GState.Menu;
                    break;
                case GState.Playing: UpdatePlaying(); break;
                case GState.Paused:
                    if (Inp.Pause) state = GState.Playing;
                    break;
                case GState.GameOver:
                    deadTimer += Time.deltaTime;
                    if (deadTimer > 0.8f && Inp.Submit) StartGame();
                    else if (deadTimer > 0.8f && Inp.Back) GoMenu();
                    break;
            }
            // place player
            rig.root.position = new Vector3(playerX, playerY, playerZ);
            backdrop.position = new Vector3(0, 0, playerZ);
        }

        void UpdatePlaying()
        {
            float dt = Time.deltaTime;
            if (Inp.Pause) { state = GState.Paused; return; }
            runTime += dt;
            if (magnetT > 0) magnetT -= dt;
            if (boostT > 0) boostT -= dt;
            if (x2T > 0) x2T -= dt;
            float baseS = Mathf.Min(MaxSpeed, BaseSpeed[difficulty] + Accel[difficulty] * runTime);
            speed = baseS * (boostT > 0 ? 1.45f : 1f);
            playerZ += speed * dt;
            float mult = x2T > 0 ? 2f : 1f;
            score += speed * dt * mult;

            // lanes
            if (Inp.Left && lane > 0) lane--;
            if (Inp.Right && lane < 2) lane++;
            playerX = Mathf.MoveTowards(playerX, (lane - 1) * LaneW, 16f * dt);

            // jump / slide
            if (Inp.Jump && grounded) { velY = JumpV; grounded = false; slideT = 0; Play(cJump); }
            if (Inp.Slide)
            {
                if (grounded) slideT = 0.7f; else velY = -20f; // slide on ground / fast fall in air
            }
            if (slideT > 0) slideT -= dt;
            if (!grounded)
            {
                velY += Gravity * dt; playerY += velY * dt;
                if (playerY <= 0) { playerY = 0; velY = 0; grounded = true; }
            }

            AnimatePlayer(speed, true);
            RecycleTiles();

            // spawn ahead
            float spacing = Mathf.Max(Mathf.Lerp(18f, 10f, Mathf.Clamp01((speed - 10f) / 22f)), speed * 0.95f);
            while (nextSpawnZ < playerZ + 150f) { SpawnRow(nextSpawnZ); nextSpawnZ += spacing * Random.Range(0.9f, 1.3f); }

            float height = slideT > 0 ? 0.8f : 1.8f;

            // obstacles
            for (int i = obsts.Count - 1; i >= 0; i--)
            {
                var o = obsts[i];
                if (o.z < playerZ - 15f) { PoolRelease(o.key, o.go); obsts.RemoveAt(i); continue; }
                float dz = Mathf.Abs(o.z - playerZ);
                if (dz > o.halfD + 0.45f) continue;
                float ox = (o.lane - 1) * LaneW;
                float halfW = o.gate || o.key == "Barrier" ? 1.5f : 1.0f;
                if (Mathf.Abs(ox - playerX) > halfW + 0.3f) continue;
                bool hit = o.gate ? (playerY + height > 1.2f) : (playerY < o.h);
                if (hit)
                {
                    if (shield) { shield = false; shakeT = 0.25f; Play(cHit); PoolRelease(o.key, o.go); obsts.RemoveAt(i); }
                    else { EndGame(); return; }
                }
            }

            // pickups
            for (int i = picks.Count - 1; i >= 0; i--)
            {
                var p = picks[i];
                if (p.pos.z < playerZ - 15f) { PoolRelease("Pickup" + p.kind, p.go); picks.RemoveAt(i); continue; }
                bool coinLike = p.kind <= 1;
                if (magnetT > 0 && coinLike && p.pos.z - playerZ < 12f && p.pos.z - playerZ > -2f)
                    p.magnetized = true;
                if (p.magnetized)
                {
                    Vector3 target = new Vector3(playerX, playerY + 1f, playerZ + 0.5f);
                    p.pos = Vector3.MoveTowards(p.pos, target, (speed + 22f) * dt);
                }
                p.go.transform.position = p.pos + (p.kind >= 2 ? new Vector3(0, Mathf.Sin(runTime * 4f) * 0.15f, 0) : Vector3.zero);
                p.go.transform.Rotate(0, 220f * dt, 0, Space.World);

                bool collect;
                if (p.magnetized) collect = (p.pos - new Vector3(playerX, playerY + 1f, playerZ)).sqrMagnitude < 1.6f;
                else collect = Mathf.Abs(p.pos.z - playerZ) < 0.9f && Mathf.Abs(p.pos.x - playerX) < 1.2f && playerY < p.pos.y + 0.5f && playerY + height > p.pos.y - 0.3f;
                if (!collect) continue;
                switch (p.kind)
                {
                    case 0: coins++; score += 10f * mult; Play(cCoin); break;
                    case 1: gems++; score += 50f * mult; Play(cGem); break;
                    case 2: shield = true; Play(cPower); break;
                    case 3: magnetT = 8f; Play(cPower); break;
                    case 4: boostT = 5f; Play(cPower); break;
                    case 5: x2T = 10f; Play(cPower); break;
                }
                PoolRelease("Pickup" + p.kind, p.go); picks.RemoveAt(i);
            }
        }

        void AnimatePlayer(float spd, bool live)
        {
            float f = Mathf.Clamp(spd, 6f, 34f) * 0.75f;
            float swing = grounded ? Mathf.Sin(runTime * f * 0.9f) * 50f : 25f;
            rig.legL.localRotation = Quaternion.Euler(swing, 0, 0);
            rig.legR.localRotation = Quaternion.Euler(grounded ? -swing : -25f, 0, 0);
            rig.armL.localRotation = Quaternion.Euler(grounded ? -swing * 0.8f : -150f, 0, 0);
            rig.armR.localRotation = Quaternion.Euler(grounded ? swing * 0.8f : -150f, 0, 0);
            bool sliding = live && slideT > 0 && grounded;
            float lean = (live ? (playerX - (lane - 1) * LaneW) * -8f : 0f);
            rig.model.localRotation = sliding ? Quaternion.Euler(-65f, 0, 0) : Quaternion.Euler(8f, 0, lean);
            rig.model.localPosition = sliding ? new Vector3(0, 0.15f, 0.05f) : new Vector3(0, grounded ? Mathf.Abs(Mathf.Sin(runTime * f * 0.9f)) * 0.06f : 0f, 0);
        }

        void LateUpdate()
        {
            Vector3 target = new Vector3(playerX * 0.55f, 4.3f, playerZ - 7.5f);
            Vector3 look = new Vector3(playerX * 0.3f, 1.2f, playerZ + 7f);
            if (shakeT > 0) { shakeT -= Time.deltaTime; target += Random.insideUnitSphere * 0.25f; }
            if (state == GState.Menu || state == GState.Instructions)
            {
                float a = Mathf.Sin(Time.time * 0.35f) * 0.9f;
                target = new Vector3(a * 3f, 2.4f, playerZ - 5.5f); look = new Vector3(0, 1.1f, playerZ + 1f);
            }
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, Mathf.Clamp01(12f * Time.deltaTime));
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look - cam.transform.position), Mathf.Clamp01(12f * Time.deltaTime));
        }

        void OnApplicationFocus(bool focus) { if (!focus && state == GState.Playing) state = GState.Paused; }

        // ================= UI (IMGUI: no packages needed) =================
        GUIStyle sLabel, sBtn, sBtnOn; bool styles;
        void InitStyles()
        {
            styles = true;
            sLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true };
            sBtn = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sBtnOn = new GUIStyle(sBtn); sBtnOn.normal.textColor = new Color(1f, 0.8f, 0.2f); sBtnOn.hover.textColor = sBtnOn.normal.textColor;
        }
        void Txt(Rect r, string t, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter)
        {
            sLabel.fontSize = size; sLabel.alignment = a;
            sLabel.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), t, sLabel);
            sLabel.normal.textColor = c; GUI.Label(r, t, sLabel);
        }
        void Box(Rect r, float a = 0.55f)
        {
            var o = GUI.color; GUI.color = new Color(0.1f, 0.07f, 0.03f, a); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = o;
        }
        static readonly Color Gold = new Color(1f, 0.78f, 0.2f);

        void OnGUI()
        {
            if (!styles) InitStyles();
            float s = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            float W = Screen.width / s, H = 720f, cx = W * 0.5f;

            if (state == GState.Playing || state == GState.Paused || state == GState.GameOver) DrawHUD(W);

            if (state == GState.Menu)
            {
                Box(new Rect(cx - 330, 40, 660, 640), 0.5f);
                Txt(new Rect(cx - 320, 55, 640, 80), "DESSERT RUNNER", 62, Gold);
                Txt(new Rect(cx - 320, 130, 640, 36), "ENDLESS RUNNER GAME", 26, Color.white);
                Txt(new Rect(cx - 320, 172, 640, 28), "Stage", 20, new Color(0.95f, 0.85f, 0.6f));
                for (int i = 0; i < 4; i++)
                    if (GUI.Button(new Rect(cx - 320 + i * 162, 202, 156, 44), Stages[i].name, i == stageIdx ? sBtnOn : sBtn) && true) { stageIdx = i; ApplyStage(); LayoutTiles(playerZ - TileLen * 2f); }
                Txt(new Rect(cx - 320, 256, 640, 28), "Difficulty", 20, new Color(0.95f, 0.85f, 0.6f));
                string[] dn = { "Easy", "Medium", "Hard" };
                for (int i = 0; i < 3; i++)
                    if (GUI.Button(new Rect(cx - 320 + i * 216, 286, 208, 44), dn[i], i == difficulty ? sBtnOn : sBtn)) difficulty = i;
                if (GUI.Button(new Rect(cx - 160, 350, 320, 64), "START GAME", sBtn)) StartGame();
                if (GUI.Button(new Rect(cx - 160, 426, 320, 56), "HOW TO PLAY", sBtn)) state = GState.Instructions;
                Txt(new Rect(cx - 320, 500, 640, 30), "Total coins " + totalCoins + "   |   Total gems " + totalGems, 20, Color.white);
                Txt(new Rect(cx - 320, 530, 640, 30), "Best distance " + Mathf.FloorToInt(bestDistance) + " m", 20, Color.white);
                Txt(new Rect(cx - 320, 600, 640, 30), "Press SPACE or ENTER to start", 18, new Color(1, 1, 1, 0.8f));
                if (!Application.isEditor && GUI.Button(new Rect(cx - 80, 630, 160, 36), "Quit", sBtn)) Application.Quit();
            }
            else if (state == GState.Instructions)
            {
                Box(new Rect(cx - 360, 60, 720, 600), 0.65f);
                Txt(new Rect(cx - 340, 70, 680, 60), "HOW TO PLAY", 44, Gold);
                string[,] k = {
                    { "A / Left Arrow", "Move left" }, { "D / Right Arrow", "Move right" },
                    { "W / Up Arrow / Space", "Jump over obstacles" }, { "S / Down Arrow", "Slide under barriers" }, { "Escape", "Pause / resume" } };
                for (int i = 0; i < 5; i++)
                {
                    Txt(new Rect(cx - 340, 140 + i * 40, 340, 36), k[i, 0], 22, Gold, TextAnchor.MiddleRight);
                    Txt(new Rect(cx + 20, 140 + i * 40, 320, 36), k[i, 1], 22, Color.white, TextAnchor.MiddleLeft);
                }
                Txt(new Rect(cx - 340, 350, 680, 30), "Collect coins and gems. Avoid rocks, logs, barriers and gates.", 20, Color.white);
                Txt(new Rect(cx - 340, 390, 680, 30), "Power-ups (glowing orbs):", 20, Gold);
                Txt(new Rect(cx - 340, 425, 680, 30), "<color=#4d80ff>BLUE</color> Shield    <color=#e62626>RED</color> Magnet    <color=#ff9919>ORANGE</color> Speed boost    <color=#a64dd9>PURPLE</color> Score x2", 20, Color.white);
                Txt(new Rect(cx - 340, 475, 680, 30), "Speed increases the longer you run. Reach the furthest distance!", 20, Color.white);
                if (GUI.Button(new Rect(cx - 120, 560, 240, 56), "BACK", sBtn)) state = GState.Menu;
            }
            else if (state == GState.Paused)
            {
                Box(new Rect(0, 0, W, H), 0.45f);
                Txt(new Rect(cx - 300, 200, 600, 80), "PAUSED", 64, Gold);
                if (GUI.Button(new Rect(cx - 150, 310, 300, 60), "RESUME", sBtn)) state = GState.Playing;
                if (GUI.Button(new Rect(cx - 150, 385, 300, 60), "MAIN MENU", sBtn)) GoMenu();
                Txt(new Rect(cx - 300, 470, 600, 30), "Press ESC to resume", 18, Color.white);
            }
            else if (state == GState.GameOver)
            {
                Box(new Rect(cx - 280, 150, 560, 420), 0.6f);
                Txt(new Rect(cx - 270, 160, 540, 80), "GAME OVER", 60, new Color(1f, 0.45f, 0.3f));
                Txt(new Rect(cx - 270, 250, 540, 40), "Score  " + Mathf.FloorToInt(score), 32, Color.white);
                Txt(new Rect(cx - 270, 295, 540, 34), "Distance " + Mathf.FloorToInt(playerZ) + " m   |   Best " + Mathf.FloorToInt(bestDistance) + " m", 22, Color.white);
                Txt(new Rect(cx - 270, 330, 540, 34), "Coins " + coins + "   Gems " + gems, 22, Gold);
                if (GUI.Button(new Rect(cx - 250, 400, 240, 60), "RETRY", sBtn) && deadTimer > 0.5f) StartGame();
                if (GUI.Button(new Rect(cx + 10, 400, 240, 60), "MAIN MENU", sBtn) && deadTimer > 0.5f) GoMenu();
                Txt(new Rect(cx - 270, 480, 540, 30), "SPACE / ENTER to retry   |   ESC for menu", 18, new Color(1, 1, 1, 0.8f));
            }
        }

        void DrawHUD(float W)
        {
            Box(new Rect(14, 14, 250, 150), 0.5f);
            Txt(new Rect(24, 18, 230, 34), "Coins   " + coins, 24, Gold, TextAnchor.MiddleLeft);
            Txt(new Rect(24, 50, 230, 34), "Gems    " + gems, 24, new Color(1f, 0.4f, 0.5f), TextAnchor.MiddleLeft);
            Txt(new Rect(24, 82, 230, 34), "Distance  " + Mathf.FloorToInt(playerZ) + " m", 24, Color.white, TextAnchor.MiddleLeft);
            Txt(new Rect(24, 114, 230, 34), "Score  " + Mathf.FloorToInt(score), 24, Color.white, TextAnchor.MiddleLeft);
            float y = 14;
            if (shield) { Box(new Rect(W - 224, y, 210, 34), 0.5f); Txt(new Rect(W - 220, y, 200, 34), "SHIELD ready", 20, new Color(0.4f, 0.65f, 1f)); y += 38; }
            if (magnetT > 0) { Box(new Rect(W - 224, y, 210, 34), 0.5f); Txt(new Rect(W - 220, y, 200, 34), "MAGNET " + Mathf.CeilToInt(magnetT) + "s", 20, new Color(1f, 0.35f, 0.35f)); y += 38; }
            if (boostT > 0) { Box(new Rect(W - 224, y, 210, 34), 0.5f); Txt(new Rect(W - 220, y, 200, 34), "SPEED BOOST " + Mathf.CeilToInt(boostT) + "s", 20, new Color(1f, 0.65f, 0.2f)); y += 38; }
            if (x2T > 0) { Box(new Rect(W - 224, y, 210, 34), 0.5f); Txt(new Rect(W - 220, y, 200, 34), "SCORE x2 " + Mathf.CeilToInt(x2T) + "s", 20, new Color(0.75f, 0.45f, 0.95f)); y += 38; }
            Txt(new Rect(W * 0.5f - 200, 8, 400, 26), Stages[stageIdx].name + " - " + new[] { "Easy", "Medium", "Hard" }[difficulty], 16, new Color(1, 1, 1, 0.7f));
        }
    }
}
