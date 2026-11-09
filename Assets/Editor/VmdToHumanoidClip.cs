// ============================================================================
//  VmdToHumanoidClip.cs  (v2)
//  Taruh di:  Assets/Editor/VmdToHumanoidClip.cs   (timpa versi lama)
//  Menu    :  Tools > VMD to Humanoid Clip
//
//  Perubahan v2:
//   - Status IK kaki dibaca dari flag IK on/off di akhir file VMD
//     (sebelumnya ditebak dari jumlah keyframe -> kaki bisa salah pakai FK).
//   - Offset pose diam lengan/jari dihitung dari arah tulang asli model Unity
//     dibanding arah tulang di PMX, jadi benar untuk avatar T-pose maupun A-pose.
//   - Kolom PMX (opsional): bone yang tidak ada di PMX (mis. 上半身3) diabaikan
//     seperti di MMD/Blender, dan skala posisi dihitung otomatis dari panjang kaki.
//   - Hierarki: tambah 上半身3 dan tulang twist (腕捩/手捩).
//
//  Syarat  : Unity 2019.4+, model target punya Avatar Humanoid.
//            Kalau error encoding 932/shift_jis: Player Settings >
//            Api Compatibility Level = ".NET Framework".
// ============================================================================
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VmdTools
{
    public class VmdToHumanoidClip : EditorWindow
    {
        GameObject model;
        string vmdPath = "";
        string pmxPath = "";
        string outputFolder = "Assets/VMDClips";
        float positionScale = 0.08f;   // dipakai bila skala otomatis mati / tanpa PMX
        bool autoScale = true;         // hitung skala dari panjang kaki (butuh PMX)
        float armAngle = 37f;          // hanya dipakai bila TIDAK ada PMX
        bool forceTPose = true;        // reset model ke pose referensi avatar (muscle = 0)
        bool loop = false;
        bool useRefine = false;   // putar-ulang muscle kaki lewat solver humanoid (eksperimental)

        [MenuItem("Tools/VMD to Humanoid Clip")]
        static void Open() { GetWindow<VmdToHumanoidClip>("VMD > Humanoid"); }

        void OnGUI()
        {
            EditorGUILayout.Space();
            model = (GameObject)EditorGUILayout.ObjectField("Model Humanoid", model, typeof(GameObject), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                vmdPath = EditorGUILayout.TextField("File VMD", vmdPath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string p = EditorUtility.OpenFilePanel("Pilih file VMD", "", "vmd");
                    if (!string.IsNullOrEmpty(p)) vmdPath = p;
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                pmxPath = EditorGUILayout.TextField("File PMX (opsional)", pmxPath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string p = EditorUtility.OpenFilePanel("Pilih file PMX", "", "pmx");
                    if (!string.IsNullOrEmpty(p)) pmxPath = p;
                }
            }

            outputFolder = EditorGUILayout.TextField("Folder output", outputFolder);
            autoScale = EditorGUILayout.Toggle("Skala otomatis (butuh PMX)", autoScale);
            positionScale = EditorGUILayout.FloatField("Skala posisi manual", positionScale);
            armAngle = EditorGUILayout.Slider("Sudut lengan (tanpa PMX)", armAngle, 0f, 60f);
            forceTPose = EditorGUILayout.Toggle("Reset ke pose referensi avatar", forceTPose);
            loop = EditorGUILayout.Toggle("Loop Time", loop);
            useRefine = EditorGUILayout.Toggle("Refine kaki (eksperimental)", useRefine);

            EditorGUILayout.HelpBox(
                "Isi File PMX dengan model MMD yang dipakai VMD (mis. yang dipakai di Blender). " +
                "Dengan PMX, pose diam lengan/jari dan skala dihitung otomatis.",
                MessageType.Info);

            GUI.enabled = model != null;
            if (GUILayout.Button("Convert file VMD di atas", GUILayout.Height(28)))
                Run(new List<string> { vmdPath });

            if (GUILayout.Button("Convert VMD yang dipilih di Project window", GUILayout.Height(28)))
            {
                var list = new List<string>();
                foreach (var o in Selection.objects)
                {
                    string ap = AssetDatabase.GetAssetPath(o);
                    if (ap.EndsWith(".vmd", StringComparison.OrdinalIgnoreCase))
                        list.Add(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ap));
                }
                Run(list);
            }

            if (GUILayout.Button("Convert semua VMD di folder / pilihan Project", GUILayout.Height(28)))
            {
                var list = new List<string>();
                string root = Directory.GetParent(Application.dataPath).FullName;
                foreach (var o in Selection.objects)
                {
                    string ap = AssetDatabase.GetAssetPath(o);
                    string full = Path.Combine(root, ap);
                    if (Directory.Exists(full))
                        list.AddRange(Directory.GetFiles(full, "*.vmd", SearchOption.AllDirectories));
                    else if (ap.EndsWith(".vmd", StringComparison.OrdinalIgnoreCase))
                        list.Add(full);
                }
                Run(list);
            }
            GUI.enabled = true;
        }

        void Run(List<string> paths)
        {
            try
            {
                EnsureFolder(outputFolder);
                int ok = 0;
                foreach (var p in paths)
                {
                    if (string.IsNullOrEmpty(p) || !File.Exists(p)) { Debug.LogWarning("VMD tidak ditemukan: " + p); continue; }
                    var job = new Job
                    {
                        path = p, pmxPath = pmxPath, model = model,
                        positionScale = positionScale, autoScale = autoScale,
                        armAngle = armAngle, forceTPose = forceTPose, useRefine = useRefine
                    };
                    AnimationClip clip = job.Run();

                    var st = AnimationUtility.GetAnimationClipSettings(clip);
                    st.loopTime = loop;
                    useRefine = EditorGUILayout.Toggle("Refine kaki (eksperimental)", useRefine);
                    st.keepOriginalOrientation = true;
                    st.keepOriginalPositionY = true;
                    st.keepOriginalPositionXZ = true;
                    AnimationUtility.SetAnimationClipSettings(clip, st);

                    string name = Path.GetFileNameWithoutExtension(p);
                    clip.name = name;
                    string assetPath = AssetDatabase.GenerateUniqueAssetPath(outputFolder + "/" + name + ".anim");
                    AssetDatabase.CreateAsset(clip, assetPath);
                    Debug.Log("Clip dibuat: " + assetPath);
                    ok++;
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                if (ok > 0) EditorUtility.DisplayDialog("Selesai", ok + " clip berhasil dibuat di " + outputFolder, "OK");
            }
            catch (Exception e)
            {
                Debug.LogError("VMD convert gagal: " + e);
                EditorUtility.DisplayDialog("Gagal", e.Message, "OK");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ====================================================================
        //  Util nama
        // ====================================================================
        // full-width -> half-width (ＩＫ -> IK, ０ -> 0)
        static string Norm(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c >= 0xFF01 && c <= 0xFF5E) sb.Append((char)(c - 0xFEE0));
                else if (c == 0x3000) sb.Append(' ');
                else sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        static Encoding Sjis()
        {
            try { return Encoding.GetEncoding(932); }
            catch { return Encoding.GetEncoding("shift_jis"); }
        }

        static string ReadName(BinaryReader br, int len, Encoding enc)
        {
            byte[] b = br.ReadBytes(len);
            int n = Array.IndexOf(b, (byte)0);
            if (n < 0) n = b.Length;
            return Norm(enc.GetString(b, 0, n));
        }

        // ====================================================================
        //  PMX (hanya posisi bone)
        // ====================================================================
        static int ReadIdx(BinaryReader br, int size)
        {
            if (size == 1) return br.ReadSByte();
            if (size == 2) return br.ReadInt16();
            return br.ReadInt32();
        }

        // Hasil: nama bone (ternormalisasi) -> posisi diam, sudah dalam koordinat Unity (-x, y, -z)
        static Dictionary<string, Vector3> LoadPmx(string path)
        {
            var res = new Dictionary<string, Vector3>();
            using (var br = new BinaryReader(File.OpenRead(path)))
            {
                if (new string(br.ReadChars(4)) != "PMX ") throw new Exception("Bukan file PMX (PMD lama tidak didukung): " + path);
                br.ReadSingle(); // versi
                int gn = br.ReadByte();
                byte[] g = br.ReadBytes(gn);
                bool utf8 = g[0] == 1;
                int addUV = g[1], vs = g[2], ts = g[3], bs = g[5];
                Func<string> text = () =>
                {
                    int len = br.ReadInt32();
                    byte[] b = br.ReadBytes(len);
                    return (utf8 ? Encoding.UTF8 : Encoding.Unicode).GetString(b);
                };
                for (int i = 0; i < 4; i++) text();

                // vertex
                int vc = br.ReadInt32();
                for (int i = 0; i < vc; i++)
                {
                    br.BaseStream.Seek(32 + addUV * 16, SeekOrigin.Current);
                    byte wt = br.ReadByte();
                    int skip;
                    switch (wt)
                    {
                        case 0: skip = bs; break;
                        case 1: skip = 2 * bs + 4; break;
                        case 2: skip = 4 * bs + 16; break;
                        case 3: skip = 2 * bs + 4 + 36; break;
                        default: skip = 4 * bs + 16; break;
                    }
                    br.BaseStream.Seek(skip + 4, SeekOrigin.Current); // + edge scale
                }
                // face
                int ic = br.ReadInt32();
                br.BaseStream.Seek((long)ic * vs, SeekOrigin.Current);
                // texture
                int tc = br.ReadInt32();
                for (int i = 0; i < tc; i++) text();
                // material
                int mc = br.ReadInt32();
                for (int i = 0; i < mc; i++)
                {
                    text(); text();
                    br.BaseStream.Seek(65, SeekOrigin.Current);
                    br.BaseStream.Seek(ts * 2 + 1, SeekOrigin.Current);
                    byte tog = br.ReadByte();
                    br.BaseStream.Seek(tog == 0 ? ts : 1, SeekOrigin.Current);
                    text();
                    br.BaseStream.Seek(4, SeekOrigin.Current);
                }
                // bone
                int bc = br.ReadInt32();
                for (int i = 0; i < bc; i++)
                {
                    string name = Norm(text()); text();
                    float x = br.ReadSingle(), y = br.ReadSingle(), z = br.ReadSingle();
                    ReadIdx(br, bs);
                    br.ReadInt32();
                    ushort fl = br.ReadUInt16();
                    if ((fl & 0x1) != 0) ReadIdx(br, bs); else br.BaseStream.Seek(12, SeekOrigin.Current);
                    if ((fl & 0x300) != 0) { ReadIdx(br, bs); br.ReadSingle(); }
                    if ((fl & 0x400) != 0) br.BaseStream.Seek(12, SeekOrigin.Current);
                    if ((fl & 0x800) != 0) br.BaseStream.Seek(24, SeekOrigin.Current);
                    if ((fl & 0x2000) != 0) br.BaseStream.Seek(4, SeekOrigin.Current);
                    if ((fl & 0x20) != 0)
                    {
                        ReadIdx(br, bs); br.ReadInt32(); br.ReadSingle();
                        int lc = br.ReadInt32();
                        for (int k = 0; k < lc; k++)
                        {
                            ReadIdx(br, bs);
                            if (br.ReadByte() != 0) br.BaseStream.Seek(24, SeekOrigin.Current);
                        }
                    }
                    if (!res.ContainsKey(name)) res[name] = new Vector3(-x, y, -z);
                }
            }
            return res;
        }

        // ====================================================================
        //  VMD
        // ====================================================================
        class Key { public int frame; public Vector3 pos; public Quaternion rot; public byte[] ip; }

        class Track
        {
            public List<Key> keys = new List<Key>();

            public void Sort() { keys.Sort((a, b) => a.frame.CompareTo(b.frame)); }

            public bool HasMotion()
            {
                foreach (var k in keys)
                    if (k.pos.sqrMagnitude > 1e-8f || Quaternion.Angle(k.rot, Quaternion.identity) > 0.01f) return true;
                return false;
            }

            public void Sample(int f, out Vector3 pos, out Quaternion rot)
            {
                int n = keys.Count;
                if (n == 0) { pos = Vector3.zero; rot = Quaternion.identity; return; }
                if (f <= keys[0].frame) { pos = keys[0].pos; rot = keys[0].rot; return; }
                if (f >= keys[n - 1].frame) { pos = keys[n - 1].pos; rot = keys[n - 1].rot; return; }

                int lo = 0, hi = n - 1;
                while (hi - lo > 1)
                {
                    int mid = (lo + hi) / 2;
                    if (keys[mid].frame <= f) lo = mid; else hi = mid;
                }
                Key k0 = keys[lo], k1 = keys[hi];
                float s = (f - k0.frame) / (float)(k1.frame - k0.frame);
                pos = new Vector3(
                    Mathf.Lerp(k0.pos.x, k1.pos.x, Bez(k1.ip, 0, s)),
                    Mathf.Lerp(k0.pos.y, k1.pos.y, Bez(k1.ip, 1, s)),
                    Mathf.Lerp(k0.pos.z, k1.pos.z, Bez(k1.ip, 2, s)));
                rot = Quaternion.Slerp(k0.rot, k1.rot, Bez(k1.ip, 3, s));
            }
        }

        class IkState { public int frame; public Dictionary<string, bool> on = new Dictionary<string, bool>(); }

        class Vmd
        {
            public Dictionary<string, Track> tracks = new Dictionary<string, Track>();
            public int lastFrame;
            public List<IkState> ik = new List<IkState>();
        }

        // Interpolasi bezier VMD. ch: 0=X 1=Y 2=Z 3=Rotasi
        static float Bez(byte[] ip, int ch, float s)
        {
            float x1 = ip[ch] / 127f, y1 = ip[4 + ch] / 127f, x2 = ip[8 + ch] / 127f, y2 = ip[12 + ch] / 127f;
            if (Mathf.Approximately(x1, y1) && Mathf.Approximately(x2, y2)) return s;
            float lo = 0f, hi = 1f, t = s;
            for (int i = 0; i < 24; i++)
            {
                float x = B3(t, x1, x2);
                if (x < s) lo = t; else hi = t;
                t = (lo + hi) * 0.5f;
            }
            return B3(t, y1, y2);
        }
        static float B3(float t, float p1, float p2)
        {
            float u = 1f - t;
            return 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t;
        }

        // allowed != null -> hanya bone yang ada di PMX (perilaku MMD/Blender)
        static Vmd LoadVmd(string path, HashSet<string> allowed)
        {
            var enc = Sjis();
            var v = new Vmd();
            using (var br = new BinaryReader(File.OpenRead(path)))
            {
                string header = ReadName(br, 30, enc);
                if (!header.StartsWith("Vocaloid Motion Data")) throw new Exception("Bukan file VMD yang valid: " + path);
                br.ReadBytes(header.Contains("0002") ? 20 : 10); // nama model

                uint count = br.ReadUInt32();
                for (uint i = 0; i < count; i++)
                {
                    string name = ReadName(br, 15, enc);
                    int frame = (int)br.ReadUInt32();
                    float px = br.ReadSingle(), py = br.ReadSingle(), pz = br.ReadSingle();
                    float qx = br.ReadSingle(), qy = br.ReadSingle(), qz = br.ReadSingle(), qw = br.ReadSingle();
                    byte[] ip = br.ReadBytes(64);

                    if (allowed != null && !allowed.Contains(name)) continue;

                    // MMD (model menghadap -Z) -> Unity (menghadap +Z): putar 180 derajat sumbu Y
                    var q = new Quaternion(-qx, qy, -qz, qw);
                    float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                    if (m > 1e-6f) q = new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m); else q = Quaternion.identity;

                    Track t;
                    if (!v.tracks.TryGetValue(name, out t)) { t = new Track(); v.tracks[name] = t; }
                    t.keys.Add(new Key { frame = frame, pos = new Vector3(-px, py, -pz), rot = q, ip = ip });
                    if (frame > v.lastFrame) v.lastFrame = frame;
                }

                // bagian akhir: morph, kamera, cahaya, bayangan, lalu flag IK on/off
                try
                {
                    uint mc = br.ReadUInt32(); br.BaseStream.Seek(mc * 23L, SeekOrigin.Current);
                    uint cc = br.ReadUInt32(); br.BaseStream.Seek(cc * 61L, SeekOrigin.Current);
                    uint lc = br.ReadUInt32(); br.BaseStream.Seek(lc * 28L, SeekOrigin.Current);
                    uint sc = br.ReadUInt32(); br.BaseStream.Seek(sc * 9L, SeekOrigin.Current);
                    uint pc = br.ReadUInt32();
                    for (uint i = 0; i < pc; i++)
                    {
                        var st = new IkState { frame = (int)br.ReadUInt32() };
                        br.ReadByte(); // show
                        uint ic = br.ReadUInt32();
                        for (uint j = 0; j < ic; j++)
                        {
                            string nm = ReadName(br, 20, enc);
                            st.on[nm] = br.ReadByte() != 0;
                        }
                        v.ik.Add(st);
                    }
                    v.ik.Sort((a, b) => a.frame.CompareTo(b.frame));
                }
                catch (EndOfStreamException) { }
            }
            foreach (var t in v.tracks.Values) t.Sort();
            return v;
        }

        // ====================================================================
        //  Tabel pemetaan MMD <-> Humanoid
        // ====================================================================
        class BoneDef
        {
            public HumanBodyBones bone; public string mmd;
            public int arm;                                   // +1 kiri, -1 kanan, 0 bukan lengan/jari
            public HumanBodyBones next = HumanBodyBones.LastBone; // tulang anak (arah segmen), LastBone = ujung
            public string mmdNext;                            // nama bone PMX untuk arah segmen
        }

        // PENTING: array ini harus dideklarasikan SEBELUM Defs/Parent
        static readonly string[] FingerJp = { "親指", "人指", "中指", "薬指", "小指" };
        static readonly string[] FingerEn = { "Thumb", "Index", "Middle", "Ring", "Little" };
        static readonly string[] FingerPart = { "Proximal", "Intermediate", "Distal" };

        static readonly List<BoneDef> Defs = BuildDefs();
        static readonly Dictionary<string, string> Parent = BuildParents();

        static HumanBodyBones Hb(string n) { return (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), n); }

        static List<BoneDef> BuildDefs()
        {
            var l = new List<BoneDef>();
            const HumanBodyBones NONE = HumanBodyBones.LastBone;
            Action<HumanBodyBones, string, int, HumanBodyBones, string> A =
                (b, m, arm, nx, mn) => l.Add(new BoneDef { bone = b, mmd = m, arm = arm, next = nx, mmdNext = mn });

            A(HumanBodyBones.Hips, "下半身", 0, NONE, null);
            A(HumanBodyBones.Spine, "上半身", 0, NONE, null);
            A(HumanBodyBones.Chest, "上半身2", 0, NONE, null);
            A(HumanBodyBones.UpperChest, "上半身2", 0, NONE, null);
            A(HumanBodyBones.Neck, "首", 0, NONE, null);
            A(HumanBodyBones.Head, "頭", 0, NONE, null);

            string[][] sides = { new[] { "Left", "左", "1" }, new[] { "Right", "右", "-1" } };
            foreach (var s in sides)
            {
                string en = s[0], jp = s[1];
                int sign = int.Parse(s[2]);
                A(Hb(en + "Shoulder"), jp + "肩", 0, NONE, null);
                A(Hb(en + "UpperArm"), jp + "腕", sign, Hb(en + "LowerArm"), jp + "ひじ");
                A(Hb(en + "LowerArm"), jp + "ひじ", sign, Hb(en + "Hand"), jp + "手首");
                A(Hb(en + "Hand"), jp + "手首", sign, Hb(en + "MiddleProximal"), jp + "中指1");
                A(Hb(en + "UpperLeg"), jp + "足", 0, Hb(en + "LowerLeg"), jp + "ひざ");
                A(Hb(en + "LowerLeg"), jp + "ひざ", 0, Hb(en + "Foot"), jp + "足首");
                A(Hb(en + "Foot"), jp + "足首", 0, NONE, null);
                for (int f = 0; f < 5; f++)
                    for (int j = 0; j < 3; j++)
                    {
                        string mmd = f == 0 ? jp + "親指" + j : jp + FingerJp[f] + (j + 1);
                        HumanBodyBones nx = NONE;
                        string mn;
                        if (j < 2)
                        {
                            nx = Hb(en + FingerEn[f] + FingerPart[j + 1]);
                            mn = f == 0 ? jp + "親指" + (j + 1) : jp + FingerJp[f] + (j + 2);
                        }
                        else
                            mn = jp + (f == 0 ? "親指先" : f == 1 ? "人差し指先" : FingerJp[f] + "先");
                        A(Hb(en + FingerEn[f] + FingerPart[j]), mmd, sign, nx, mn);
                    }
            }
            return l;
        }

        static Dictionary<string, string> BuildParents()
        {
            var p = new Dictionary<string, string>
            {
                {"センター","全ての親"}, {"グルーブ","センター"}, {"腰","グルーブ"},
                {"上半身","腰"}, {"上半身2","上半身"}, {"上半身3","上半身2"},
                {"首","上半身3"}, {"頭","首"},
                {"下半身","腰"}
            };
            foreach (var s in new[] { "左", "右" })
            {
                p[s + "肩"] = "上半身3"; p[s + "腕"] = s + "肩";
                p[s + "腕捩"] = s + "腕"; p[s + "ひじ"] = s + "腕捩";
                p[s + "手捩"] = s + "ひじ"; p[s + "手首"] = s + "手捩";
                p[s + "足"] = "下半身"; p[s + "ひざ"] = s + "足"; p[s + "足首"] = s + "ひざ";
                p[s + "足IK"] = "全ての親";
                p[s + "親指0"] = s + "手首"; p[s + "親指1"] = s + "親指0"; p[s + "親指2"] = s + "親指1";
                for (int i = 1; i < 5; i++)
                {
                    p[s + FingerJp[i] + "1"] = s + "手首";
                    p[s + FingerJp[i] + "2"] = s + FingerJp[i] + "1";
                    p[s + FingerJp[i] + "3"] = s + FingerJp[i] + "2";
                }
            }
            return p;
        }

        // Nama muscle HumanTrait -> nama properti di AnimationClip
        static string ClipPropName(string muscle)
        {
            foreach (var hand in new[] { "Left", "Right" })
                foreach (var finger in FingerEn)
                {
                    string prefix = hand + " " + finger + " ";
                    if (muscle.StartsWith(prefix))
                        return hand + "Hand." + finger + "." + muscle.Substring(prefix.Length);
                }
            return muscle;
        }

        // ====================================================================
        //  Proses konversi
        // ====================================================================
        class Bone
        {
            public BoneDef def; public Transform t; public Quaternion restWorld; public Quaternion offset; public int depth;
        }

        class Job
        {
            public string path, pmxPath; public GameObject model;
            public float positionScale, armAngle; public bool forceTPose, autoScale;
            public bool useRefine;

            Vmd vmd;
            readonly Dictionary<string, Quaternion> cache = new Dictionary<string, Quaternion>();
            int frame;
            static readonly string[] SideEn = { "Left", "Right" };
            static readonly string[] SideJp = { "左", "右" };
            bool[] ikHeur = new bool[2];

            Quaternion WorldDelta(string name)
            {
                Quaternion q;
                if (cache.TryGetValue(name, out q)) return q;
                Quaternion local = Quaternion.identity;
                Track tr; Vector3 p;
                if (vmd.tracks.TryGetValue(name, out tr)) tr.Sample(frame, out p, out local);
                string par;
                q = Parent.TryGetValue(name, out par) ? WorldDelta(par) * local : local;
                cache[name] = q;
                return q;
            }

            Vector3 PosOf(string name)
            {
                Track tr; Vector3 p; Quaternion r;
                if (vmd.tracks.TryGetValue(name, out tr)) { tr.Sample(frame, out p, out r); return p; }
                return Vector3.zero;
            }

            bool IkOn(int side, int f)
            {
                if (vmd.ik.Count > 0)
                {
                    var st = vmd.ik[0].on;
                    foreach (var e in vmd.ik) { if (e.frame <= f) st = e.on; else break; }
                    bool v;
                    if (st.TryGetValue(SideJp[side] + "足IK", out v)) return v;
                }
                return ikHeur[side]; // tidak ada flag: tebak dari isi track IK
            }

            static int Depth(Transform t) { int d = 0; while (t.parent != null) { d++; t = t.parent; } return d; }

            static readonly string[] LegMuscleSuffix = {
                " Upper Leg Front-Back", " Upper Leg In-Out", " Upper Leg Twist In-Out",
                " Lower Leg Stretch", " Lower Leg Twist In-Out",
                " Foot Up-Down", " Foot Twist In-Out" };

            static float Norm(float[] v) { float s = 0f; for (int i = 0; i < v.Length; i++) s += v[i] * v[i]; return Mathf.Sqrt(s); }

            static float[] SolveLinear(float[,] A, float[] b, int n)
            {
                for (int i = 0; i < n; i++)
                {
                    int piv = i;
                    for (int k = i + 1; k < n; k++) if (Mathf.Abs(A[k, i]) > Mathf.Abs(A[piv, i])) piv = k;
                    if (Mathf.Abs(A[piv, i]) < 1e-9f) continue;
                    if (piv != i)
                    {
                        for (int c = 0; c < n; c++) { float t = A[i, c]; A[i, c] = A[piv, c]; A[piv, c] = t; }
                        float tb = b[i]; b[i] = b[piv]; b[piv] = tb;
                    }
                    for (int k = i + 1; k < n; k++)
                    {
                        float f = A[k, i] / A[i, i];
                        for (int c = i; c < n; c++) A[k, c] -= f * A[i, c];
                        b[k] -= f * b[i];
                    }
                }
                var x = new float[n];
                for (int i = n - 1; i >= 0; i--)
                {
                    float s = b[i];
                    for (int c = i + 1; c < n; c++) s -= A[i, c] * x[c];
                    x[i] = Mathf.Abs(A[i, i]) < 1e-9f ? 0f : s / A[i, i];
                }
                return x;
            }

            // Cari muscle satu kaki lewat solver humanoid asli agar pergelangan & telapak tepat di target.
            // pose.muscles (array) diubah langsung. Mengembalikan galat posisi akhir (meter).
            static float RefineLeg(HumanPoseHandler h, HumanPose pose, int[] mi, Transform foot,
                                Vector3 tPos, Quaternion tRot, float legLen, out float errBefore)
            {
                const int N = 7, M = 6;
                float[] x = new float[N];
                for (int i = 0; i < N; i++) x[i] = pose.muscles[mi[i]];

                Func<float[], float[]> eval = xs =>
                {
                    for (int i = 0; i < N; i++) pose.muscles[mi[i]] = Mathf.Clamp(xs[i], -1f, 1f);
                    h.SetHumanPose(ref pose);
                    Vector3 e = (foot.position - tPos) / legLen;
                    Quaternion dq = foot.rotation * Quaternion.Inverse(tRot);
                    float ang; Vector3 ax;
                    dq.ToAngleAxis(out ang, out ax);
                    if (ang > 180f) ang -= 360f;
                    Vector3 rv = ax * (ang * Mathf.Deg2Rad * 0.15f);
                    return new[] { e.x, e.y, e.z, rv.x, rv.y, rv.z };
                };

                float[] r0 = eval(x);
                errBefore = new Vector3(r0[0], r0[1], r0[2]).magnitude * legLen;
                float n0 = Norm(r0), lambda = 0.05f;

                for (int it = 0; it < 10 && n0 > 2e-3f; it++)
                {
                    var J = new float[M, N];
                    for (int j = 0; j < N; j++)
                    {
                        float[] xp = (float[])x.Clone();
                        xp[j] = Mathf.Clamp(x[j] + 0.02f, -1f, 1f);
                        float d = xp[j] - x[j];
                        if (Mathf.Abs(d) < 1e-4f) { xp[j] = Mathf.Clamp(x[j] - 0.02f, -1f, 1f); d = xp[j] - x[j]; }
                        float[] rp = eval(xp);
                        for (int i = 0; i < M; i++) J[i, j] = (rp[i] - r0[i]) / d;
                    }
                    var A = new float[N, N]; var b = new float[N];
                    for (int a = 0; a < N; a++)
                    {
                        for (int c = 0; c < N; c++)
                        {
                            float s = 0f;
                            for (int i = 0; i < M; i++) s += J[i, a] * J[i, c];
                            A[a, c] = s;
                        }
                        A[a, a] += lambda;
                        float sb = 0f;
                        for (int i = 0; i < M; i++) sb += J[i, a] * r0[i];
                        b[a] = -sb;
                    }
                    float[] dx = SolveLinear(A, b, N);
                    float[] xn = new float[N];
                    for (int i = 0; i < N; i++) xn[i] = Mathf.Clamp(x[i] + dx[i], -1f, 1f);
                    float[] rn = eval(xn);
                    float nn = Norm(rn);
                    if (nn < n0) { x = xn; r0 = rn; n0 = nn; lambda = Mathf.Max(lambda * 0.5f, 1e-4f); }
                    else lambda *= 4f;
                }
                eval(x);   // pastikan pose dan array muscle = solusi terbaik
                return (foot.position - tPos).magnitude;
            }
            // IK kaki dengan lutut sebagai engsel murni (seperti batas sumbu X di MMD)
            // Mengembalikan jarak sisa pergelangan ke target (0 = tepat, >0 = di luar jangkauan kaki)
            static float LegIK(Transform a, Transform b, Transform c, Vector3 hingeLocal, Vector3 target)
            {
                Vector3 ap = a.position, bp = b.position;
                Vector3 shin0 = c.position - bp;                    // vektor betis (lutut -> pergelangan)
                float l1 = (bp - ap).magnitude, l2 = shin0.magnitude;
                Vector3 toTarget = target - ap;
                if (toTarget.sqrMagnitude < 1e-8f) return 0f;
                float want = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);

                // 1) fleksi lutut murni di sumbu engsel milik paha, sudutnya dicari numerik
                Vector3 hinge = a.rotation * hingeLocal;
                Func<float, float> chord = deg => ((bp + Quaternion.AngleAxis(deg, hinge) * shin0) - ap).magnitude;

                float lo = -40f, best = chord(lo);
                for (float p = -35f; p <= 140f; p += 5f)            // titik paling lurus = jarak terjauh
                {
                    float v = chord(p);
                    if (v > best) { best = v; lo = p; }
                }
                float phi = lo;
                if (want < best)
                {
                    float hi = lo + 175f;
                    for (int i = 0; i < 32; i++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        if (chord(mid) > want) lo = mid; else hi = mid;
                    }
                    phi = (lo + hi) * 0.5f;
                }
                b.rotation = Quaternion.AngleAxis(phi, hinge) * b.rotation;

                // 2) putar seluruh kaki dari pinggul agar pergelangan menuju target
                a.rotation = Quaternion.FromToRotation(c.position - ap, toTarget) * a.rotation;
                return (c.position - target).magnitude;
            }

            static AnimationCurve MakeCurve(float[] v, float dt)
            {
                var keys = new Keyframe[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    float tin = i > 0 ? (v[i] - v[i - 1]) / dt : 0f;
                    float tout = i < v.Length - 1 ? (v[i + 1] - v[i]) / dt : 0f;
                    keys[i] = new Keyframe(i * dt, v[i], tin, tout);
                }
                return new AnimationCurve(keys);
            }

            public AnimationClip Run()
            {
                Dictionary<string, Vector3> pmx = null;
                if (!string.IsNullOrEmpty(pmxPath) && File.Exists(pmxPath)) pmx = LoadPmx(pmxPath);

                vmd = LoadVmd(path, pmx != null ? new HashSet<string>(pmx.Keys) : null);
                int lastFrame = vmd.lastFrame;
                if (lastFrame <= 0) throw new Exception("VMD tidak punya keyframe bone yang bisa dipakai.");

                for (int s = 0; s < 2; s++)
                {
                    Track tr;
                    ikHeur[s] = vmd.tracks.TryGetValue(SideJp[s] + "足IK", out tr) && tr.HasMotion();
                }

                GameObject go = UnityEngine.Object.Instantiate(model);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                HumanPoseHandler handler = null;
                try
                {
                    Animator anim = go.GetComponentInChildren<Animator>();
                    if (anim == null || anim.avatar == null || !anim.avatar.isHuman)
                        throw new Exception("Model harus punya Animator dengan Avatar tipe Humanoid.");

                    handler = new HumanPoseHandler(anim.avatar, anim.transform);
                    var pose = new HumanPose();
                    handler.GetHumanPose(ref pose);
                    if (forceTPose)
                    {
                        pose.muscles = new float[pose.muscles.Length];
                        pose.bodyRotation = Quaternion.identity;
                        handler.SetHumanPose(ref pose);
                    }

                    // ---- catat pose diam model ----
                    var bones = new List<Bone>();
                    foreach (var d in Defs)
                    {
                        Transform t = anim.GetBoneTransform(d.bone);
                        if (t == null) continue;
                        bones.Add(new Bone { def = d, t = t, restWorld = t.rotation, depth = Depth(t), offset = Quaternion.identity });
                    }
                    bones.Sort((a, b) => a.depth.CompareTo(b.depth));
                    var byT = new Dictionary<Transform, Bone>();
                    foreach (var bb in bones) byT[bb.t] = bb;

                    // ---- offset pose diam lengan & jari: arah tulang model -> arah tulang MMD ----
                    var offByT = new Dictionary<Transform, Quaternion>();
                    foreach (var b in bones)
                    {
                        if (b.def.arm == 0 && b.def.mmdNext == null) continue;
                        var d = b.def;
                        // kaki: pose diam MMD dan T-pose sama-sama lurus ke bawah -> tanpa offset (seperti versi pelvis)
                        if (d.bone == HumanBodyBones.LeftUpperLeg  || d.bone == HumanBodyBones.RightUpperLeg ||
                            d.bone == HumanBodyBones.LeftLowerLeg  || d.bone == HumanBodyBones.RightLowerLeg)
                        {
                            b.offset = Quaternion.identity;
                            continue;
                        }
                        string jp = d.arm > 0 ? "左" : "右", en = d.arm > 0 ? "Left" : "Right";

                        Transform next = d.next != HumanBodyBones.LastBone
                            ? anim.GetBoneTransform(d.next)
                            : (b.t.childCount > 0 ? b.t.GetChild(0) : null);
                        string mNext = d.mmdNext;
                        bool isHand = d.bone == HumanBodyBones.LeftHand || d.bone == HumanBodyBones.RightHand;
                        if (isHand && next == null)
                        {
                            next = anim.GetBoneTransform(Hb(en + "IndexProximal"));
                            mNext = jp + "人指1";
                        }

                        // arah segmen di MMD (dari PMX), atau arah A-pose standar bila tanpa PMX
                        Vector3 mDir = Vector3.zero;
                        Vector3 p0, p1;
                        if (pmx != null && mNext != null && pmx.TryGetValue(d.mmd, out p0) && pmx.TryGetValue(mNext, out p1))
                            mDir = p1 - p0;
                        if (isHand && pmx != null && mDir.sqrMagnitude < 1e-8f && pmx.TryGetValue(d.mmd, out p0) && pmx.TryGetValue(jp + "人指1", out p1))
                            mDir = p1 - p0;
                        if (mDir.sqrMagnitude < 1e-8f)
                        {
                            float rad = armAngle * Mathf.Deg2Rad;
                            mDir = new Vector3(-d.arm * Mathf.Cos(rad), -Mathf.Sin(rad), 0f);
                        }

                        Quaternion off;
                        Vector3 tDir = next != null ? next.position - b.t.position : Vector3.zero;
                        if (next != null && tDir.sqrMagnitude > 1e-10f)
                            off = Quaternion.FromToRotation(tDir, mDir);
                        else if (b.t.parent != null && offByT.TryGetValue(b.t.parent, out off)) { }
                        else off = Quaternion.identity;

                        b.offset = off;
                        offByT[b.t] = off;
                    }

                    Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                    Vector3 hipsRestPos = hips.position;

                    // ---- siapkan IK kaki ----
                    Transform[] thigh = new Transform[2], knee = new Transform[2], foot = new Transform[2];
                    Vector3[] ankleRest = new Vector3[2];
                    Quaternion[] footRest = new Quaternion[2];
                    Vector3[] hingeLocal = new Vector3[2];
                    // salinan pose diam (muscle = 0) untuk memulihkan model setelah kalibrasi
                    var restPose = new HumanPose
                    {
                        bodyPosition = pose.bodyPosition,
                        bodyRotation = pose.bodyRotation,
                        muscles = (float[])pose.muscles.Clone()
                    };
                    Transform lThigh = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                    Transform rThigh = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    Vector3 bodyFwd = (lThigh != null && rThigh != null)
                        ? Vector3.Cross(Vector3.up, lThigh.position - rThigh.position).normalized
                        : anim.transform.forward;
                    bool[] legOk = new bool[2];
                    for (int s = 0; s < 2; s++)
                    {
                        thigh[s] = anim.GetBoneTransform(Hb(SideEn[s] + "UpperLeg"));
                        knee[s] = anim.GetBoneTransform(Hb(SideEn[s] + "LowerLeg"));
                        foot[s] = anim.GetBoneTransform(Hb(SideEn[s] + "Foot"));
                        legOk[s] = thigh[s] != null && knee[s] != null && foot[s] != null;
                        if (!legOk[s]) continue;
                        ankleRest[s] = foot[s].position;
                        footRest[s] = foot[s].rotation;

                        
                        // tandanya dipilih agar menekuk lutut = pergelangan bergerak KE BELAKANG
                        // (sama seperti pole = pelvis*forward di versi pelvis)
                        Vector3 fwdRef = anim.transform.forward;
                        Vector3 legDir = (knee[s].position - thigh[s].position).normalized;
                        Vector3 hingeW = Vector3.Cross(fwdRef, legDir).normalized;

                        Vector3 shinRest = foot[s].position - knee[s].position;
                        Vector3 moved = Quaternion.AngleAxis(20f, hingeW) * shinRest;
                        if (Vector3.Dot(moved - shinRest, fwdRef) > 0f) hingeW = -hingeW;

                        Debug.Log("[VMD] engsel lutut " + SideEn[s] + " = " + hingeW.ToString("F2"));
                        hingeLocal[s] = Quaternion.Inverse(thigh[s].rotation) * hingeW;
                    }

                    // ---- skala posisi ----
                    float scale = positionScale;
                    Vector3 pa, pb;
                    if (autoScale && pmx != null && legOk[0] &&
                        pmx.TryGetValue("左足", out pa) && pmx.TryGetValue("左足首", out pb))
                    {
                        float mmdLeg = pa.y - pb.y;
                        float tgtLeg = thigh[0].position.y - foot[0].position.y;
                        if (mmdLeg > 0.01f && tgtLeg > 0.01f) scale = tgtLeg / mmdLeg;
                    }
                    Debug.Log("[VMD] skala posisi = " + scale.ToString("F4") +
                              " | IK kiri f0 = " + (legOk[0] && IkOn(0, 0)) + ", IK kanan f0 = " + (legOk[1] && IkOn(1, 0)) +
                              " | PMX = " + (pmx != null));
                    Debug.Log("[VMD] arah hadap badan = " + bodyFwd.ToString("F2") +
                            " | x paha kiri = " + (lThigh != null ? lThigh.position.x.ToString("F2") : "-") +
                            ", x paha kanan = " + (rThigh != null ? rThigh.position.x.ToString("F2") : "-"));
                    if (Vector3.Dot(bodyFwd, Vector3.forward) < 0.5f)
                        Debug.LogWarning("[VMD] Model tidak menghadap +Z. Konversi MMD mengasumsikan +Z, jadi kaki kiri/kanan bisa tertukar.");
                    // ---- penampung kurva ----
                    int muscleCount = HumanTrait.MuscleCount;
                    int channels = 7 + muscleCount;
                    var data = new float[channels][];
                    for (int i = 0; i < channels; i++) data[i] = new float[lastFrame + 1];

                    Quaternion prevQ = Quaternion.identity;
                    Vector3 forward = anim.transform.forward;

                    int[][] legMi = new int[2][];
                    bool[] refine = new bool[2];
                    for (int s = 0; s < 2; s++)
                    {
                        legMi[s] = new int[LegMuscleSuffix.Length];
                        bool okm = true;
                        for (int k = 0; k < LegMuscleSuffix.Length; k++)
                        {
                            legMi[s][k] = Array.IndexOf(HumanTrait.MuscleName, SideEn[s] + LegMuscleSuffix[k]);
                            if (legMi[s][k] < 0) okm = false;
                        }
                        refine[s] = useRefine && okm && legOk[s];
                    }
                    Vector3[] tgtPos = new Vector3[2];
                    Quaternion[] tgtRot = new Quaternion[2];
                    bool[] tgtOn = new bool[2];
                    float legLen = legOk[0] ? (thigh[0].position - foot[0].position).magnitude
                                : legOk[1] ? (thigh[1].position - foot[1].position).magnitude : 0.8f;

                    for (int f = 0; f <= lastFrame; f++)
                    {
                        if (f % 60 == 0 &&
                            EditorUtility.DisplayCancelableProgressBar("VMD > Humanoid", Path.GetFileName(path), f / (float)lastFrame))
                            throw new OperationCanceledException("Dibatalkan.");

                        frame = f;
                        cache.Clear();

                        // posisi root
                        Vector3 move = PosOf("全ての親") + PosOf("センター") + PosOf("グルーブ");
                        hips.position = hipsRestPos + move * scale;

                        // rotasi semua tulang humanoid (FK), urut dari induk ke anak
                        foreach (var b in bones)
                            b.t.rotation = WorldDelta(b.def.mmd) * b.offset * b.restWorld;


                        tgtOn[0] = tgtOn[1] = false;
                        // kaki IK (bila flag IK menyala pada frame ini)
                        Quaternion pelvis = WorldDelta("下半身");
                        for (int s = 0; s < 2; s++)
                        {
                            if (!legOk[s] || !IkOn(s, f)) continue;
                            string ikName = SideJp[s] + "足IK";

                            // buang nilai FK paha/lutut: titik awal = ikut pinggul saja (seperti MMD)
                            Bone bt = byT[thigh[s]], bk = byT[knee[s]];
                            thigh[s].rotation = pelvis * bt.offset * bt.restWorld;
                            knee[s].rotation  = pelvis * bk.offset * (bk.restWorld);

                            Vector3 target = ankleRest[s] + (PosOf("全ての親") + PosOf(ikName)) * scale;
                            float err = LegIK(thigh[s], knee[s], foot[s], hingeLocal[s], target);
                            if (err > 0.01f) Debug.LogWarning("Frame " + f + " kaki " + SideEn[s] + " tak terjangkau: " + err.ToString("F3") + " m");
                            foot[s].rotation = WorldDelta(ikName) * footRest[s];
                            tgtOn[s] = true;
                            tgtPos[s] = target;
                            tgtRot[s] = WorldDelta(ikName) * footRest[s];
                        }

                        // baca pose humanoid
                        handler.GetHumanPose(ref pose);
                        for (int s = 0; s < 2; s++)
                        {
                            if (!tgtOn[s] || !refine[s]) continue;
                            float eBefore;
                            float eAfter = RefineLeg(handler, pose, legMi[s], foot[s], tgtPos[s], tgtRot[s], legLen, out eBefore);
                            if (f == 0 || eAfter > 0.02f)
                                Debug.Log("[VMD] frame " + f + " kaki " + SideEn[s] + ": galat pergelangan setelah putar-ulang humanoid = "
                                        + eBefore.ToString("F3") + " m, setelah refine = " + eAfter.ToString("F3") + " m");
                        }
                        data[0][f] = pose.bodyPosition.x;
                        data[1][f] = pose.bodyPosition.y;
                        data[2][f] = pose.bodyPosition.z;
                        Quaternion q = pose.bodyRotation;
                        if (f > 0 && Quaternion.Dot(prevQ, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        prevQ = q;
                        data[3][f] = q.x; data[4][f] = q.y; data[5][f] = q.z; data[6][f] = q.w;
                        for (int i = 0; i < muscleCount; i++) data[7 + i][f] = pose.muscles[i];
                    }

                    // ---- tulis ke AnimationClip ----
                    var clip = new AnimationClip { frameRate = 30f };
                    float dt = 1f / 30f;
                    string[] rootNames = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
                    for (int i = 0; i < channels; i++)
                    {
                        string prop = i < 7 ? rootNames[i] : ClipPropName(HumanTrait.MuscleName[i - 7]);
                        AnimationUtility.SetEditorCurve(clip,
                            EditorCurveBinding.FloatCurve("", typeof(Animator), prop),
                            MakeCurve(data[i], dt));
                    }
                    return clip;
                }
                finally
                {
                    if (handler != null) handler.Dispose();
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }
    }
}
#endif
