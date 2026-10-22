using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Networking;

namespace UnknownsSandbox
{
  // Textures from resource packs the player puts in <game>\Unknowns Sandbox\minecraft\packs\
  // (the template pack there is the default), and sounds from the player's OWN Minecraft
  // install (vanilla launcher, Prism, MultiMC, CurseForge...). Nothing of Mojang's ships
  // with the SandBox: without a pack / install the built-in art and synthesized sounds are
  // used. Loose files can override textures too:
  //   <game>\Unknowns Sandbox\minecraft\  (textures\block\*.png, textures\item\*.png)
  internal static class SbMinecraft
  {
    internal static bool Enabled = PlayerPrefs.GetInt("UnknownsSandBox_RealMinecraft", 1) == 1;
    internal static string TextureSource = "built-in";
    internal static string SoundSource = "built-in";

    private static bool texturesLoaded;
    private static readonly Dictionary<string, Color32[]> images = new Dictionary<string, Color32[]>(StringComparer.Ordinal);
    private static readonly Dictionary<string, Vector2Int> sizes = new Dictionary<string, Vector2Int>(StringComparer.Ordinal);

    // ---------------------------------------------------------------- finding Minecraft
    private static IEnumerable<string> CandidateRoots()
    {
      string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
      string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
      string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
      yield return Path.Combine(appData, ".minecraft");
      yield return Path.Combine(appData, "PrismLauncher");
      yield return Path.Combine(appData, "MultiMC");
      yield return Path.Combine(appData, "PolyMC");
      yield return Path.Combine(appData, "ATLauncher");
      yield return Path.Combine(home, "curseforge", "minecraft", "Install");
      yield return Path.Combine(local, "Packages", "Microsoft.4297127D64EC6_8wekyb3d8bbwe", "LocalCache", "Roaming", ".minecraft");
      // more launchers (Modrinth, TLauncher, GDLauncher, Technic, Feather, Lunar's vanilla copy)
      yield return Path.Combine(appData, "ModrinthApp", "meta");
      yield return Path.Combine(appData, "com.modrinth.theseus", "meta");
      yield return Path.Combine(appData, ".tlauncher", "legacy", "Minecraft", "game");
      yield return Path.Combine(appData, "gdlauncher_carbon", "data");
      yield return Path.Combine(appData, "gdlauncher_next");
      yield return Path.Combine(appData, ".technic");
      yield return Path.Combine(appData, ".feather");
      yield return Path.Combine(home, ".lunarclient", "offline", "multiver");
    }

    private static string OverrideFolder()
    {
      try { return Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Unknowns Sandbox", "minecraft"); }
      catch { return null; }
    }

    // ---------------------------------------------------------------- textures
    // which textures the SandBox uses (tablet settings, TEXTURE PACK):
    //   ""          your Minecraft's own default textures (the default)
    //   "<name>.zip" a resource pack from <game>\Unknowns Sandbox\minecraft\packs\
    //               (what it doesn't have still comes from your Minecraft)
    //   BuiltIn     the SandBox's own drawn textures
    internal const string BuiltIn = "*built-in*";
    internal static string Pack = PlayerPrefs.GetString("UnknownsSandBox_TexturePack", "");
    private static IEnumerable<string> lastWanted;

    internal static string PacksFolder()
    {
      string over = OverrideFolder();
      return over != null ? Path.Combine(over, "packs") : null;
    }

    // the packs you can pick (file names)
    internal static List<string> AvailablePacks()
    {
      var list = new List<string>();
      try
      {
        string dir = PacksFolder();
        if (dir == null) return list;
        Directory.CreateDirectory(dir);
        foreach (string f in Directory.GetFiles(dir, "*.zip")) list.Add(Path.GetFileName(f));
        list.Sort(StringComparer.OrdinalIgnoreCase);
      }
      catch { }
      return list;
    }

    // no pack yet: download the template pack from Modrinth into the packs folder
    // (once, on this PC), then switch the textures over to it. Nothing of Mojang's ships with the SandBox.
    private const string DefaultPackUrl = "https://cdn.modrinth.com/data/rkT8RY3X/versions/9H0H0MUP/1.21.4-Template.zip";
    private const string DefaultPackFile = "1.21.4-Template.zip";
    private static bool packDownloadStarted;
    internal static string PackDownloadState = "";

    internal static void EnsureDefaultPack(MonoBehaviour host)
    {
      if (packDownloadStarted || host == null || Pack == BuiltIn) return;
      packDownloadStarted = true;
      if (AvailablePacks().Count > 0) return;
      host.StartCoroutine(DownloadDefaultPack());
    }

    private static IEnumerator DownloadDefaultPack()
    {
      string dir = PacksFolder();
      if (dir == null) yield break;
      try { Directory.CreateDirectory(dir); } catch { yield break; }
      string target = Path.Combine(dir, DefaultPackFile), part = target + ".part";
      PackDownloadState = "downloading";
      SandBoxWorld.Toast?.Invoke("SandBox", "Downloading the Minecraft texture pack (5 MB)...");
      var req = new UnityWebRequest(DefaultPackUrl, UnityWebRequest.kHttpVerbGET);
      req.downloadHandler = new DownloadHandlerFile(part) { removeFileOnAbort = true };
      req.timeout = 120;
      yield return req.SendWebRequest();
      bool ok = req.result == UnityWebRequest.Result.Success;
      string err = req.error;
      req.Dispose();
      try
      {
        // a real zip of a sensible size, or it is thrown away
        var fi = new FileInfo(part);
        if (ok && fi.Exists && fi.Length > 1_000_000)
        {
          using (var fs = File.OpenRead(part))
            ok = fs.ReadByte() == 0x50 && fs.ReadByte() == 0x4b;
        }
        else ok = false;
        if (ok)
        {
          if (File.Exists(target)) File.Delete(target);
          File.Move(part, target);
        }
        else if (File.Exists(part)) File.Delete(part);
      }
      catch (Exception ex) { ok = false; err = ex.Message; }
      if (!ok)
      {
        PackDownloadState = "failed";
        SbLog.Warn("texture pack download failed: " + err);
        SandBoxWorld.Toast?.Invoke("SandBox", "Couldn't download the texture pack: using the built-in textures");
        yield break;
      }
      PackDownloadState = "done";
      UsePack(Pack);
      SandBoxWorld.Toast?.Invoke("SandBox", "Minecraft texture pack ready");
    }

    // the default pack: the template pack in the packs folder (a name with "template"), else the first one
    internal static string DefaultPack()
    {
      List<string> all = AvailablePacks();
      foreach (string p in all) if (p.IndexOf("template", StringComparison.OrdinalIgnoreCase) >= 0) return p;
      return all.Count > 0 ? all[0] : null;
    }

    internal static string PackLabel(string pack)
    {
      if (string.IsNullOrEmpty(pack))
      {
        string d = DefaultPack();
        return d != null ? Path.GetFileNameWithoutExtension(d).ToUpperInvariant() + " (DEFAULT)" : "NO PACK FOUND (BUILT-IN)";
      }
      if (pack == BuiltIn) return "BUILT-IN";
      return Path.GetFileNameWithoutExtension(pack).ToUpperInvariant();
    }

    // switch packs now: blocks, mob skins, menu icons and the portal swirl all change
    internal static void UsePack(string pack)
    {
      Pack = pack ?? "";
      PlayerPrefs.SetString("UnknownsSandBox_TexturePack", Pack);
      PlayerPrefs.Save();
      ResetTextures();
      SbBlockArt.Reload();
      SbMobDefs.RefreshTextures();
      SbCatalog.RebuildPixelItems();
      SbCatalog.ResetIcons();
      SbPortalOverlay.Reset();
      SbEndPortalArt.Reset();
      SbMcFx.Reset();
    }

    // forget the loaded textures (switching packs): the next LoadTextures reads them again
    internal static void ResetTextures()
    {
      texturesLoaded = false;
      images.Clear();
      sizes.Clear();
      foreach (Texture2D t in imageTextures.Values) if (t != null) UnityEngine.Object.Destroy(t);
      imageTextures.Clear();
      TextureSource = "built-in";
    }

    internal static void LoadTextures(IEnumerable<string> wanted)
    {
      if (wanted != null) lastWanted = new List<string>(wanted);
      if (texturesLoaded) return;
      texturesLoaded = true;
      if (!Enabled || Pack == BuiltIn || lastWanted == null) return;
      var want = new HashSet<string>(lastWanted, StringComparer.Ordinal);
      try
      {
        string over = OverrideFolder();
        if (over != null && Directory.Exists(over))
        {
          int n = 0;
          foreach (string name in want)
          {
            string p = Path.Combine(over, name.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(p) && Decode(name, File.ReadAllBytes(p))) n++;
          }
          if (n > 0) TextureSource = "folder (" + n + ")";
        }
        // textures come only from packs in the packs folder (not from a Minecraft
        // install). The default is the template pack there; the one you pick goes first,
        // and anything it lacks comes from the other packs.
        string chosen = string.IsNullOrEmpty(Pack) ? DefaultPack() : Pack;
        var order = new List<string>();
        if (!string.IsNullOrEmpty(chosen)) order.Add(chosen);
        foreach (string p in AvailablePacks()) if (!order.Contains(p)) order.Add(p);
        foreach (string name in order)
        {
          string zip = Path.Combine(PacksFolder(), name);
          if (!File.Exists(zip)) { SbLog.Warn("texture pack not found: " + name); continue; }
          var missing = new List<string>();
          foreach (string w in want) if (!images.ContainsKey(w)) missing.Add("assets/minecraft/" + w);
          if (missing.Count == 0) break;
          int fromPack = 0;
          try
          {
            foreach (var kv in ReadZipEntries(zip, missing))
              if (Decode(kv.Key.Substring("assets/minecraft/".Length), kv.Value)) fromPack++;
          }
          catch (Exception ex) { SbLog.Warn("pack " + name + ": " + ex.Message); }
          if (fromPack > 0 && (TextureSource == "built-in" || TextureSource.StartsWith("folder", StringComparison.Ordinal)))
            TextureSource = "pack " + Path.GetFileNameWithoutExtension(name) + " (" + fromPack + ")";
        }
      }
      catch (Exception ex) { SbLog.Warn("Minecraft textures: " + ex.Message); }
    }

    private static bool Decode(string name, byte[] png)
    {
      try
      {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!LoadImage(tex, png)) { UnityEngine.Object.Destroy(tex); return false; }
        images[name] = tex.GetPixels32();
        sizes[name] = new Vector2Int(tex.width, tex.height);
        UnityEngine.Object.Destroy(tex);
        return true;
      }
      catch { return false; }
    }

    // LoadImage through reflection: the direct call pulls ReadOnlySpan into the net48 build
    private static System.Reflection.MethodInfo loadImage;
    private static bool LoadImage(Texture2D tex, byte[] data)
    {
      if (loadImage == null)
      {
        Type conv = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
        if (conv == null) return false;
        loadImage = conv.GetMethod("LoadImage", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
          null, new Type[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }, null);
      }
      if (loadImage == null) return false;
      object r = loadImage.Invoke(null, new object[] { tex, data, false });
      return !(r is bool) || (bool)r;
    }

    // a whole texture (mob skins), point sampled; null when the player's Minecraft doesn't have it
    private static readonly Dictionary<string, Texture2D> imageTextures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
    internal static Texture2D Image(string name)
    {
      Texture2D t;
      if (imageTextures.TryGetValue(name, out t)) return t;
      Color32[] px; Vector2Int sz;
      if (!images.TryGetValue(name, out px) || !sizes.TryGetValue(name, out sz)) { imageTextures[name] = null; return null; }
      t = new Texture2D(sz.x, sz.y, TextureFormat.RGBA32, false) { name = "MC " + name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
      t.SetPixels32(px);
      t.Apply(false, false);
      imageTextures[name] = t;
      return t;
    }

    // The first 16 x 16 frame (animated textures are vertical strips), rows from
    // the TOP of the picture. Bigger resource-pack textures are point-sampled.
    internal static Color32[] Frame16(string name)
    {
      Color32[] px; Vector2Int sz;
      if (!images.TryGetValue(name, out px) || !sizes.TryGetValue(name, out sz)) return null;
      int fw = sz.x, fh = Mathf.Min(sz.x, sz.y);
      var o = new Color32[256];
      for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
          int sx = x * fw / 16, syTop = y * fh / 16;
          int row = sz.y - 1 - syTop;                // texture rows go up
          o[y * 16 + x] = px[row * sz.x + sx];
        }
      return o;
    }

    // ---------------------------------------------------------------- zip (jar) reading
    private static Dictionary<string, byte[]> ReadZipEntries(string path, List<string> names)
    {
      var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
      var want = new HashSet<string>(names, StringComparer.Ordinal);
      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
      using (var br = new BinaryReader(fs))
      {
        long len = fs.Length;
        int tail = (int)Math.Min(len, 66000);
        fs.Seek(len - tail, SeekOrigin.Begin);
        byte[] end = br.ReadBytes(tail);
        int eocd = -1;
        for (int i = end.Length - 22; i >= 0; i--)
          if (end[i] == 0x50 && end[i + 1] == 0x4b && end[i + 2] == 0x05 && end[i + 3] == 0x06) { eocd = i; break; }
        if (eocd < 0) return result;
        long cdOffset = BitConverter.ToUInt32(end, eocd + 16);
        if (cdOffset == 0xFFFFFFFF && eocd >= 20)
        {
          int loc = eocd - 20;
          if (BitConverter.ToUInt32(end, loc) == 0x07064b50)
          {
            long z64 = (long)BitConverter.ToUInt64(end, loc + 8);
            fs.Seek(z64 + 48, SeekOrigin.Begin);
            cdOffset = (long)br.ReadUInt64();
          }
        }
        fs.Seek(cdOffset, SeekOrigin.Begin);
        var entries = new List<KeyValuePair<string, long[]>>();
        while (fs.Position + 46 <= len)
        {
          if (br.ReadUInt32() != 0x02014b50) break;
          fs.Seek(6, SeekOrigin.Current);
          int method = br.ReadUInt16();
          fs.Seek(8, SeekOrigin.Current);
          long comp = br.ReadUInt32();
          long uncomp = br.ReadUInt32();
          int nameLen = br.ReadUInt16(), extraLen = br.ReadUInt16(), commentLen = br.ReadUInt16();
          fs.Seek(8, SeekOrigin.Current);
          long local = br.ReadUInt32();
          string name = System.Text.Encoding.UTF8.GetString(br.ReadBytes(nameLen));
          fs.Seek(extraLen + commentLen, SeekOrigin.Current);
          if (want.Contains(name)) entries.Add(new KeyValuePair<string, long[]>(name, new long[] { local, method, comp, uncomp }));
        }
        foreach (var e in entries)
        {
          try
          {
            fs.Seek(e.Value[0], SeekOrigin.Begin);
            if (br.ReadUInt32() != 0x04034b50) continue;
            fs.Seek(22, SeekOrigin.Current);
            int n = br.ReadUInt16(), x = br.ReadUInt16();
            fs.Seek(n + x, SeekOrigin.Current);
            byte[] data = br.ReadBytes((int)e.Value[2]);
            if (e.Value[1] == 8)
            {
              using (var ms = new MemoryStream(data))
              using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
              using (var outMs = new MemoryStream((int)Math.Max(16, e.Value[3])))
              {
                ds.CopyTo(outMs);
                data = outMs.ToArray();
              }
            }
            else if (e.Value[1] != 0) continue;
            result[e.Key] = data;
          }
          catch { }
        }
      }
      return result;
    }

    // ---------------------------------------------------------------- sounds
    private static readonly Dictionary<string, List<AudioClip>> sounds = new Dictionary<string, List<AudioClip>>(StringComparer.Ordinal);
    private static bool soundsStarted;
    private static readonly System.Random rng = new System.Random();

    // group -> Minecraft sound files (without .ogg)
    private static readonly Dictionary<string, string[]> Groups = new Dictionary<string, string[]>
    {
      { "stone", new[] { "dig/stone1", "dig/stone2", "dig/stone3", "dig/stone4" } },
      { "wood", new[] { "dig/wood1", "dig/wood2", "dig/wood3", "dig/wood4" } },
      { "gravel", new[] { "dig/gravel1", "dig/gravel2", "dig/gravel3", "dig/gravel4" } },
      { "sand", new[] { "dig/sand1", "dig/sand2", "dig/sand3", "dig/sand4" } },
      { "grass", new[] { "dig/grass1", "dig/grass2", "dig/grass3", "dig/grass4" } },
      { "cloth", new[] { "dig/cloth1", "dig/cloth2", "dig/cloth3", "dig/cloth4" } },
      { "snow", new[] { "dig/snow1", "dig/snow2", "dig/snow3", "dig/snow4" } },
      { "glass", new[] { "random/glass1", "random/glass2", "random/glass3" } },
      { "slime", new[] { "mob/slime/big1", "mob/slime/big2", "mob/slime/big3", "mob/slime/big4" } },
      { "explode", new[] { "random/explode1", "random/explode2", "random/explode3", "random/explode4" } },
      { "fuse", new[] { "random/fuse" } },
      { "fizz", new[] { "random/fizz" } },
      { "bucket_water", new[] { "item/bucket/empty1", "item/bucket/empty2", "item/bucket/empty3" } },
      { "bucket_lava", new[] { "item/bucket/empty_lava1", "item/bucket/empty_lava2", "item/bucket/empty_lava3" } },
      { "splash", new[] { "liquid/splash", "liquid/splash2" } },
      { "swim", new[] { "liquid/swim1", "liquid/swim2", "liquid/swim3", "liquid/swim4" } },
      { "lava", new[] { "liquid/lavapop" } },
      { "sweep", new[] { "entity/player/attack/sweep1", "entity/player/attack/sweep2", "entity/player/attack/sweep3", "entity/player/attack/sweep4" } },
      { "strong", new[] { "entity/player/attack/strong1", "entity/player/attack/strong2", "entity/player/attack/strong3", "entity/player/attack/strong4" } },
      { "crit", new[] { "entity/player/attack/crit1", "entity/player/attack/crit2", "entity/player/attack/crit3" } },
      { "ignite", new[] { "fire/ignite" } },
      { "fire", new[] { "fire/fire" } },
      { "portal", new[] { "portal/portal" } },
      { "portal_trigger", new[] { "portal/trigger" } },
      { "portal_travel", new[] { "portal/travel" } },
      // footsteps: what a hand tap on a block sounds like (SbTapSounds)
      { "step_stone", new[] { "step/stone1", "step/stone2", "step/stone3", "step/stone4", "step/stone5", "step/stone6" } },
      { "step_wood", new[] { "step/wood1", "step/wood2", "step/wood3", "step/wood4", "step/wood5", "step/wood6" } },
      { "step_gravel", new[] { "step/gravel1", "step/gravel2", "step/gravel3", "step/gravel4" } },
      { "step_sand", new[] { "step/sand1", "step/sand2", "step/sand3", "step/sand4", "step/sand5" } },
      { "step_grass", new[] { "step/grass1", "step/grass2", "step/grass3", "step/grass4", "step/grass5", "step/grass6" } },
      { "step_cloth", new[] { "step/cloth1", "step/cloth2", "step/cloth3", "step/cloth4" } },
      { "step_snow", new[] { "step/snow1", "step/snow2", "step/snow3", "step/snow4" } },
      { "step_glass", new[] { "step/stone1", "step/stone2", "step/stone3" } },
      { "step_slime", new[] { "mob/slime/small1", "mob/slime/small2", "mob/slime/small3", "mob/slime/small4", "mob/slime/small5" } },
      // mobs (SandBoxMobs.cs)
      { "sheep_say", new[] { "mob/sheep/say1", "mob/sheep/say2", "mob/sheep/say3" } },
      { "pig_say", new[] { "mob/pig/say1", "mob/pig/say2", "mob/pig/say3" } },
      { "pig_death", new[] { "mob/pig/death" } },
      { "cow_say", new[] { "mob/cow/say1", "mob/cow/say2", "mob/cow/say3", "mob/cow/say4" } },
      { "cow_hurt", new[] { "mob/cow/hurt1", "mob/cow/hurt2", "mob/cow/hurt3" } },
      { "chicken_say", new[] { "mob/chicken/say1", "mob/chicken/say2", "mob/chicken/say3" } },
      { "chicken_hurt", new[] { "mob/chicken/hurt1", "mob/chicken/hurt2" } },
      { "zombie_say", new[] { "mob/zombie/say1", "mob/zombie/say2", "mob/zombie/say3" } },
      { "zombie_hurt", new[] { "mob/zombie/hurt1", "mob/zombie/hurt2" } },
      { "zombie_death", new[] { "mob/zombie/death" } },
      { "skeleton_say", new[] { "mob/skeleton/say1", "mob/skeleton/say2", "mob/skeleton/say3" } },
      { "skeleton_hurt", new[] { "mob/skeleton/hurt1", "mob/skeleton/hurt2", "mob/skeleton/hurt3", "mob/skeleton/hurt4" } },
      { "skeleton_death", new[] { "mob/skeleton/death" } },
      { "villager_say", new[] { "mob/villager/idle1", "mob/villager/idle2", "mob/villager/idle3" } },
      { "villager_hurt", new[] { "mob/villager/hit1", "mob/villager/hit2", "mob/villager/hit3", "mob/villager/hit4" } },
      { "villager_death", new[] { "mob/villager/death" } },
      { "golem_step", new[] { "mob/irongolem/walk1", "mob/irongolem/walk2", "mob/irongolem/walk3", "mob/irongolem/walk4" } },
      { "golem_hurt", new[] { "mob/irongolem/hit1", "mob/irongolem/hit2", "mob/irongolem/hit3", "mob/irongolem/hit4" } },
      { "golem_death", new[] { "mob/irongolem/death" } },
      { "piglin_say", new[] { "mob/piglin/idle1", "mob/piglin/idle2", "mob/piglin/idle3", "mob/piglin/idle4", "mob/piglin/idle5" } },
      { "piglin_hurt", new[] { "mob/piglin/hurt1", "mob/piglin/hurt2", "mob/piglin/hurt3" } },
      { "piglin_death", new[] { "mob/piglin/death1", "mob/piglin/death2" } },
      { "enderman_say", new[] { "mob/endermen/idle1", "mob/endermen/idle2", "mob/endermen/idle3", "mob/endermen/idle4", "mob/endermen/idle5" } },
      { "enderman_hurt", new[] { "mob/endermen/hit1", "mob/endermen/hit2", "mob/endermen/hit3", "mob/endermen/hit4" } },
      { "enderman_death", new[] { "mob/endermen/death" } },
      { "enderman_tp", new[] { "mob/endermen/portal", "mob/endermen/portal2" } },
      // the new mobs
      { "creeper_say", new[] { "mob/creeper/say1", "mob/creeper/say2", "mob/creeper/say3", "mob/creeper/say4" } },
      { "creeper_death", new[] { "mob/creeper/death" } },
      { "creeper_fuse", new[] { "random/fuse" } },
      { "spider_say", new[] { "mob/spider/say1", "mob/spider/say2", "mob/spider/say3", "mob/spider/say4" } },
      { "spider_death", new[] { "mob/spider/death" } },
      { "wolf_say", new[] { "mob/wolf/bark1", "mob/wolf/bark2", "mob/wolf/bark3", "mob/wolf/panting" } },
      { "wolf_hurt", new[] { "mob/wolf/hurt1", "mob/wolf/hurt2", "mob/wolf/hurt3" } },
      { "wolf_death", new[] { "mob/wolf/death" } },
      { "cat_say", new[] { "mob/cat/meow1", "mob/cat/meow2", "mob/cat/meow3", "mob/cat/meow4" } },
      { "cat_hurt", new[] { "mob/cat/hitt1", "mob/cat/hitt2", "mob/cat/hitt3" } },
      { "husk_say", new[] { "mob/husk/idle1", "mob/husk/idle2", "mob/husk/idle3" } },
      { "husk_hurt", new[] { "mob/husk/hurt1", "mob/husk/hurt2" } },
      { "husk_death", new[] { "mob/husk/death1", "mob/husk/death2" } },
      { "drowned_say", new[] { "mob/drowned/idle1", "mob/drowned/idle2", "mob/drowned/idle3", "mob/drowned/idle4", "mob/drowned/idle5" } },
      { "drowned_hurt", new[] { "mob/drowned/hurt1", "mob/drowned/hurt2", "mob/drowned/hurt3" } },
      { "drowned_death", new[] { "mob/drowned/death1", "mob/drowned/death2" } },
      { "zvillager_say", new[] { "mob/zombie_villager/say1", "mob/zombie_villager/say2", "mob/zombie_villager/say3" } },
      { "zvillager_hurt", new[] { "mob/zombie_villager/hurt1", "mob/zombie_villager/hurt2" } },
      { "zvillager_death", new[] { "mob/zombie_villager/death" } },
      { "stray_say", new[] { "mob/stray/idle1", "mob/stray/idle2", "mob/stray/idle3", "mob/stray/idle4" } },
      { "stray_hurt", new[] { "mob/stray/hurt1", "mob/stray/hurt2", "mob/stray/hurt3", "mob/stray/hurt4" } },
      { "stray_death", new[] { "mob/stray/death1", "mob/stray/death2" } },
      { "wskeleton_say", new[] { "mob/wither_skeleton/idle1", "mob/wither_skeleton/idle2", "mob/wither_skeleton/idle3" } },
      { "wskeleton_hurt", new[] { "mob/wither_skeleton/hurt1", "mob/wither_skeleton/hurt2", "mob/wither_skeleton/hurt3", "mob/wither_skeleton/hurt4" } },
      { "wskeleton_death", new[] { "mob/wither_skeleton/death1", "mob/wither_skeleton/death2" } },
      { "zpig_say", new[] { "mob/zpig/zpig1", "mob/zpig/zpig2", "mob/zpig/zpig3", "mob/zpig/zpig4" } },
      { "zpig_hurt", new[] { "mob/zpig/zpighurt1", "mob/zpig/zpighurt2" } },
      { "zpig_death", new[] { "mob/zpig/zpigdeath" } },
      { "brute_say", new[] { "mob/piglin_brute/idle1", "mob/piglin_brute/idle2", "mob/piglin_brute/idle3", "mob/piglin_brute/idle4" } },
      { "brute_hurt", new[] { "mob/piglin_brute/hurt1", "mob/piglin_brute/hurt2", "mob/piglin_brute/hurt3", "mob/piglin_brute/hurt4" } },
      { "brute_death", new[] { "mob/piglin_brute/death1", "mob/piglin_brute/death2", "mob/piglin_brute/death3" } },
      { "blaze_say", new[] { "mob/blaze/breathe1", "mob/blaze/breathe2", "mob/blaze/breathe3", "mob/blaze/breathe4" } },
      { "blaze_hurt", new[] { "mob/blaze/hit1", "mob/blaze/hit2", "mob/blaze/hit3", "mob/blaze/hit4" } },
      { "blaze_death", new[] { "mob/blaze/death" } },
      { "magma_say", new[] { "mob/magmacube/big1", "mob/magmacube/big2", "mob/magmacube/big3", "mob/magmacube/big4" } },
      { "magma_jump", new[] { "mob/magmacube/jump1", "mob/magmacube/jump2", "mob/magmacube/jump3", "mob/magmacube/jump4" } },
      { "silverfish_say", new[] { "mob/silverfish/say1", "mob/silverfish/say2", "mob/silverfish/say3", "mob/silverfish/say4" } },
      { "silverfish_hurt", new[] { "mob/silverfish/hit1", "mob/silverfish/hit2", "mob/silverfish/hit3" } },
      { "silverfish_death", new[] { "mob/silverfish/kill" } },

      { "rabbit_say", new[] { "mob/rabbit/idle1", "mob/rabbit/idle2", "mob/rabbit/idle3", "mob/rabbit/idle4" } },
      { "rabbit_hurt", new[] { "mob/rabbit/hurt1", "mob/rabbit/hurt2", "mob/rabbit/hurt3", "mob/rabbit/hurt4" } },
      { "rabbit_death", new[] { "mob/rabbit/bunnymurder" } },
      { "rabbit_hop", new[] { "mob/rabbit/hop1", "mob/rabbit/hop2", "mob/rabbit/hop3", "mob/rabbit/hop4" } },
      { "fox_say", new[] { "mob/fox/idle1", "mob/fox/idle2", "mob/fox/idle3", "mob/fox/idle4", "mob/fox/idle5", "mob/fox/idle6" } },
      { "fox_hurt", new[] { "mob/fox/hurt1", "mob/fox/hurt2", "mob/fox/hurt3", "mob/fox/hurt4" } },
      { "fox_death", new[] { "mob/fox/death1", "mob/fox/death2" } },
      { "llama_say", new[] { "mob/llama/idle1", "mob/llama/idle2", "mob/llama/idle3", "mob/llama/idle4", "mob/llama/idle5" } },
      { "llama_hurt", new[] { "mob/llama/hurt1", "mob/llama/hurt2", "mob/llama/hurt3" } },
      { "llama_death", new[] { "mob/llama/death1", "mob/llama/death2" } },
      { "witch_say", new[] { "mob/witch/ambient1", "mob/witch/ambient2", "mob/witch/ambient3", "mob/witch/ambient4", "mob/witch/ambient5" } },
      { "witch_hurt", new[] { "mob/witch/hurt1", "mob/witch/hurt2", "mob/witch/hurt3" } },
      { "witch_death", new[] { "mob/witch/death1", "mob/witch/death2", "mob/witch/death3" } },
      { "pillager_say", new[] { "mob/pillager/idle1", "mob/pillager/idle2", "mob/pillager/idle3", "mob/pillager/idle4" } },
      { "pillager_hurt", new[] { "mob/pillager/hurt1", "mob/pillager/hurt2", "mob/pillager/hurt3" } },
      { "pillager_death", new[] { "mob/pillager/death1", "mob/pillager/death2" } },
      { "vindicator_say", new[] { "mob/vindication_illager/idle1", "mob/vindication_illager/idle2", "mob/vindication_illager/idle3", "mob/vindication_illager/idle4", "mob/vindication_illager/idle5" } },
      { "vindicator_hurt", new[] { "mob/vindication_illager/hurt1", "mob/vindication_illager/hurt2", "mob/vindication_illager/hurt3" } },
      { "vindicator_death", new[] { "mob/vindication_illager/death1", "mob/vindication_illager/death2" } },
      { "evoker_say", new[] { "mob/evocation_illager/idle1", "mob/evocation_illager/idle2", "mob/evocation_illager/idle3", "mob/evocation_illager/idle4" } },
      { "evoker_hurt", new[] { "mob/evocation_illager/hurt1", "mob/evocation_illager/hurt2" } },
      { "evoker_death", new[] { "mob/evocation_illager/death1", "mob/evocation_illager/death2" } },
      { "hoglin_say", new[] { "mob/hoglin/idle1", "mob/hoglin/idle2", "mob/hoglin/idle3", "mob/hoglin/idle4", "mob/hoglin/idle5" } },
      { "hoglin_hurt", new[] { "mob/hoglin/hurt1", "mob/hoglin/hurt2", "mob/hoglin/hurt3", "mob/hoglin/hurt4" } },
      { "hoglin_death", new[] { "mob/hoglin/death1", "mob/hoglin/death2", "mob/hoglin/death3" } },
      { "zoglin_say", new[] { "mob/zoglin/idle1", "mob/zoglin/idle2", "mob/zoglin/idle3", "mob/zoglin/idle4", "mob/zoglin/idle5" } },
      { "zoglin_hurt", new[] { "mob/zoglin/hurt1", "mob/zoglin/hurt2", "mob/zoglin/hurt3" } },
      { "zoglin_death", new[] { "mob/zoglin/death1", "mob/zoglin/death2", "mob/zoglin/death3" } },
      { "shulker_say", new[] { "mob/shulker/ambient1", "mob/shulker/ambient2", "mob/shulker/ambient3", "mob/shulker/ambient4", "mob/shulker/ambient5" } },
      { "shulker_hurt", new[] { "mob/shulker/hurt1", "mob/shulker/hurt2", "mob/shulker/hurt3", "mob/shulker/hurt4" } },
      { "shulker_death", new[] { "mob/shulker/death1", "mob/shulker/death2", "mob/shulker/death3", "mob/shulker/death4" } },
      // the End
      { "eye_place", new[] { "block/end_portal/eyeplace1", "block/end_portal/eyeplace2", "block/end_portal/eyeplace3" } },
      { "end_portal_open", new[] { "block/end_portal/endportal" } },
      { "end_amb", new[] { "music/game/end/end" } },
      { "nether_amb", new[] { "ambient/nether/nether_wastes/ambience" } },
      { "nether_mood", new[] { "ambient/nether/nether_wastes/mood1", "ambient/nether/nether_wastes/mood2", "ambient/nether/nether_wastes/mood3", "ambient/nether/nether_wastes/mood4" } },
    };

    internal static bool HasSound(string group)
    {
      List<AudioClip> l;
      return Enabled && sounds.TryGetValue(group, out l) && l.Count > 0;
    }

    internal static AudioClip Sound(string group)
    {
      List<AudioClip> l;
      if (!Enabled || !sounds.TryGetValue(group, out l) || l.Count == 0) return null;
      return l[rng.Next(l.Count)];
    }

    internal static void StartSounds(MonoBehaviour host)
    {
      if (soundsStarted || !Enabled || host == null) return;
      soundsStarted = true;
      host.StartCoroutine(LoadSounds());
    }

    private static IEnumerator LoadSounds()
    {
      // asset index (json) -> hashed object files
      string objects = null;
      Dictionary<string, string> hashes = null;
      try { hashes = FindSoundIndex(out objects); } catch (Exception ex) { SbLog.Warn("Minecraft sounds: " + ex.Message); }
      if (hashes == null) yield break;
      int loaded = 0;
      foreach (var g in Groups)
      {
        var list = new List<AudioClip>();
        foreach (string s in g.Value)
        {
          string hash;
          if (!hashes.TryGetValue("minecraft/sounds/" + s + ".ogg", out hash) || hash.Length < 2) continue;
          string file = Path.Combine(objects, hash.Substring(0, 2), hash);
          if (!File.Exists(file)) continue;
          UnityWebRequest req = null;
          try { req = UnityWebRequestMultimedia.GetAudioClip(new Uri(file).AbsoluteUri, AudioType.OGGVORBIS); }
          catch { }
          if (req == null) continue;
          yield return req.SendWebRequest();
          try
          {
            if (req.result == UnityWebRequest.Result.Success)
            {
              AudioClip c = DownloadHandlerAudioClip.GetContent(req);
              if (c != null) { c.name = "MC " + s; list.Add(c); loaded++; }
            }
          }
          catch { }
          req.Dispose();
        }
        if (list.Count > 0) sounds[g.Key] = list;
      }
      if (loaded > 0) SoundSource = "Minecraft (" + loaded + " sounds)";
    }

    private static Dictionary<string, string> FindSoundIndex(out string objectsDir)
    {
      objectsDir = null;
      FileInfo best = null;
      foreach (string root in CandidateRoots())
      {
        try
        {
          string idx = Path.Combine(root, "assets", "indexes");
          if (!Directory.Exists(idx)) continue;
          foreach (string f in Directory.GetFiles(idx, "*.json"))
          {
            var fi = new FileInfo(f);
            if (fi.Length < 100_000) continue;
            if (best == null || fi.LastWriteTimeUtc > best.LastWriteTimeUtc) best = fi;
          }
        }
        catch { }
      }
      if (best == null) return null;
      objectsDir = Path.Combine(best.Directory.Parent.FullName, "objects");
      if (!Directory.Exists(objectsDir)) return null;
      var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(best.FullName));
      var objs = json["objects"] as Newtonsoft.Json.Linq.JObject;
      if (objs == null) return null;
      var map = new Dictionary<string, string>(StringComparer.Ordinal);
      foreach (var p in objs.Properties())
        if (p.Name.StartsWith("minecraft/sounds/", StringComparison.Ordinal))
          map[p.Name] = (string)p.Value["hash"];
      return map;
    }
  }
}
