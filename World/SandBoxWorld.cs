using System;
using System.Collections.Generic;
using GorillaLocomotion;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace UnknownsSandbox
{
  internal sealed class SbObject
  {
    internal int Owner;            // 0 = mine, -1 = made locally by flowing liquids, else Photon actor
    internal int Id;
    internal SbItem Item;
    internal GameObject Go;
    internal Transform T;
    internal GameObject ColGo;
    internal readonly List<Collider> Cols = new List<Collider>();
    internal Rigidbody Rb;
    internal float Scale = 1f;
    internal bool Anchored = true;
    internal bool IsCell;          // block or liquid source on the grid
    internal int SizeIdx;
    internal Vector3Int Cell;
    internal bool Primed;
    // redstone and shapes (SandBoxRedstone.cs)
    internal bool RedOn;         // a lever switched on, a door opened by hand
    internal float OnUntil;      // a button pressed until then
    internal bool Powered;       // powered by redstone last time it was worked out
    internal int Note;           // a note block's note (0 - 24)
    internal bool ShapeState;    // the look shown: open / on
    internal float FuseLeft, FuseTotal;
    internal bool Held, HeldRight, HeldRemote;
    internal Vector3 HoldLocalPos;
    internal Quaternion HoldLocalRot = Quaternion.identity;
    internal Vector3 NetPos, NetVel;
    internal Quaternion NetRot = Quaternion.identity;
    internal float NetTime;
    internal bool HasNet;
    internal Vector3 SentPos;
    internal Quaternion SentRot = Quaternion.identity;
    internal int RestSends;
    internal bool Stuck;
    internal bool Falling;
    internal float FallVel;
    internal Vector3Int FallTarget;
    internal float SpawnTime;
    internal Light Light;
    internal ParticleSystem Fire;
    internal TMPro.TextMeshPro Countdown;
    internal float LastBeep;
    internal float LaunchT = -1f;
    internal float PearlThrownAt = -1f;   // an ender pearl in flight (SandBoxPearl.cs)
    internal Bounds LocalBounds;
    // attachments (SandBoxAttach.cs): what this is stuck to, and what is stuck to it
    internal SbMob Mob;            // a Minecraft mob's brain and body (SandBoxMobs.cs)
    internal SbObject Parent;
    internal bool Welded;          // stuck on with the weld tool (not a machine)
    internal readonly List<SbObject> Children = new List<SbObject>();
    internal bool On = true;
    internal Quaternion AttachRot = Quaternion.identity;   // local rotation on the parent (wheels spin around it)
    internal float SpinAngle, ToggleAt;
    internal Vector3 LastPos;
    internal ParticleSystem Flame;
    internal AudioSource Loop;
    internal long Key { get { return ((long)Owner << 32) | (uint)Id; } }
    internal bool Mine { get { return Owner == 0; } }
    internal bool Generated { get { return Owner == -1; } }
    internal float BlockSize { get { return SandBoxWorld.BlockSizes[SizeIdx]; } }
    internal Vector3 Center
    {
      get
      {
        if (T != null) return T.TransformPoint(LocalBounds.center);
        float s = BlockSize;
        return new Vector3((Cell.x + 0.5f) * s, (Cell.y + 0.5f) * s, (Cell.z + 0.5f) * s);
      }
    }
  }

  internal sealed class SbAim
  {
    internal bool Valid;
    internal bool Hit;
    internal Vector3 Pos;
    internal Quaternion Rot = Quaternion.identity;
    internal Vector3Int Cell;
    internal int SizeIdx;
    internal float Scale = 1f;
    internal Vector3 HitPoint;
    internal Vector3 HitNormal = Vector3.up;
    internal SbObject HitObj;
    internal SbObject AttachTo;   // the object a thruster / wheel / balloon will stick to
    internal string Why;
  }

  internal sealed class SbFluidCell
  {
    internal SbFluid Type;
    internal int Amount;     // 1..8
    internal bool Source;
    internal bool Falling;
    internal SbObject SourceObj;
  }

  internal sealed class SbRelay : MonoBehaviour
  {
    internal SbObject Obj;
    private void OnCollisionEnter(Collision c)
    {
      SandBoxWorld w = SandBoxWorld.Instance;
      if (w != null) w.OnObjectCollision(Obj, c);
    }
  }

  internal sealed partial class SandBoxWorld : MonoBehaviour
  {
    internal static SandBoxWorld Instance;

    // hooks from the tablet (it owns hand tracking)
    internal static Func<bool, Transform> HandProvider;
    internal static Func<bool, Vector3> PointerDirProvider;
    internal static Func<Transform> HeadProvider;
    internal static Action<bool, float, float> Haptic;
    internal static Action<string, string> Toast;
    internal static bool ShowOthers = true;
    // the next Create pops in / the next Remove pops out (SbPopIn / SbPopOut):
    // set around placing and live network spawns, not snapshots or bulk clears
    internal static bool PopNext, PopOutNext;

    internal static readonly float[] BlockSizes = { 0.1f, 0.15f, 0.2f, 0.25f, 0.3f, 0.4f, 0.5f, 0.75f, 1f, 1.5f, 2f, 3f };
    internal const int MaxMine = 2500;
    private const int MaxFluidCells = 6000;

    private readonly Dictionary<long, SbObject> objects = new Dictionary<long, SbObject>();
    internal readonly List<SbObject> List = new List<SbObject>();
    private readonly Dictionary<long, SbObject> grid = new Dictionary<long, SbObject>();
    private readonly Dictionary<Vector3Int, List<SbObject>> hash = new Dictionary<Vector3Int, List<SbObject>>();
    private readonly HashSet<Collider> myColliders = new HashSet<Collider>();
    private readonly List<int> undo = new List<int>();
    private int nextId = 1;
    private int nextGenId = 1;
    private Transform objRoot;

    internal int MineCount { get; private set; }
    internal bool SolidAllowed { get; private set; } = true;
    internal string GateReason { get; private set; } = "OFFLINE";

    internal static SandBoxWorld Ensure()
    {
      if (Instance != null) return Instance;
      var go = new GameObject("Unknowns_SandBoxWorld");
      DontDestroyOnLoad(go);
      Instance = go.AddComponent<SandBoxWorld>();
      return Instance;
    }

    private void Awake()
    {
      Instance = this;
      objRoot = new GameObject("Unknowns_SandBoxObjects").transform;
      DontDestroyOnLoad(objRoot.gameObject);
      SbCatalog.Init();
      SbNet.Init();
      SbMinecraft.StartSounds(this);
      SbMinecraft.EnsureDefaultPack(this);   // the template texture pack, downloaded once when there is none
    }

    private void OnDestroy()
    {
      if (Instance == this) Instance = null;
      SbNet.Shutdown();
    }

    // ================================================================ keys
    internal static long Pack(int s, int x, int y, int z)
    {
      return ((long)(s & 15) << 60) | ((long)(x & 0xFFFFF) << 40) | ((long)(y & 0xFFFFF) << 20) | (long)(z & 0xFFFFF);
    }
    private static int S20(long v) { int i = (int)(v & 0xFFFFF); return (i & 0x80000) != 0 ? i | ~0xFFFFF : i; }
    internal static void Unpack(long k, out int s, out int x, out int y, out int z)
    {
      s = (int)((k >> 60) & 15); x = S20(k >> 40); y = S20(k >> 20); z = S20(k);
    }
    internal static Vector3Int CellOf(Vector3 p, float s)
    {
      return new Vector3Int(Mathf.FloorToInt(p.x / s), Mathf.FloorToInt(p.y / s), Mathf.FloorToInt(p.z / s));
    }
    internal static Vector3 CellCenter(Vector3Int c, float s) { return new Vector3((c.x + 0.5f) * s, (c.y + 0.5f) * s, (c.z + 0.5f) * s); }

    // ================================================================ physics layers / gate
    private int locoMask = 1;
    private int solidLayer;
    private int ghostLayer = 2;
    private bool layersReady;
    private readonly List<Collider> playerCols = new List<Collider>();
    private float nextGate;
    private float nextPlayerCols;

    private void EnsureLayers()
    {
      if (layersReady) return;
      GTPlayer p = GTPlayer.Instance;
      if (p == null) return;
      locoMask = p.locomotionEnabledLayers.value;
      if (locoMask == 0) locoMask = 1;
      solidLayer = 0;
      if ((locoMask & 1) == 0)
        for (int i = 0; i < 32; i++) if ((locoMask & (1 << i)) != 0) { solidLayer = i; break; }
      // ghost layer: not walkable, still collides with the world. Use a layer the game
      // has no colliders on, and switch its collisions on with itself, the solid layer
      // and every walkable layer, so objects never fall through the map or each other.
      int[] tryLayers = { 31, 6, 1 };
      ghostLayer = -1;
      foreach (int l in tryLayers)
        if ((locoMask & (1 << l)) == 0) { ghostLayer = l; break; }
      if (ghostLayer < 0)
        for (int l = 31; l > 0; l--)
          if ((locoMask & (1 << l)) == 0) { ghostLayer = l; break; }
      if (ghostLayer < 0) ghostLayer = 2;
      Physics.IgnoreLayerCollision(ghostLayer, ghostLayer, false);
      Physics.IgnoreLayerCollision(ghostLayer, solidLayer, false);
      Physics.IgnoreLayerCollision(solidLayer, solidLayer, false);
      for (int l = 0; l < 32; l++)
        if ((locoMask & (1 << l)) != 0)
        {
          Physics.IgnoreLayerCollision(ghostLayer, l, false);
          Physics.IgnoreLayerCollision(solidLayer, l, false);
        }
      layersReady = true;
    }

    private void RefreshPlayerColliders()
    {
      playerCols.Clear();
      try
      {
        GTPlayer p = GTPlayer.Instance;
        if (p == null) return;
        foreach (Collider c in p.GetComponentsInChildren<Collider>(true))
          if (c != null && !c.isTrigger) playerCols.Add(c);
        if (p.bodyCollider != null && !playerCols.Contains(p.bodyCollider)) playerCols.Add(p.bodyCollider);
        if (p.headCollider != null && !playerCols.Contains(p.headCollider)) playerCols.Add(p.headCollider);
      }
      catch { }
    }

    private static bool IsModdedRoom()
    {
      try
      {
        string mode = NetworkSystem.Instance != null ? NetworkSystem.Instance.GameModeString : null;
        if (string.IsNullOrEmpty(mode) && PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("gameMode"))
          mode = Convert.ToString(PhotonNetwork.CurrentRoom.CustomProperties["gameMode"]);
        return !string.IsNullOrEmpty(mode) && mode.IndexOf("MODDED", StringComparison.OrdinalIgnoreCase) >= 0;
      }
      catch { return false; }
    }

    private static bool IsSandboxLobbyRoom()
    {
      try
      {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return false;
        string room = PhotonNetwork.CurrentRoom.Name;
        return !string.IsNullOrEmpty(room) && room.StartsWith("_SANDBOX", StringComparison.OrdinalIgnoreCase);
      }
      catch { return false; }
    }

    private void UpdateGate()
    {
      if (Time.unscaledTime < nextGate) return;
      nextGate = Time.unscaledTime + 0.5f;
      bool solid; string why;
      if (!PhotonNetwork.InRoom) { solid = true; why = "OFFLINE"; }
      else if (IsModdedRoom()) { solid = true; why = "UTILLA MODDED LOBBY"; }
      else if (IsSandboxLobbyRoom()) { solid = true; why = "SANDBOX LOBBY"; }
      else
      {
        solid = false; why = "PUBLIC LOBBY";
      }
      GateReason = why;
      if (Time.unscaledTime >= nextPlayerCols || playerCols.Count == 0)
      {
        nextPlayerCols = Time.unscaledTime + 10f;
        var before = new List<Collider>(playerCols);
        RefreshPlayerColliders();
        bool changed = before.Count != playerCols.Count;
        for (int i = 0; !changed && i < before.Count; i++) if (before[i] != playerCols[i]) changed = true;
        if (changed) foreach (SbObject o in List) ApplySolid(o);
      }
      if (solid != SolidAllowed)
      {
        SolidAllowed = solid;
        foreach (SbObject o in List) ApplySolid(o);
      }
    }

    private void ApplySolid(SbObject o)
    {
      if (o == null || o.ColGo == null) return;
      EnsureLayers();
      bool solid = SolidAllowed && !o.Held && !o.HeldRemote && (ShowOthers || o.Mine || o.Generated);
      o.ColGo.layer = solid ? solidLayer : ghostLayer;
      foreach (Collider c in o.Cols)
      {
        if (c == null) continue;
        c.gameObject.layer = solid ? solidLayer : ghostLayer;   // ragdoll limbs sit on their bones
        c.enabled = !o.Held && !o.HeldRemote;
        foreach (Collider pc in playerCols)
          if (pc != null) { try { Physics.IgnoreCollision(c, pc, !solid); } catch { } }
      }
    }

    // ================================================================ creating
    internal SbObject Create(int owner, int id, SbItem item, Vector3 pos, Quaternion rot, float scale, bool anchored, int sizeIdx, Vector3Int cell)
    {
      if (item == null) return null;
      long key = ((long)owner << 32) | (uint)id;
      SbObject existing;
      if (objects.TryGetValue(key, out existing)) Remove(existing, false, false);

      var o = new SbObject { Owner = owner, Id = id, Item = item, Scale = scale, Anchored = anchored, SizeIdx = Mathf.Clamp(sizeIdx, 0, BlockSizes.Length - 1), Cell = cell, SpawnTime = Time.time };
      o.IsCell = item.Kind == SbKind.Block || item.Kind == SbKind.Fluid;

      if (item.Kind == SbKind.Fluid)
      {
        o.LocalBounds = new Bounds(Vector3.zero, Vector3.one);
        objects[key] = o;
        List.Add(o);
        if (o.Mine) MineCount++;
        SetFluidSource(o, true);
        return o;
      }

      if (item.Ragdoll)
      {
        SbObject rag = CreateRagdoll(o, key, pos, rot, scale, anchored);
        if (rag != null) return rag;
      }
      if (item.Kind == SbKind.Mob)
      {
        SbObject mob = CreateMob(o, key, pos, rot, scale);
        if (mob != null) return mob;
      }

      var go = new GameObject("SB " + item.Name);
      go.transform.SetParent(objRoot, false);
      o.Go = go; o.T = go.transform;
      if (o.IsCell)
      {
        float s = o.BlockSize;
        o.T.position = CellCenter(cell, s);
        o.T.rotation = rot;
        o.T.localScale = Vector3.one * s;
      }
      else
      {
        o.T.position = pos;
        o.T.rotation = rot;
        o.T.localScale = Vector3.one * scale;
      }
      var mf = go.AddComponent<MeshFilter>();
      mf.sharedMesh = item.Mesh;
      var mr = go.AddComponent<MeshRenderer>();
      mr.sharedMaterials = SbMats.For(item);
      o.LocalBounds = item.Mesh != null ? item.Mesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.3f);

      // colliders live on a child so their layer can change (walkable or not)
      var cg = new GameObject("col");
      cg.transform.SetParent(o.T, false);
      o.ColGo = cg;
      Collider col = null;
      if (o.IsCell && item.BlockFx == SbBlockFx.Portal)
      {
        // a portal is a thin see-through pane you walk into (no collider)
        o.T.localScale = new Vector3(o.BlockSize, o.BlockSize, o.BlockSize * 0.25f);
      }
      else if (o.IsCell && item.BlockFx == SbBlockFx.EndPortal)
      {
        // the End portal: a thin starry surface 3/4 up the frame you jump into (no collider)
        o.T.localScale = new Vector3(o.BlockSize, o.BlockSize * 0.08f, o.BlockSize);
        o.T.position += Vector3.up * o.BlockSize * 0.25f;
        // drawn like Minecraft's (layered drifting stars), not as a textured block
        mr.enabled = false;
        Vector3 surface = o.T.position + Vector3.up * o.BlockSize * 0.04f;
        SbEndPortalArt.Build(o.T, new List<Vector3[]> { SbEndPortalArt.Rect(surface, o.BlockSize, o.BlockSize) });
      }
      else if (o.IsCell && item.Shape != SbShape.Cube)
      {
        // a shape's own boxes (SandBoxRedstone.cs); plates and buttons have none
        AddShapeColliders(o, cg, false);
        foreach (Collider sc in cg.GetComponents<Collider>()) { if (col == null) col = sc; else { o.Cols.Add(sc); myColliders.Add(sc); } }
      }
      else if (o.IsCell)
      {
        var bc = cg.AddComponent<BoxCollider>(); bc.size = Vector3.one; col = bc;
      }
      else if (item.Col == SbCol.Sphere)
      {
        var sc = cg.AddComponent<SphereCollider>();
        sc.center = o.LocalBounds.center;
        sc.radius = Mathf.Max(o.LocalBounds.extents.x, Mathf.Max(o.LocalBounds.extents.y, o.LocalBounds.extents.z));
        col = sc;
      }
      else if (item.Col == SbCol.Mesh && item.Mesh != null && item.Mesh.vertexCount < 4000)
      {
        var mc = cg.AddComponent<MeshCollider>();
        mc.sharedMesh = item.Mesh;
        mc.convex = true;
        col = mc;
      }
      if (col == null && !(o.IsCell && (item.BlockFx == SbBlockFx.Portal || item.BlockFx == SbBlockFx.EndPortal || item.Shape != SbShape.Cube)))
      {
        var bc = cg.AddComponent<BoxCollider>();
        bc.center = o.LocalBounds.center;
        bc.size = Vector3.Max(o.LocalBounds.size, Vector3.one * 0.02f);
        col = bc;
      }
      if (item.Bounce > 0.3f && col != null)
      {
        var pm = new PhysicsMaterial("SB Bouncy") { bounciness = item.Bounce, bounceCombine = PhysicsMaterialCombine.Maximum, dynamicFriction = 0.3f, staticFriction = 0.4f };
        col.sharedMaterial = pm;
      }
      if (col != null) { o.Cols.Add(col); myColliders.Add(col); }

      // Gorilla Tag surfaces: slime / trampolines boost jumps, ice slides
      try
      {
        if (item.BlockFx == SbBlockFx.Slime || item.PropFx == SbPropFx.Trampoline)
        {
          var so = cg.AddComponent<GorillaSurfaceOverride>();
          so.extraVelMultiplier = item.PropFx == SbPropFx.Trampoline ? 2.2f : 1.8f;
          so.extraVelMaxMultiplier = item.PropFx == SbPropFx.Trampoline ? 2.2f : 1.8f;
        }
        else if (item.BlockFx == SbBlockFx.Ice)
        {
          var so = cg.AddComponent<GorillaSurfaceOverride>();
          so.slidePercentageOverride = 0.92f;
        }
      }
      catch { }

      if (!anchored && !o.IsCell) MakeDynamic(o, Vector3.zero);

      if (item.PropFx == SbPropFx.Light || item.PropFx == SbPropFx.Fire || item.BlockFx == SbBlockFx.Glow)
        AddLight(o);
      if (item.PropFx == SbPropFx.Fire)
      {
        o.Fire = SbFx.Burst(o.T.position, 1, Color.white, Color.white, 0f, 0f, 0f, 0f, 0.1f, 0.1f, 0f, true);
        if (o.Fire != null)
        {
          o.Fire.transform.SetParent(o.T, true);
          var main = o.Fire.main;
          main.loop = true;
          main.stopAction = ParticleSystemStopAction.None;
          main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
          main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
          main.startSize = new ParticleSystem.MinMaxCurve(0.08f * scale, 0.18f * scale);
          main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.35f, 0.05f));
          main.gravityModifier = -0.15f;
          main.maxParticles = 80;
          var em = o.Fire.emission; em.rateOverTime = 26f;
          var sh = o.Fire.shape; sh.radius = 0.1f * scale;
          o.Fire.Play();
        }
      }

      var relay = go.AddComponent<SbRelay>();
      relay.Obj = o;

      objects[key] = o;
      List.Add(o);
      if (o.Mine) MineCount++;
      if (o.IsCell) AddToGrid(o);
      NotePortal(o, true);
      if (!ShowOthers && !o.Mine && !o.Generated) go.SetActive(false);
      ApplySolid(o);
      if (o.IsCell) { ScheduleFluidAround(o.SizeIdx, o.Cell); CheckFalling(o); CheckFallingAbove(o.SizeIdx, o.Cell); }
      if (item.BlockFx == SbBlockFx.Sponge) Soak(o);
      SetupAttachVisual(o);
      if (item.Vehicle != null) SetupVehicle(o);
      if (PopNext) SbPopIn.Play(o.T);
      return o;
    }

    // A Gorilla Tag gorilla ragdoll copied from the spawner's rig (SbRagdoll)
    private SbObject CreateRagdoll(SbObject o, long key, Vector3 pos, Quaternion rot, float scale, bool anchored)
    {
      VRRig src = null;
      try
      {
        if (o.Owner > 0) src = RigOf(o.Owner);
        if (src == null && GorillaTagger.Instance != null) src = GorillaTagger.Instance.offlineVRRig;
      }
      catch { }
      SbRagdoll.Result res = null;
      // a random fur colour for every ragdoll, the same on every client (owner's actor + id)
      int ownerActor = o.Owner;
      if (ownerActor == 0) { try { ownerActor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0; } catch { } }
      uint rh = unchecked((uint)(o.Id * 73856093 ^ ownerActor * 19349663) * 2654435761u);
      Color fur = Color.HSVToRGB((rh & 0xFFFF) / 65535f, 0.55f + ((rh >> 16) & 0xFF) / 255f * 0.35f, 0.75f + ((rh >> 24) & 0xFF) / 255f * 0.25f);
      try { res = SbRagdoll.Build(src, objRoot, fur); } catch (Exception ex) { SbLog.Warn("ragdoll: " + ex.Message); }
      if (res == null || res.Root == null)
      {
        SbLog.Warn("ragdoll: could not copy the gorilla (" + SbRagdoll.LastError + "), using the simple model");
        return null;
      }
      o.Go = res.Root; o.T = res.Root.transform;
      o.T.position = pos;
      o.T.rotation = rot;
      o.T.localScale = o.T.localScale * Mathf.Clamp(scale, 0.25f, 4f);
      Transform col = o.T.Find("col");
      o.ColGo = col != null ? col.gameObject : o.Go;
      o.LocalBounds = res.LocalBounds;
      foreach (Collider c in res.Cols) { o.Cols.Add(c); myColliders.Add(c); }
      for (int i = 0; i < res.Cols.Count; i++)
        for (int j = i + 1; j < res.Cols.Count; j++)
          try { Physics.IgnoreCollision(res.Cols[i], res.Cols[j], true); } catch { }
      MakeDynamic(o, Vector3.zero);
      if (anchored && o.Rb != null) { o.Anchored = true; o.Rb.isKinematic = true; }
      SbRagdoll.ConnectJoints(o.Go);
      o.Go.AddComponent<SbRelay>().Obj = o;
      objects[key] = o;
      List.Add(o);
      if (o.Mine) MineCount++;
      if (!ShowOthers && !o.Mine && !o.Generated) o.Go.SetActive(false);
      ApplySolid(o);
      return o;
    }

    private int lightCount;
    private void AddLight(SbObject o)
    {
      if (lightCount >= 14 || o.T == null) return;
      var lg = new GameObject("light");
      lg.transform.SetParent(o.T, false);
      lg.transform.localPosition = o.LocalBounds.center + Vector3.up * o.LocalBounds.extents.y * 0.6f;
      var l = lg.AddComponent<Light>();
      l.type = LightType.Point;
      l.shadows = LightShadows.None;
      l.range = 3f * Mathf.Max(0.5f, o.IsCell ? o.BlockSize * 3f : o.Scale);
      l.intensity = o.Item.PropFx == SbPropFx.Fire ? 1.6f : 1.2f;
      Color c = o.Item.LightColor;
      if (o.Item.Id == "mc_sealantern") c = new Color(0.7f, 0.95f, 1f);
      if (o.Item.Id == "ob_ring") c = new Color(0.3f, 0.9f, 1f);
      if (o.Item.Id == "ob_neonbar" || o.Item.Id == "ob_arcade") c = new Color(1f, 0.35f, 0.95f);
      l.color = c;
      o.Light = l;
      lightCount++;
    }

    private void MakeDynamic(SbObject o, Vector3 velocity)
    {
      if (o.Go == null) return;
      if (o.Rb == null) o.Rb = o.Go.AddComponent<Rigidbody>();
      o.Anchored = false;
      o.Rb.mass = Mathf.Clamp(o.Item.Mass * o.Scale * o.Scale * o.Scale, 0.02f, 200f);
      o.Rb.interpolation = RigidbodyInterpolation.Interpolate;
      o.Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
      o.Rb.isKinematic = !(o.Mine || o.Generated);   // yours, or made here (natural mobs): simulated here
      o.Rb.useGravity = o.Item.PropFx != SbPropFx.Balloon;
      o.Rb.linearDamping = o.Item.PropFx == SbPropFx.Balloon ? 1.2f : 0.05f;
      o.Rb.angularDamping = 0.3f;
      if (o.Mine && !o.Rb.isKinematic) o.Rb.linearVelocity = velocity;
    }

    internal void Remove(SbObject o, bool broadcast, bool fx)
    {
      if (o == null) return;
      if (!objects.Remove(o.Key)) return;
      List.Remove(o);
      // what is stuck to it goes with it (SandBoxAttach.cs)
      DetachForRemove(o, broadcast);
      if (o == Driving) ExitVehicle();
      if (o.Mine) MineCount = Mathf.Max(0, MineCount - 1);
      if (heldL == o) heldL = null;
      if (heldR == o) heldR = null;
      if (o.Light != null) lightCount = Mathf.Max(0, lightCount - 1);
      if (o.Countdown != null) Destroy(o.Countdown.gameObject);
      Vector3 c = o.Center;
      if (fx)
      {
        float size = o.IsCell ? o.BlockSize : Mathf.Max(0.2f, o.LocalBounds.size.magnitude * o.Scale * 0.5f);
        // blocks break into Minecraft bits of their own texture
        if (o.Item.Kind == SbKind.Block) SbFx.McBreak(c, o.Item.Side, size, Color.white);
        else SbFx.Debris(c, ItemColor(o.Item), size);
        if (o.Item.Kind == SbKind.Block) SbFx.PlayMc(o.Item.Sound, o.Item.BlockFx == SbBlockFx.Glass ? SbSound.Glass : SbSound.Break, c, 1f);
        else SbFx.Play(o.Item.BlockFx == SbBlockFx.Glass ? SbSound.Glass : SbSound.Break, c, 0.8f);
      }
      if (o.Item.Kind == SbKind.Fluid) SetFluidSource(o, false);
      if (o.IsCell && o.Item.Kind == SbKind.Block) RemoveFromGrid(o);
      NotePortal(o, false);
      if (o.IsCell && (o.Item.Id == "mc_obsidian" || o.Item.BlockFx == SbBlockFx.Portal)) BreakPortalsNear(o.SizeIdx, o.Cell);
      if (o.IsCell && o.Item.Id == "mc_endframe_eye" && !EndEyeSwap) BreakEndPortalNear(o.SizeIdx, o.Cell);
      foreach (Collider col in o.Cols) if (col != null) myColliders.Remove(col);
      if (o.Go != null)
      {
        if ((fx || PopOutNext) && o.Go.activeInHierarchy && !o.Item.Ragdoll) SbPopOut.Play(o.Go);
        else Destroy(o.Go);
      }
      if (o.IsCell)
      {
        ScheduleFluidAround(o.SizeIdx, o.Cell);
        CheckFallingAbove(o.SizeIdx, o.Cell);
      }
      if (broadcast && o.Mine) SbNet.QueueRemove(o.Id);
      if (o.Mine) undo.Remove(o.Id);
    }

    internal static Color ItemColor(SbItem item)
    {
      if (item == null) return Color.gray;
      if (item.Kind == SbKind.Block)
      {
        Rect r = SbBlockArt.TileRect(item.Side);
        Color sum = Color.clear;
        for (int i = 0; i < 4; i++) sum += (Color)SbBlockArt.Sample(new Vector2(r.x + r.width * (0.2f + 0.2f * i), r.y + r.height * 0.5f));
        sum /= 4f; sum.a = 1f;
        return sum;
      }
      if (item.Kind == SbKind.Fluid) return item.Fluid == SbFluid.Water ? new Color(0.25f, 0.45f, 0.9f) : new Color(1f, 0.45f, 0.1f);
      return new Color(0.75f, 0.75f, 0.78f);
    }

    // ================================================================ grid / hash
    private void AddToGrid(SbObject o)
    {
      grid[Pack(o.SizeIdx, o.Cell.x, o.Cell.y, o.Cell.z)] = o;
      HashOp(o, true);
    }

    private void RemoveFromGrid(SbObject o)
    {
      long k = Pack(o.SizeIdx, o.Cell.x, o.Cell.y, o.Cell.z);
      SbObject cur;
      if (grid.TryGetValue(k, out cur) && cur == o) grid.Remove(k);
      HashOp(o, false);
    }

    private void HashOp(SbObject o, bool add)
    {
      float s = o.BlockSize;
      Vector3 min = new Vector3(o.Cell.x * s, o.Cell.y * s, o.Cell.z * s);
      Vector3 max = min + Vector3.one * s;
      for (int x = Mathf.FloorToInt(min.x); x <= Mathf.FloorToInt(max.x - 0.0001f); x++)
        for (int y = Mathf.FloorToInt(min.y); y <= Mathf.FloorToInt(max.y - 0.0001f); y++)
          for (int z = Mathf.FloorToInt(min.z); z <= Mathf.FloorToInt(max.z - 0.0001f); z++)
          {
            var k = new Vector3Int(x, y, z);
            List<SbObject> l;
            if (!hash.TryGetValue(k, out l)) { if (!add) continue; l = new List<SbObject>(); hash[k] = l; }
            if (add) l.Add(o); else { l.Remove(o); if (l.Count == 0) hash.Remove(k); }
          }
    }

    // any block (any size) overlapping this box
    private bool BlockOverlap(Vector3 center, float half, SbObject ignore = null)
    {
      Vector3 min = center - Vector3.one * half, max = center + Vector3.one * half;
      for (int x = Mathf.FloorToInt(min.x); x <= Mathf.FloorToInt(max.x); x++)
        for (int y = Mathf.FloorToInt(min.y); y <= Mathf.FloorToInt(max.y); y++)
          for (int z = Mathf.FloorToInt(min.z); z <= Mathf.FloorToInt(max.z); z++)
          {
            List<SbObject> l;
            if (!hash.TryGetValue(new Vector3Int(x, y, z), out l)) continue;
            foreach (SbObject o in l)
            {
              if (o == ignore || o.Falling || o.Primed) continue;
              float s = o.BlockSize;
              Vector3 bmin = new Vector3(o.Cell.x * s, o.Cell.y * s, o.Cell.z * s), bmax = bmin + Vector3.one * s;
              if (min.x < bmax.x && max.x > bmin.x && min.y < bmax.y && max.y > bmin.y && min.z < bmax.z && max.z > bmin.z) return true;
            }
          }
      return false;
    }

    private readonly Collider[] overlapBuf = new Collider[16];
    private readonly Dictionary<long, bool> worldSolidCache = new Dictionary<long, bool>();
    private float worldCacheClearAt;

    private bool WorldSolid(int s, Vector3Int c)
    {
      long k = Pack(s, c.x, c.y, c.z);
      bool v;
      if (worldSolidCache.TryGetValue(k, out v)) return v;
      EnsureLayers();
      float size = BlockSizes[s];
      int n = Physics.OverlapBoxNonAlloc(CellCenter(c, size), Vector3.one * size * 0.42f, overlapBuf, Quaternion.identity, locoMask, QueryTriggerInteraction.Ignore);
      v = false;
      for (int i = 0; i < n; i++)
      {
        Collider col = overlapBuf[i];
        if (col == null || myColliders.Contains(col) || playerCols.Contains(col)) continue;
        v = true; break;
      }
      if (worldSolidCache.Count > 200000) worldSolidCache.Clear();
      worldSolidCache[k] = v;
      return v;
    }

    internal bool CellSolid(int s, Vector3Int c)
    {
      SbObject b;
      if (grid.TryGetValue(Pack(s, c.x, c.y, c.z), out b) && b != null && !b.Falling && !b.Primed) return true;
      float size = BlockSizes[s];
      if (BlockOverlap(CellCenter(c, size), size * 0.45f)) return true;
      return WorldSolid(s, c);
    }

    // ================================================================ aiming
    private readonly RaycastHit[] rayBuf = new RaycastHit[32];

    internal bool RaycastWorld(Ray ray, float max, out RaycastHit best)
    {
      EnsureLayers();
      best = default(RaycastHit);
      int n = Physics.RaycastNonAlloc(ray, rayBuf, max, locoMask, QueryTriggerInteraction.Ignore);
      float bd = float.MaxValue; bool found = false;
      for (int i = 0; i < n; i++)
      {
        RaycastHit h = rayBuf[i];
        if (h.collider == null || myColliders.Contains(h.collider) || playerCols.Contains(h.collider)) continue;
        if (h.distance < bd) { bd = h.distance; best = h; found = true; }
      }
      return found;
    }

    internal SbObject RaycastObjects(Ray ray, float max, out float dist, out Vector3 normal, bool includeSources, bool onlyMine, SbObject ignore = null)
    {
      dist = max; normal = Vector3.up;
      SbObject best = null;
      foreach (SbObject o in List)
      {
        if (o == ignore || o.Held || o.HeldRemote) continue;
        // anyone's blocks can be broken, their other things only by them
        if (onlyMine && !o.Mine && !o.Generated && !(o.Owner > 0 && o.Item.Kind == SbKind.Block)) continue;
        if (!ShowOthers && !o.Mine && !o.Generated) continue;
        if (o.Item.Kind == SbKind.Fluid && !includeSources) continue;
        Vector3 lo, ld; Bounds b;
        if (o.T != null)
        {
          lo = o.T.InverseTransformPoint(ray.origin);
          ld = o.T.InverseTransformVector(ray.direction);
          b = o.IsCell ? new Bounds(Vector3.zero, Vector3.one) : o.LocalBounds;
        }
        else
        {
          float s = o.BlockSize;
          Vector3 c = CellCenter(o.Cell, s);
          lo = (ray.origin - c) / s; ld = ray.direction / s;
          b = new Bounds(Vector3.zero, Vector3.one);
        }
        float t; Vector3 ln;
        if (!RayBox(lo, ld, b, out t, out ln)) continue;
        if (t < 0f || t >= dist) continue;
        dist = t; best = o;
        normal = o.T != null ? o.T.TransformDirection(ln).normalized : ln;
      }
      return best;
    }

    private static bool RayBox(Vector3 o, Vector3 d, Bounds b, out float t, out Vector3 n)
    {
      t = 0f; n = Vector3.up;
      float tmin = float.MinValue, tmax = float.MaxValue;
      Vector3 nmin = Vector3.zero;
      for (int a = 0; a < 3; a++)
      {
        float oa = o[a], da = d[a], mn = b.min[a], mx = b.max[a];
        if (Mathf.Abs(da) < 1e-8f)
        {
          if (oa < mn || oa > mx) return false;
          continue;
        }
        float t1 = (mn - oa) / da, t2 = (mx - oa) / da;
        Vector3 n1 = Vector3.zero; n1[a] = -1f;
        if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; n1[a] = 1f; }
        if (t1 > tmin) { tmin = t1; nmin = n1; }
        if (t2 < tmax) tmax = t2;
        if (tmin > tmax) return false;
      }
      if (tmax < 0f) return false;
      t = tmin >= 0f ? tmin : 0f;
      n = nmin.sqrMagnitude > 0f ? nmin : -d.normalized;
      return true;
    }

    private static Vector3 SnapAxis(Vector3 n)
    {
      float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
      if (ax >= ay && ax >= az) return new Vector3(Mathf.Sign(n.x), 0f, 0f);
      if (ay >= az) return new Vector3(0f, Mathf.Sign(n.y), 0f);
      return new Vector3(0f, 0f, Mathf.Sign(n.z));
    }

    private Vector3 PlayerPos()
    {
      Transform h = HeadProvider != null ? HeadProvider() : null;
      return h != null ? h.position : Vector3.zero;
    }

    internal void ComputeAim(Vector3 origin, Vector3 dir, SbItem item, int sizeIdx, float scale, float yawOffset, bool snap, SbAim aim)
    {
      aim.Valid = false; aim.Hit = false; aim.HitObj = null; aim.Why = null;
      if (item == null || dir.sqrMagnitude < 1e-6f) return;
      dir.Normalize();
      var ray = new Ray(origin, dir);
      const float reach = 12f;
      RaycastHit wh; bool wHit = RaycastWorld(ray, reach, out wh);
      float od; Vector3 on;
      SbObject oHit = RaycastObjects(ray, reach, out od, out on, false, false);
      bool objCloser = oHit != null && (!wHit || od <= wh.distance);
      Vector3 hp = objCloser ? origin + dir * od : (wHit ? wh.point : origin + dir * 2.5f);
      Vector3 hn = objCloser ? on : (wHit ? wh.normal : -dir);
      aim.Hit = objCloser || wHit;
      aim.HitPoint = hp; aim.HitNormal = hn; aim.HitObj = objCloser ? oHit : null;
      Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
      if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
      flat.Normalize();

      if (item.Kind == SbKind.Block || item.Kind == SbKind.Fluid)
      {
        sizeIdx = Mathf.Clamp(sizeIdx, 0, BlockSizes.Length - 1);
        float s = BlockSizes[sizeIdx];
        Vector3 target;
        if (objCloser && oHit.IsCell && oHit.Item.Kind == SbKind.Block)
        {
          Vector3 ax = SnapAxis(on);
          target = oHit.Center + ax * (oHit.BlockSize * 0.5f + s * 0.5f);
        }
        else if (aim.Hit) target = hp + SnapAxis(hn) * (s * 0.5f) + hn * 0.001f;
        else target = origin + dir * Mathf.Max(1.2f, s * 4f);
        Vector3Int cell = CellOf(target, s);
        aim.Cell = cell; aim.SizeIdx = sizeIdx; aim.Scale = s;
        aim.Pos = CellCenter(cell, s);
        float yaw = Mathf.Round(Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg / 90f) * 90f;
        aim.Rot = Quaternion.Euler(0f, yaw, 0f);
        SbObject there;
        if (grid.TryGetValue(Pack(sizeIdx, cell.x, cell.y, cell.z), out there) && there != null) { aim.Why = "occupied"; return; }
        if (BlockOverlap(aim.Pos, s * 0.49f)) { aim.Why = "occupied"; return; }
        if (SolidAllowed && item.Kind == SbKind.Block && OverlapsPlayer(aim.Pos, s * 0.5f)) { aim.Why = "inside you"; return; }
        // never inside another player
        if (item.Kind == SbKind.Block && OverlapsOtherPlayer(aim.Pos, s * 0.5f)) { aim.Why = "a player is there"; return; }
        aim.Valid = true;
        return;
      }

      // free objects
      Quaternion rot = item.IsHeldType && item.Kind != SbKind.Explosive
        ? Quaternion.LookRotation(flat, Vector3.up)
        : Quaternion.LookRotation(-flat, Vector3.up);
      rot = Quaternion.AngleAxis(yawOffset, Vector3.up) * rot;
      aim.Rot = rot; aim.Scale = scale;
      Vector3 pos;
      if (aim.Hit)
      {
        Bounds b = item.Mesh != null ? item.Mesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.3f);
        float minDot = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
          Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
          float dd = Vector3.Dot(rot * (corner * scale), hn);
          if (dd < minDot) minDot = dd;
        }
        pos = hp + hn * (-minDot + 0.002f);
      }
      else pos = origin + dir * 2.0f;
      if (snap) pos = new Vector3(Mathf.Round(pos.x / 0.05f) * 0.05f, Mathf.Round(pos.y / 0.05f) * 0.05f, Mathf.Round(pos.z / 0.05f) * 0.05f);
      aim.Pos = pos;
      aim.Valid = true;
    }

    private bool OverlapsPlayer(Vector3 c, float half)
    {
      try
      {
        GTPlayer p = GTPlayer.Instance;
        if (p == null) return false;
        Vector3 head = p.headCollider.transform.position;
        Vector3 body = p.bodyCollider.transform.position;
        float r = 0.18f * p.scale;
        return BoxSphere(c, half, head, r) || BoxSphere(c, half, body, r * 1.2f) || BoxSphere(c, half, (head + body) * 0.5f, r);
      }
      catch { return false; }
    }

    // the other players' bodies (head, chest and between), as the game shows them
    private static VRRig[] otherRigs = new VRRig[0];
    private static float otherRigsAt = -1f;

    private static bool OverlapsOtherPlayer(Vector3 c, float half)
    {
      try
      {
        if (Time.unscaledTime >= otherRigsAt)
        {
          otherRigsAt = Time.unscaledTime + 0.5f;
          otherRigs = FindObjectsByType<VRRig>(FindObjectsSortMode.None);
        }
        foreach (VRRig rig in otherRigs)
        {
          if (rig == null || rig.isOfflineVRRig || rig.isMyPlayer || !rig.gameObject.activeInHierarchy) continue;
          float sc = Mathf.Max(0.2f, rig.scaleFactor);
          Vector3 body = rig.transform.position;
          Vector3 head = rig.head != null && rig.head.rigTarget != null ? rig.head.rigTarget.position : body + Vector3.up * 0.3f * sc;
          float r = 0.2f * sc;
          if (BoxSphere(c, half, head, r) || BoxSphere(c, half, body, r * 1.3f) || BoxSphere(c, half, (head + body) * 0.5f, r)) return true;
          // the hands too, so a block is not put in someone's grip
          if (rig.leftHand != null && rig.leftHand.rigTarget != null && BoxSphere(c, half, rig.leftHand.rigTarget.position, 0.08f * sc)) return true;
          if (rig.rightHand != null && rig.rightHand.rigTarget != null && BoxSphere(c, half, rig.rightHand.rigTarget.position, 0.08f * sc)) return true;
        }
      }
      catch { }
      return false;
    }

    private static bool BoxSphere(Vector3 c, float half, Vector3 p, float r)
    {
      Vector3 d = new Vector3(Mathf.Max(0f, Mathf.Abs(p.x - c.x) - half), Mathf.Max(0f, Mathf.Abs(p.y - c.y) - half), Mathf.Max(0f, Mathf.Abs(p.z - c.z) - half));
      return d.sqrMagnitude < r * r;
    }

    // ================================================================ placing / deleting
    internal SbObject Place(SbAim aim, SbItem item, bool anchored)
    {
      if (aim == null || !aim.Valid || item == null) return null;
      if (MineCount >= MaxMine)
      {
        Toast?.Invoke("SandBox", "Object limit reached (" + MaxMine + "). Delete some first.");
        return null;
      }
      PopNext = true;
      SbObject o;
      try { o = Create(0, nextId++, item, aim.Pos, aim.Rot, aim.Scale, anchored || item.Kind == SbKind.Block, aim.SizeIdx, aim.Cell); }
      finally { PopNext = false; }
      if (o == null) return null;
      undo.Add(o.Id);
      if (item.Kind == SbKind.Block) SbFx.PlayMc(item.Sound, SbSound.Place, aim.Pos, 1f, 0.8f);
      else if (item.Kind == SbKind.Fluid) SbFx.PlayMc(item.Fluid == SbFluid.Lava ? "bucket_lava" : "bucket_water", SbSound.Splash, aim.Pos, 1f);
      else SbFx.Play(SbSound.Place, aim.Pos, 0.7f, 0.8f);
      SbNet.QueueSpawn(o);
      // a thruster / wheel / balloon placed on one of your objects sticks to it
      if (item.Id == "mc_wither_skull") CheckWitherBuild(o);
      if (aim.AttachTo != null && aim.AttachTo.T != null && objects.ContainsKey(aim.AttachTo.Key)) AttachTo(o, aim.AttachTo, true);
      return o;
    }

    internal SbObject SpawnHeld(SbItem item, bool right, float scale)
    {
      Transform hand = HandProvider != null ? HandProvider(right) : null;
      if (hand == null || item == null) return null;
      if (right ? heldR != null : heldL != null) Release(right, false);
      SbObject o = Create(0, nextId++, item, hand.position, hand.rotation, scale, true, 0, Vector3Int.zero);
      if (o == null) return null;
      undo.Add(o.Id);
      SbNet.QueueSpawn(o);
      Grab(o, right);
      SbFx.Play(SbSound.Place, hand.position, 0.6f, 1.3f);
      return o;
    }

    internal SbObject AimDelete(Vector3 origin, Vector3 dir)
    {
      var ray = new Ray(origin, dir.normalized);
      float od; Vector3 on;
      SbObject o = RaycastObjects(ray, 12f, out od, out on, true, true);
      if (o == null) return null;
      RaycastHit wh;
      if (RaycastWorld(ray, od, out wh) && wh.distance < od - 0.01f) return null;
      return o;
    }

    internal void Delete(SbObject o)
    {
      if (o == null) return;
      // another player's block: it breaks here at once and its owner (who
      // keeps it in their build) is told to break it for everyone
      if (!o.Mine && !o.Generated)
      {
        if (o.Owner <= 0 || o.Item.Kind != SbKind.Block) return;
        int owner = o.Owner, id = o.Id;
        Remove(o, false, true);
        SbNet.SendBreak(owner, id);
        return;
      }
      Remove(o, true, true);
    }

    // someone broke one of your blocks
    internal void NetBreakMine(int id)
    {
      SbObject o;
      if (objects.TryGetValue(id, out o) && o != null && o.Mine && o.Item.Kind == SbKind.Block) Remove(o, true, true);
    }

    internal void Undo()
    {
      while (undo.Count > 0)
      {
        int id = undo[undo.Count - 1];
        undo.RemoveAt(undo.Count - 1);
        SbObject o;
        if (objects.TryGetValue(id, out o) && o != null) { Remove(o, true, true); return; }
      }
    }

    internal void ClearMine()
    {
      var copy = new List<SbObject>(List);
      foreach (SbObject o in copy) if (o.Mine || o.Generated) Remove(o, false, false);
      undo.Clear();
      SbNet.SendClear();
    }

    internal void DetonateAll()
    {
      var copy = new List<SbObject>(List);
      foreach (SbObject o in copy)
      {
        if (!o.Mine || o.Primed) continue;
        if (o.Item.Kind == SbKind.Explosive || o.Item.BlockFx == SbBlockFx.Tnt)
          Prime(o, o.Item.Blast == SbBlast.Nuke ? o.Item.Fuse : 0.05f + (float)UnityEngine.Random.value * 0.35f, true);
      }
    }

    // ================================================================ update
    private void Update()
    {
      EnsureLayers();
      UpdateGate();
      SbNet.Tick(this);
      float dt = Time.deltaTime;
      UpdateHands(dt);
      UpdateObjects(dt);
      UpdateAttachments(dt);
      UpdateDriving();
      UpdateWeldHighlight();
      UpdateProjectiles(dt);
      UpdateFluids(dt);
      UpdateFalling(dt);
      UpdateSwimVisuals();
      UpdatePortals(dt);
      UpdateMobsWorld();
      UpdateGadgets(dt);
      UpdateBossWorld();
      UpdateMcItems(dt);
      try { UpdateRedstone(dt); } catch (Exception ex) { SbLog.Throttled("SandBox redstone", ex); }
      // the portal swirl rises while you stand in it and fades as you arrive
      PortalSwirl = Mathf.MoveTowards(PortalSwirl, swirlWant, Time.unscaledDeltaTime * (swirlWant > PortalSwirl ? 1.2f : 1.4f));
      SbPortalOverlay.Tick(PortalSwirl);
      if (Time.unscaledTime > worldCacheClearAt)
      {
        worldCacheClearAt = Time.unscaledTime + 90f;
        worldSolidCache.Clear();
      }
    }

    private float nextMoveSend;

    private void UpdateObjects(float dt)
    {
      bool sendMoves = Time.unscaledTime >= nextMoveSend;
      if (sendMoves) nextMoveSend = Time.unscaledTime + 0.1f;
      Transform lh = HandProvider != null ? HandProvider(false) : null;
      Transform rh = HandProvider != null ? HandProvider(true) : null;

      for (int i = List.Count - 1; i >= 0; i--)
      {
        if (i >= List.Count) continue;
        SbObject o = List[i];
        if (o.T == null) continue;
        SbItem it = o.Item;

        if (it.PropFx == SbPropFx.Spin && !o.Held) o.T.Rotate(Vector3.up, 40f * dt, Space.World);
        if (it.PropFx == SbPropFx.Balloon && o.Rb != null && !o.Rb.isKinematic && !o.Held)
          o.Rb.AddForce(Vector3.up * 2.2f, ForceMode.Acceleration);

        // remote dynamic objects follow the owner's updates
        if (!o.Mine && !o.Generated && o.HasNet && !o.HeldRemote)
        {
          float age = Mathf.Min(Time.unscaledTime - o.NetTime, 0.3f);
          Vector3 target = o.NetPos + o.NetVel * age;
          float k = 1f - Mathf.Exp(-14f * dt);
          o.T.position = Vector3.Lerp(o.T.position, target, k);
          o.T.rotation = Quaternion.Slerp(o.T.rotation, o.NetRot, k);
        }
        if (o.HeldRemote) FollowRemoteHand(o);

        if (o.Primed) UpdatePrimed(o, dt);
        else if (o.Mine && !o.Held)
        {
          // poke to light: TNT, nukes, fireworks
          bool touchPrime = it.BlockFx == SbBlockFx.Tnt || it.Blast == SbBlast.Nuke || it.Blast == SbBlast.Firework;
          if (touchPrime && (Touching(o, lh) || Touching(o, rh)) && Time.time - o.SpawnTime > 0.8f)
          {
            Prime(o, it.Kind == SbKind.Block ? 4f : it.Fuse, true);
          }
          if (it.Blast == SbBlast.Landmine && Time.time - o.SpawnTime > 2f && LandmineTriggered(o))
            Prime(o, it.Fuse, true);
        }

        if (o.Mine && sendMoves && o.Rb != null && !o.Rb.isKinematic && !o.Held)
        {
          bool moved = (o.T.position - o.SentPos).sqrMagnitude > 0.0001f || Quaternion.Angle(o.T.rotation, o.SentRot) > 1f;
          if (moved) o.RestSends = 3;
          if (moved || o.RestSends > 0)
          {
            if (!moved) o.RestSends--;
            o.SentPos = o.T.position; o.SentRot = o.T.rotation;
            SbNet.QueueMove(o);
          }
          // fell out of the world
          if (o.T.position.y < -200f) Remove(o, true, false);
        }
      }
    }

    private bool Touching(SbObject o, Transform hand)
    {
      if (hand == null || o.T == null) return false;
      Vector3 lp = o.T.InverseTransformPoint(hand.position);
      Bounds b = o.IsCell ? new Bounds(Vector3.zero, Vector3.one) : o.LocalBounds;
      float pad = 0.04f / Mathf.Max(0.01f, o.T.lossyScale.x);
      b.Expand(pad * 2f);
      return b.Contains(lp);
    }

    private bool LandmineTriggered(SbObject o)
    {
      Vector3 c = o.Center;
      float r = 0.6f * Mathf.Max(0.5f, o.Scale);
      try
      {
        GTPlayer p = GTPlayer.Instance;
        if (p != null && (p.bodyCollider.transform.position - c).sqrMagnitude < (r + 0.4f) * (r + 0.4f) && Time.time - o.SpawnTime > 4f) return true;
      }
      catch { }
      foreach (VRRig rig in Rigs())
        if (rig != null && !rig.isOfflineVRRig && (rig.transform.position - c).sqrMagnitude < (r + 0.3f) * (r + 0.3f)) return true;
      foreach (SbObject other in List)
        if (other != o && other.Rb != null && !other.Rb.isKinematic && (other.T.position - c).sqrMagnitude < r * r) return true;
      return false;
    }

    // ================================================================ priming / blasts
    internal void Prime(SbObject o, float fuse, bool broadcast)
    {
      if (o == null || o.Primed) return;
      o.Primed = true;
      o.FuseLeft = o.FuseTotal = Mathf.Max(0.02f, fuse);
      if (o.IsCell && o.Item.Kind == SbKind.Block)
      {
        RemoveFromGrid(o);
        ScheduleFluidAround(o.SizeIdx, o.Cell);
        CheckFallingAbove(o.SizeIdx, o.Cell);
      }
      if (o.Item.Blast == SbBlast.Nuke)
      {
        SbFx.Play(SbSound.Siren, o.Center, 1f, 1f, 200f);
        CreateCountdown(o);
      }
      else if (o.Item.BlockFx == SbBlockFx.Tnt) SbFx.PlayMc("fuse", SbSound.Fuse, o.Center, 1f);
      else if (fuse > 0.3f) SbFx.Play(SbSound.Fuse, o.Center, 0.8f);
      if (o.Item.Blast == SbBlast.Firework) o.LaunchT = 0f;
      if (broadcast && o.Mine) SbNet.SendPrime(o.Id, fuse);
    }

    private void CreateCountdown(SbObject o)
    {
      try
      {
        var go = new GameObject("countdown");
        go.transform.SetParent(objRoot, false);
        var tmp = go.AddComponent<TMPro.TextMeshPro>();
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.fontSize = 3f;
        tmp.color = new Color(1f, 0.25f, 0.2f);
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.text = "10";
        o.Countdown = tmp;
      }
      catch { }
    }

    private void UpdatePrimed(SbObject o, float dt)
    {
      o.FuseLeft -= dt;
      float k = 1f - Mathf.Clamp01(o.FuseLeft / Mathf.Max(0.01f, o.FuseTotal));
      // flash white / swell like Minecraft TNT
      if (o.T != null && o.Item.Kind != SbKind.Fluid)
      {
        float baseScale = o.IsCell ? o.BlockSize : o.Scale;
        float swell = o.Item.BlockFx == SbBlockFx.Tnt ? 1f + Mathf.Max(0f, k - 0.75f) * 0.6f : 1f;
        o.T.localScale = Vector3.one * baseScale * swell;
        var r = o.Go.GetComponent<MeshRenderer>();
        bool flash = ((int)(Time.time * (4f + k * 10f))) % 2 == 0;
        if (r != null && r.enabled != (!flash || k < 0.05f)) r.enabled = !flash || k < 0.05f || o.Item.Blast == SbBlast.Nuke;
      }
      if (o.Item.Blast == SbBlast.Nuke)
      {
        if (o.Countdown != null)
        {
          Transform head = HeadProvider != null ? HeadProvider() : null;
          o.Countdown.transform.position = o.Center + Vector3.up * (0.6f * o.Scale + 0.4f);
          if (head != null) o.Countdown.transform.rotation = Quaternion.LookRotation(o.Countdown.transform.position - head.position);
          o.Countdown.text = Mathf.CeilToInt(Mathf.Max(0f, o.FuseLeft)).ToString();
        }
        float beepEvery = Mathf.Lerp(1f, 0.12f, k);
        if (Time.time - o.LastBeep > beepEvery) { o.LastBeep = Time.time; SbFx.Play(SbSound.Beep, o.Center, 0.9f, 1f + k * 0.4f, 120f); }
      }
      if (o.Item.Blast == SbBlast.Firework && o.LaunchT >= 0f && o.T != null)
      {
        o.LaunchT += dt;
        if (o.LaunchT > 0.4f)
        {
          if (o.Rb != null) o.Rb.isKinematic = true;
          o.T.position += Vector3.up * 16f * dt;
          if (UnityEngine.Random.value < 0.6f) SbFx.Burst(o.T.position, 2, new Color(1f, 0.8f, 0.3f), Color.white, 0.2f, 1f, 0.03f, 0.06f, 0.3f, 0.6f, 0.3f, true);
        }
      }
      if (o.FuseLeft <= 0f && o.Mine) Detonate(o);
    }

    private void Detonate(SbObject o)
    {
      Vector3 c = o.Center;
      SbItem it = o.Item;
      float r = it.BlockFx == SbBlockFx.Tnt ? Mathf.Max(1.2f, o.BlockSize * 4f) : it.Radius * Mathf.Max(0.35f, o.Scale);
      SbBlast kind = it.BlockFx == SbBlockFx.Tnt ? SbBlast.Tnt : it.Blast;
      if (o.Countdown != null) Destroy(o.Countdown.gameObject);
      Remove(o, true, false);
      Explode(c, r, kind, true);
      if (kind == SbBlast.Cluster)
        for (int i = 0; i < 5; i++)
        {
          Vector3 off = UnityEngine.Random.insideUnitSphere * r * 1.3f; off.y = Mathf.Abs(off.y) * 0.5f;
          pendingBlasts.Add(new PendingBlast { at = Time.time + 0.25f + i * 0.12f, pos = c + off, radius = r * 0.7f, kind = SbBlast.Normal });
        }
    }

    private struct PendingBlast { public float at; public Vector3 pos; public float radius; public SbBlast kind; }
    private readonly List<PendingBlast> pendingBlasts = new List<PendingBlast>();

    // Local blast: effects + my objects + me. broadcast = it started here.
    internal void Explode(Vector3 pos, float radius, SbBlast kind, bool broadcast)
    {
      if (broadcast) SbNet.SendBlast(pos, radius, kind);
      // a smoke grenade and a flashbang hurt nothing (SandBoxMelee.cs)
      if (kind == SbBlast.Smoke) { SmokeCloud(pos, radius); return; }
      if (kind == SbBlast.Flash) { Flashbang(pos, radius); return; }
      if (kind == SbBlast.BlackHole)
      {
        var go = new GameObject("SB Black Hole");
        go.transform.SetParent(SbFx.Root, false);
        go.transform.position = pos;
        var bh = go.AddComponent<SbBlackHole>();
        bh.radius = radius;
        return;
      }
      SbFx.Explosion(pos, radius, kind);
      ApplyBlastToMine(pos, radius, kind);
      KnockLocalPlayer(pos, radius, kind == SbBlast.Nuke ? 2.2f : kind == SbBlast.MiniNuke ? 1.6f : kind == SbBlast.Firework ? 0f : 1f);
    }

    internal void ApplyBlastToMine(Vector3 pos, float radius, SbBlast kind)
    {
      bool nuke = kind == SbBlast.Nuke || kind == SbBlast.MiniNuke;
      var copy = new List<SbObject>(List);
      int debris = 0;
      foreach (SbObject o in copy)
      {
        if (!(o.Mine || o.Generated) || o.Held) continue;
        if (!objects.ContainsKey(o.Key)) continue;
        Vector3 c = o.Center;
        float d = Vector3.Distance(c, pos);
        if (d > radius) continue;
        SbItem it = o.Item;
        if (it.Kind == SbKind.Fluid) { if (nuke && d < radius * 0.5f) Remove(o, true, false); continue; }
        if (o.Mob != null)
        {
          o.Mob.Damage(Mathf.Lerp(30f, 4f, d / radius) * (nuke ? 10f : 1f), c);
          if (!objects.ContainsKey(o.Key)) continue;
        }
        if (o.IsCell && it.Kind == SbKind.Block)
        {
          if (o.Primed) continue;
          if ((it.Id == "mc_obsidian" || it.Id == "mc_bedrock") && !nuke) continue;
          if (it.BlockFx == SbBlockFx.Tnt) { Prime(o, 0.5f + UnityEngine.Random.value, true); continue; }
          if (d < radius * 0.8f || UnityEngine.Random.value < 0.5f)
          {
            bool fx = debris < 24;
            if (fx) debris++;
            Remove(o, true, fx);
          }
          continue;
        }
        if (it.Kind == SbKind.Explosive && !o.Primed) { Prime(o, 0.08f + UnityEngine.Random.value * 0.3f, true); continue; }
        if (nuke && d < radius * 0.45f) { Remove(o, true, debris++ < 24); continue; }
        if (o.Anchored && d < radius * 0.6f && o.Mine) { MakeDynamic(o, Vector3.zero); SbNet.QueueSpawn(o); }
        if (o.Rb != null && !o.Rb.isKinematic)
        {
          float force = (nuke ? 40f : 12f) * Mathf.Clamp01(1f - d / radius) * o.Rb.mass + 1f;
          o.Rb.AddExplosionForce(force, pos, radius, 0.6f, ForceMode.Impulse);
          if (o.Item.Ragdoll && o.Go != null)
            foreach (Rigidbody limb in o.Go.GetComponentsInChildren<Rigidbody>())
              if (limb != o.Rb && !limb.isKinematic) limb.AddExplosionForce(force * 0.3f, pos, radius, 0.6f, ForceMode.Impulse);
        }
      }
    }

    internal void KnockLocalPlayer(Vector3 pos, float radius, float strength)
    {
      strength *= 1f - ArmorProtection;   // armour (SandBoxItems.cs)
      if (strength <= 0f || !SolidAllowed) return;
      try
      {
        GTPlayer p = GTPlayer.Instance;
        if (p == null) return;
        Vector3 body = p.bodyCollider.transform.position;
        Vector3 d = body - pos;
        float dist = d.magnitude;
        if (dist > radius * 1.2f) return;
        float k = Mathf.Clamp01(1f - dist / (radius * 1.2f));
        Vector3 dir = (dist > 0.01f ? d / dist : Vector3.up) + Vector3.up * 0.35f;
        float speed = Mathf.Min(28f, (4f + radius * 2.2f) * k * strength);
        p.ApplyKnockback(dir.normalized, speed, true);
        Haptic?.Invoke(true, 0.8f * k, 0.25f);
        Haptic?.Invoke(false, 0.8f * k, 0.25f);
      }
      catch { }
    }

    internal void ApplyHitToMine(SbObject o, Vector3 impulse, Vector3 point, bool ignite)
    {
      if (o == null || !(o.Mine || o.Generated)) return;
      if (o.Mob != null) o.Mob.Hurt(impulse, point);
      if (ignite && (o.Item.Kind == SbKind.Explosive || o.Item.BlockFx == SbBlockFx.Tnt) && !o.Primed && !o.Held)
      {
        Prime(o, o.Item.BlockFx == SbBlockFx.Tnt ? 0.6f + UnityEngine.Random.value * 0.6f : (o.Item.Blast == SbBlast.Nuke ? o.Item.Fuse : 0.05f), true);
        return;
      }
      if (o.Rb != null && !o.Rb.isKinematic) o.Rb.AddForceAtPosition(impulse, point, ForceMode.Impulse);
    }

    internal void OnObjectCollision(SbObject o, Collision c)
    {
      if (o == null || !o.Mine || o.Held) return;
      if (o.Item.Pearl) { PearlHit(o, c); return; }
      if (o.Item.Blast == SbBlast.Sticky && o.Primed && !o.Stuck && o.Rb != null)
      {
        o.Stuck = true;
        o.Rb.isKinematic = true;
        o.Rb.linearVelocity = Vector3.zero;
        SbFx.Play(SbSound.Splat, o.T.position, 0.6f, 1.5f);
        SbNet.QueueMove(o);
      }
      if (c.relativeVelocity.magnitude > 3f && o.Item.PropFx == SbPropFx.Bouncy && UnityEngine.Random.value < 0.6f)
        SbFx.Play(SbSound.Thunk, o.T.position, Mathf.Clamp01(c.relativeVelocity.magnitude / 10f), 1.4f);
      if (o.Item.Id == "ob_duck" && c.relativeVelocity.magnitude > 2f) SbFx.Play(SbSound.Squeak, o.T.position, 0.5f);
    }

    // ================================================================ remote rigs
    private VRRig[] rigCache = new VRRig[0];
    private float rigCacheAt;
    internal VRRig[] Rigs()
    {
      if (Time.unscaledTime - rigCacheAt > 2f)
      {
        rigCacheAt = Time.unscaledTime;
        try { rigCache = FindObjectsByType<VRRig>(FindObjectsSortMode.None); } catch { rigCache = new VRRig[0]; }
      }
      return rigCache;
    }

    internal VRRig RigOf(int actor)
    {
      foreach (VRRig r in Rigs())
      {
        if (r == null || r.isOfflineVRRig) continue;
        try { if (r.Creator != null && r.Creator.ActorNumber == actor) return r; } catch { }
      }
      return null;
    }

    internal static int ActorOf(VRRig r)
    {
      try { return r != null && r.Creator != null ? r.Creator.ActorNumber : -1; } catch { return -1; }
    }

    private void FollowRemoteHand(SbObject o)
    {
      VRRig rig = RigOf(o.Owner);
      if (rig == null) return;
      Transform bone = o.HeldRight ? rig.rightHandTransform : rig.leftHandTransform;
      if (bone == null) return;
      o.T.position = bone.TransformPoint(o.HoldLocalPos);
      o.T.rotation = bone.rotation * o.HoldLocalRot;
    }

    // ================================================================ network in
    internal void NetSpawn(int actor, SbNet.Rec r)
    {
      SbItem item;
      if (!SbCatalog.ByKey.TryGetValue(r.key, out item)) return;
      SbObject o = Create(actor, r.id, item, r.pos, r.rot, r.scale, (r.flags & 1) != 0, r.sizeIdx, r.cell);
      if (o == null) return;
      o.NetPos = r.pos; o.NetRot = r.rot; o.NetTime = Time.unscaledTime; o.HasNet = !o.Anchored;
      if ((r.flags & 2) != 0) Prime(o, 3f, false);
    }

    internal void NetRemove(int actor, int id)
    {
      SbObject o;
      if (!objects.TryGetValue(((long)actor << 32) | (uint)id, out o)) return;
      PopOutNext = true;
      try { Remove(o, false, o.IsCell); }
      finally { PopOutNext = false; }
    }

    internal void NetClear(int actor)
    {
      var copy = new List<SbObject>(List);
      foreach (SbObject o in copy) if (o.Owner == actor) Remove(o, false, false);
    }

    internal void NetMove(int actor, int id, Vector3 pos, Quaternion rot, Vector3 vel)
    {
      SbObject o;
      if (!objects.TryGetValue(((long)actor << 32) | (uint)id, out o) || o.T == null) return;
      if (o.Anchored) { o.Anchored = false; if (o.Go != null && o.Rb == null) MakeDynamic(o, Vector3.zero); }
      o.NetPos = pos; o.NetRot = rot; o.NetVel = vel; o.NetTime = Time.unscaledTime; o.HasNet = true;
    }

    internal void NetHold(int actor, int id, bool right, bool holding, Vector3 lp, Quaternion lr)
    {
      SbObject o;
      if (!objects.TryGetValue(((long)actor << 32) | (uint)id, out o) || o.T == null) return;
      o.HeldRemote = holding; o.HeldRight = right; o.HoldLocalPos = lp; o.HoldLocalRot = lr;
      if (!holding) { o.NetPos = o.T.position; o.NetRot = o.T.rotation; o.NetTime = Time.unscaledTime; }
      ApplySolid(o);
    }

    internal void NetPrime(int actor, int id, float fuse)
    {
      SbObject o;
      if (objects.TryGetValue(((long)actor << 32) | (uint)id, out o)) Prime(o, fuse + 1f, false); // owner's blast removes it
    }

    internal void NetHitObject(int id, Vector3 impulse, Vector3 point, bool ignite)
    {
      SbObject o;
      if (objects.TryGetValue(id, out o)) ApplyHitToMine(o, impulse, point, ignite);
    }

    internal void NetHitPlayer(Vector3 impulse)
    {
      if (!SolidAllowed) return;
      try
      {
        GTPlayer p = GTPlayer.Instance;
        if (p == null) return;
        float speed = Mathf.Min(impulse.magnitude, 16f);
        if (speed < 0.05f) return;
        p.ApplyKnockback(impulse.normalized, speed, true);
        Haptic?.Invoke(true, 0.6f, 0.12f);
        Haptic?.Invoke(false, 0.6f, 0.12f);
      }
      catch { }
    }

    internal void NetLeft(int actor) { NetClear(actor); ClearPortalsOf(actor); ClearArmorOf(actor); }

    internal void NetRoomChanged()
    {
      var copy = new List<SbObject>(List);
      foreach (SbObject o in copy) if (!o.Mine && !o.Generated) Remove(o, false, false);
      ResetTerrainEdits();
    }

    internal void ApplyShowOthers()
    {
      foreach (SbObject o in List)
      {
        if (o.Mine || o.Generated) continue;
        if (o.Go != null) o.Go.SetActive(ShowOthers);
        ApplySolid(o);
      }
      foreach (var fm in fluidMeshes.Values) fm.dirty = true;
    }

    internal IEnumerable<SbObject> Mine()
    {
      foreach (SbObject o in List) if (o.Mine) yield return o;
    }
  }

  // Pulls things in for 2.5 s, then pops.
  internal sealed class SbBlackHole : MonoBehaviour
  {
    internal float radius = 6f;
    private float t;
    private ParticleSystem swirl;
    private GameObject core;

    private void Start()
    {
      core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      var c = core.GetComponent<Collider>(); if (c != null) Destroy(c);
      core.transform.SetParent(transform, false);
      var r = core.GetComponent<Renderer>();
      var m = new Material(SbMats.Line) { hideFlags = HideFlags.DontSave, color = new Color(0.02f, 0f, 0.05f, 1f) };
      r.sharedMaterial = m;
      core.transform.localScale = Vector3.one * 0.05f;
      swirl = SbFx.Burst(transform.position, 1, Color.white, Color.white, 0f, 0f, 0.05f, 0.1f, 0.6f, 0.6f, 0f, true);
      if (swirl != null)
      {
        swirl.transform.SetParent(transform, true);
        var main = swirl.main;
        main.loop = true;
        main.stopAction = ParticleSystemStopAction.None;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.3f, 1f), new Color(0.3f, 0.9f, 1f));
        main.startSpeed = new ParticleSystem.MinMaxCurve(-radius * 1.2f, -radius * 0.6f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.maxParticles = 600;
        var sh = swirl.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius;
        var em = swirl.emission; em.rateOverTime = 260f;
        var vel = swirl.velocityOverLifetime; vel.enabled = true; vel.orbitalY = new ParticleSystem.MinMaxCurve(3f);
        swirl.Play();
      }
      SbFx.Play(SbSound.Suck, transform.position, 1f, 1f, 90f);
    }

    private void FixedUpdate()
    {
      SandBoxWorld w = SandBoxWorld.Instance;
      if (w == null) return;
      Vector3 c = transform.position;
      foreach (SbObject o in w.List)
      {
        if (!o.Mine || o.Rb == null || o.Rb.isKinematic || o.Held) continue;
        Vector3 d = c - o.T.position;
        float dist = d.magnitude;
        if (dist > radius * 1.6f || dist < 0.01f) continue;
        o.Rb.AddForce(d / dist * 18f * (1f - dist / (radius * 1.6f)), ForceMode.Acceleration);
      }
      if (w.SolidAllowed)
      {
        try
        {
          var p = GorillaLocomotion.GTPlayer.Instance;
          if (p != null)
          {
            Vector3 d = c - p.bodyCollider.transform.position;
            float dist = d.magnitude;
            if (dist < radius * 1.6f && dist > 0.3f) p.playerRigidBody.AddForce(d / dist * 14f * (1f - dist / (radius * 1.6f)), ForceMode.Acceleration);
          }
        }
        catch { }
      }
    }

    private void Update()
    {
      t += Time.deltaTime;
      if (core != null) core.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, radius * 0.35f, Mathf.Clamp01(t / 2.5f)) * (1f + 0.06f * Mathf.Sin(t * 30f));
      if (t >= 2.5f)
      {
        SandBoxWorld w = SandBoxWorld.Instance;
        SbFx.Explosion(transform.position, radius, SbBlast.BlackHole);
        if (w != null)
        {
          w.ApplyBlastToMine(transform.position, radius * 0.6f, SbBlast.Normal);
          w.KnockLocalPlayer(transform.position, radius, 0.8f);
        }
        Destroy(gameObject);
      }
    }
  }
}
