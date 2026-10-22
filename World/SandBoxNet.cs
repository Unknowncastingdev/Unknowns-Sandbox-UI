using System;
using System.Collections.Generic;
using System.IO;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace UnknownsSandbox
{
  // SandBox objects are shared only with other Unknowns clients, through one
  // Photon event code (193, unused by the game).
  // Every player owns what they placed: spawns/removals/blasts are reliable,
  // physics movement is unreliable at 10 Hz. A player joining gets everybody's
  // objects as a snapshot (Hello -> Snapshot, answered once each way per room).
  internal static class SbNet
  {
    private const byte Code = 193;
    private const string Magic = "UnkSB1";

    private enum Op : byte { Spawn = 1, Remove = 2, Clear = 3, Move = 4, Blast = 5, Shot = 6, HitPlayer = 7, HitObject = 8, Hello = 9, Hold = 10, Prime = 11, Snapshot = 12, Presence = 13, Attach = 14, Weld = 15, Terrain = 16, Break = 17, Portal = 18, Toggle = 19, Armor = 20 }

    internal struct Rec
    {
      public int id;
      public ushort key;
      public byte flags;    // 1 anchored, 2 primed, 4 grid cell
      public byte sizeIdx;
      public Vector3Int cell;
      public Vector3 pos;
      public Quaternion rot;
      public float scale;
    }

    private static bool subscribed;
    private static readonly HashSet<int> users = new HashSet<int>();
    private static readonly HashSet<int> snapshotSent = new HashSet<int>();
    private static string room = string.Empty;
    private static float helloAt = -1f;
    private static int helloTries;
    private static readonly List<SbObject> spawnOut = new List<SbObject>();
    private static readonly HashSet<SbObject> spawnSet = new HashSet<SbObject>();
    private static readonly List<int> removeOut = new List<int>();
    private static readonly List<SbObject> moveOut = new List<SbObject>();
    private static readonly HashSet<SbObject> moveSet = new HashSet<SbObject>();
    private static float nextFlush, nextRoster;

    private struct ShotRec { public ushort key; public Vector3 o, d; public int seed; public float scale; }
    private static readonly List<ShotRec> shotOut = new List<ShotRec>();
    private static readonly Dictionary<int, Vector3> hitPlayerOut = new Dictionary<int, Vector3>();
    private struct HitRec { public int owner, id; public Vector3 imp, pt; public bool ignite; }
    private static readonly Dictionary<long, HitRec> hitObjOut = new Dictionary<long, HitRec>();

    internal static bool IsSandBoxUser(int actor) { return users.Contains(actor); }
    internal static int UserCount { get { return users.Count; } }

    internal static void Init() { Subscribe(); }

    private static void Subscribe()
    {
      if (subscribed) return;
      try
      {
        if (PhotonNetwork.NetworkingClient == null) return;
        PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
        subscribed = true;
      }
      catch { }
    }

    internal static void Shutdown()
    {
      try { if (subscribed && PhotonNetwork.NetworkingClient != null) PhotonNetwork.NetworkingClient.EventReceived -= OnEvent; } catch { }
      subscribed = false;
    }

    private static int LocalActor
    {
      get { try { return PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1; } catch { return -1; } }
    }

    // ---------------------------------------------------------------- outgoing
    internal static void QueueSpawn(SbObject o)
    {
      if (o == null || !o.Mine || !PhotonNetwork.InRoom) return;
      if (spawnSet.Add(o)) spawnOut.Add(o);
    }

    internal static void QueueRemove(int id)
    {
      if (!PhotonNetwork.InRoom) return;
      removeOut.Add(id);
    }

    // you broke another player's block (owner, id): everyone drops it, its owner
    // breaks it for good
    internal static void SendBreak(int owner, int id)
    {
      if (!PhotonNetwork.InRoom) return;
      Send(Op.Break, Build(w => { w.Write(owner); w.Write(id); }), null, true);
    }

    // one end of your portal-gun pair opened (or closed)
    internal static void SendPortal(int kind, int slot, bool on, Vector3 pos, Vector3 normal, Vector3 up)
    {
      if (!PhotonNetwork.InRoom) return;
      Send(Op.Portal, Build(w => { w.Write((byte)kind); w.Write((byte)slot); w.Write(on); WV(w, pos); WV(w, normal); WV(w, up); }), null, true);
    }

    // a lever / door flipped, a button pressed or a note block tuned (owner 0 = yours)
    internal static void SendToggle(int owner, int id, bool on, int note = 0)
    {
      if (!PhotonNetwork.InRoom) return;
      int actor = owner == 0 ? LocalActor : owner;
      Send(Op.Toggle, Build(w => { w.Write(actor); w.Write(id); w.Write(on); w.Write((byte)note); }), null, true);
    }

    // what you wear (slot 0 helmet, 1 chestplate / elytra; key 0 = nothing)
    internal static void SendArmor(int slot, ushort key)
    {
      if (!PhotonNetwork.InRoom) return;
      Send(Op.Armor, Build(w => { w.Write((byte)slot); w.Write(key); }), null, true);
    }

    internal static void QueueMove(SbObject o)
    {
      if (o == null || !o.Mine || !PhotonNetwork.InRoom) return;
      if (moveSet.Add(o)) moveOut.Add(o);
    }

    internal static void SendClear()
    {
      spawnOut.Clear(); spawnSet.Clear(); removeOut.Clear(); moveOut.Clear(); moveSet.Clear();
      Send(Op.Clear, new byte[0], null, true);
    }

    internal static void SendBlast(Vector3 pos, float radius, SbBlast kind)
    {
      Send(Op.Blast, Build(w => { WV(w, pos); w.Write(radius); w.Write((byte)kind); }), null, true);
    }

    // Shots and hits are gathered and sent with the 0.1 s flush, so a minigun
    // or flamethrower is still at most ten events a second.
    internal static void SendShot(ushort key, Vector3 origin, Vector3 dir, int seed, float scale)
    {
      if (!PhotonNetwork.InRoom || shotOut.Count >= 40) return;
      shotOut.Add(new ShotRec { key = key, o = origin, d = dir, seed = seed, scale = scale });
    }

    internal static void SendHitPlayer(int actor, Vector3 impulse)
    {
      if (!PhotonNetwork.InRoom) return;
      Vector3 cur;
      hitPlayerOut.TryGetValue(actor, out cur);
      hitPlayerOut[actor] = Vector3.ClampMagnitude(cur + impulse, 16f);
    }

    internal static void SendHitObject(int owner, int id, Vector3 impulse, Vector3 point, bool ignite)
    {
      if (!PhotonNetwork.InRoom) return;
      long k = ((long)owner << 32) | (uint)id;
      HitRec h;
      if (hitObjOut.TryGetValue(k, out h)) { h.imp = Vector3.ClampMagnitude(h.imp + impulse, 30f); h.ignite |= ignite; h.pt = point; }
      else h = new HitRec { owner = owner, id = id, imp = impulse, pt = point, ignite = ignite };
      hitObjOut[k] = h;
    }

    private static void FlushShotsAndHits()
    {
      if (shotOut.Count > 0)
      {
        var list = new List<ShotRec>(shotOut);
        shotOut.Clear();
        Send(Op.Shot, Build(w =>
        {
          w.Write((ushort)list.Count);
          foreach (ShotRec s in list) { w.Write(s.key); WV(w, s.o); WV(w, s.d); w.Write(s.seed); w.Write(s.scale); }
        }), null, false);
      }
      if (hitPlayerOut.Count > 0)
      {
        foreach (var kv in hitPlayerOut)
        {
          int actor = kv.Key; Vector3 imp = kv.Value;
          Send(Op.HitPlayer, Build(w => { w.Write(actor); WV(w, imp); }), new[] { actor }, true);
        }
        hitPlayerOut.Clear();
      }
      if (hitObjOut.Count > 0)
      {
        var byOwner = new Dictionary<int, List<HitRec>>();
        foreach (HitRec h in hitObjOut.Values)
        {
          List<HitRec> l;
          if (!byOwner.TryGetValue(h.owner, out l)) { l = new List<HitRec>(); byOwner[h.owner] = l; }
          l.Add(h);
        }
        hitObjOut.Clear();
        foreach (var kv in byOwner)
        {
          int owner = kv.Key; List<HitRec> l = kv.Value;
          Send(Op.HitObject, Build(w =>
          {
            w.Write(owner);
            w.Write((ushort)l.Count);
            foreach (HitRec h in l) { w.Write(h.id); WV(w, h.imp); WV(w, h.pt); w.Write(h.ignite); }
          }), new[] { owner }, true);
        }
      }
    }

    internal static void SendHold(int id, bool right, bool holding, Vector3 lp, Quaternion lr)
    {
      FlushSpawns();
      Send(Op.Hold, Build(w => { w.Write(id); w.Write(right); w.Write(holding); WV(w, lp); WQ(w, lr); }), null, true);
    }

    // a thruster / wheel / balloon / fire stuck onto another of your objects (and its on / off)
    internal static void SendAttach(int id, int parentId, Vector3 lp, Quaternion lr, bool on, int[] targets = null)
    {
      FlushSpawns();
      Send(Op.Attach, Build(w => { w.Write(id); w.Write(parentId); WV(w, lp); WQ(w, lr); w.Write(on); }), targets, true);
    }

    // dimension blocks broken / placed (already packed by SbVoxelDim)
    internal static void SendTerrain(byte[] data)
    {
      if (data != null) Send(Op.Terrain, data, null, true);
    }

    // an object welded onto another (parentId -1: welded off again, lp / lr then in the world)
    internal static void SendWeld(int id, int parentId, Vector3 lp, Quaternion lr)
    {
      FlushSpawns();
      Send(Op.Weld, Build(w => { w.Write(id); w.Write(parentId); WV(w, lp); WQ(w, lr); }), null, true);
    }

    // your tablet / pointer / preview for the others to see (SbPresence), unreliable ~12 Hz
    internal static void SendPresence(byte[] data)
    {
      if (data != null) Send(Op.Presence, data, null, false);
    }

    internal static void SendPrime(int id, float fuse)
    {
      FlushSpawns();
      Send(Op.Prime, Build(w => { w.Write(id); w.Write(fuse); }), null, true);
    }

    private static void Send(Op op, byte[] data, int[] targets, bool reliable)
    {
      if (!PhotonNetwork.InRoom) return;
      try
      {
        object[] payload = { Magic, (byte)op, data };
        var opts = targets == null ? new RaiseEventOptions { Receivers = ReceiverGroup.Others } : new RaiseEventOptions { TargetActors = targets };
        PhotonNetwork.RaiseEvent(Code, payload, opts, reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable);
      }
      catch (Exception ex) { SbLog.Warn("send " + op + ": " + ex.Message); }
    }

    private static byte[] Build(Action<BinaryWriter> f)
    {
      using (var ms = new MemoryStream())
      using (var w = new BinaryWriter(ms))
      {
        f(w);
        w.Flush();
        return ms.ToArray();
      }
    }

    internal static void WV(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
    internal static void WQ(BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
    internal static Vector3 RV(BinaryReader r) { return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); }
    internal static Quaternion RQ(BinaryReader r)
    {
      var q = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
      float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
      return m > 1e-5f ? new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m) : Quaternion.identity;
    }

    private static Rec ToRec(SbObject o)
    {
      var r = new Rec { id = o.Id, key = o.Item.Key, sizeIdx = (byte)o.SizeIdx, cell = o.Cell, scale = o.Scale };
      if (o.Anchored) r.flags |= 1;
      if (o.Primed) r.flags |= 2;
      if (o.IsCell) r.flags |= 4;
      if (o.T != null) { r.pos = o.T.position; r.rot = o.T.rotation; } else { r.pos = o.Center; r.rot = Quaternion.identity; }
      return r;
    }

    private static void WriteRec(BinaryWriter w, Rec r)
    {
      w.Write(r.id); w.Write(r.key); w.Write(r.flags); w.Write(r.sizeIdx);
      if ((r.flags & 4) != 0)
      {
        w.Write(r.cell.x); w.Write(r.cell.y); w.Write(r.cell.z);
        w.Write((byte)(Mathf.RoundToInt(r.rot.eulerAngles.y / 90f) & 3));
      }
      else { WV(w, r.pos); WQ(w, r.rot); w.Write(r.scale); }
    }

    private static Rec ReadRec(BinaryReader rd)
    {
      var r = new Rec { id = rd.ReadInt32(), key = rd.ReadUInt16(), flags = rd.ReadByte(), sizeIdx = rd.ReadByte(), scale = 1f };
      if ((r.flags & 4) != 0)
      {
        r.cell = new Vector3Int(rd.ReadInt32(), rd.ReadInt32(), rd.ReadInt32());
        r.rot = Quaternion.Euler(0f, rd.ReadByte() * 90f, 0f);
        float s = SandBoxWorld.BlockSizes[Mathf.Clamp(r.sizeIdx, 0, SandBoxWorld.BlockSizes.Length - 1)];
        r.pos = SandBoxWorld.CellCenter(r.cell, s);
      }
      else { r.pos = RV(rd); r.rot = RQ(rd); r.scale = Mathf.Clamp(rd.ReadSingle(), 0.05f, 20f); }
      return r;
    }

    // ---------------------------------------------------------------- pacing
    // A snapshot goes out one small packet at a time (one burst would freeze the other
    // end and can get you kicked for flooding), and everything received is built a few
    // objects a frame, popping in, in the order it arrived.
    private struct Pending { public Op op; public byte[] data; public int[] targets; }
    private static readonly Queue<Pending> slowOut = new Queue<Pending>();
    private static float nextSlowOut;
    private const float SlowOutGap = 0.12f;      // ~8 snapshot packets a second
    private const int SnapshotChunk = 50;

    private struct Incoming { public Action run; public bool create; public int actor; }
    private static readonly Queue<Incoming> inbox = new Queue<Incoming>();
    private const int CreatesPerFrame = 5;

    private static void SendSlow(Op op, byte[] data, int[] targets)
    {
      slowOut.Enqueue(new Pending { op = op, data = data, targets = targets });
    }

    private static void Later(int actor, Action run, bool create)
    {
      inbox.Enqueue(new Incoming { run = run, create = create, actor = actor });
    }

    // a player left: what they sent that is still waiting is dropped (their objects go too)
    private static void DropInbox(int actor)
    {
      if (inbox.Count == 0) return;
      var keep = new List<Incoming>(inbox);
      inbox.Clear();
      foreach (Incoming i in keep) if (i.actor != actor) inbox.Enqueue(i);
    }

    private static void PumpQueues()
    {
      // outgoing snapshot pieces
      if (slowOut.Count > 0 && Time.unscaledTime >= nextSlowOut)
      {
        nextSlowOut = Time.unscaledTime + SlowOutGap;
        Pending p = slowOut.Dequeue();
        Send(p.op, p.data, p.targets, true);
      }
      // incoming builds: a few objects a frame, within ~4 ms
      if (inbox.Count == 0) return;
      int creates = 0;
      var sw = System.Diagnostics.Stopwatch.StartNew();
      while (inbox.Count > 0 && creates < CreatesPerFrame && sw.Elapsed.TotalMilliseconds < 4.0)
      {
        Incoming i = inbox.Dequeue();
        if (i.create) creates++;
        try { i.run(); } catch (Exception ex) { SbLog.Warn("queued: " + ex.Message); }
      }
    }

    private static void SendRecs(Op op, List<Rec> recs, int[] targets)
    {
      // snapshots go out paced (SendSlow); live spawns are small and go now
      bool slow = op == Op.Snapshot;
      int chunk = slow ? SnapshotChunk : 120;
      if (recs.Count == 0 && op == Op.Snapshot)
      {
        SendSlow(op, Build(w => { w.Write(true); w.Write((ushort)0); }), targets);
        return;
      }
      for (int i = 0; i < recs.Count; i += chunk)
      {
        int start = i, n = Mathf.Min(chunk, recs.Count - i);
        byte[] data = Build(w =>
        {
          w.Write(start == 0);
          w.Write((ushort)n);
          for (int k = 0; k < n; k++) WriteRec(w, recs[start + k]);
        });
        if (slow) SendSlow(op, data, targets); else Send(op, data, targets, true);
      }
    }

    private static void FlushSpawns()
    {
      if (spawnOut.Count == 0) return;
      var recs = new List<Rec>(spawnOut.Count);
      foreach (SbObject o in spawnOut) if (o != null && (o.T != null || o.Item.Kind == SbKind.Fluid)) recs.Add(ToRec(o));
      spawnOut.Clear(); spawnSet.Clear();
      SendRecs(Op.Spawn, recs, null);
    }

    private static void SendSnapshot(int actor)
    {
      SandBoxWorld world = SandBoxWorld.Instance;
      if (world == null) return;
      snapshotSent.Add(actor);
      world.ResendMyPortals();
      world.ResendMyArmor();
      var recs = new List<Rec>();
      var held = new List<SbObject>();
      foreach (SbObject o in world.Mine())
      {
        if (o.T == null && o.Item.Kind != SbKind.Fluid) continue;
        recs.Add(ToRec(o));
        if (o.Held) held.Add(o);
      }
      SendRecs(Op.Snapshot, recs, new[] { actor });
      // what is stuck to what (after the objects, in the same paced line)
      foreach (SbObject o in world.MyAttachments())
      {
        int id = o.Id, pid = o.Parent.Id; Vector3 lp = o.T.localPosition; Quaternion lr = o.AttachRot; bool on = o.On;
        if (o.Welded) SendSlow(Op.Weld, Build(w => { w.Write(id); w.Write(pid); WV(w, lp); WQ(w, o.T.localRotation); }), new[] { actor });
        else SendSlow(Op.Attach, Build(w => { w.Write(id); w.Write(pid); WV(w, lp); WQ(w, lr); w.Write(on); }), new[] { actor });
      }
      // the dimension blocks you broke or placed (SandBoxVoxels.cs)
      foreach (byte[] edits in world.MyTerrainEdits()) SendSlow(Op.Terrain, edits, new[] { actor });
      foreach (SbObject o in held)
      {
        try
        {
          VRRig me = GorillaTagger.Instance != null ? GorillaTagger.Instance.offlineVRRig : null;
          Transform bone = me != null ? (o.HeldRight ? me.rightHandTransform : me.leftHandTransform) : null;
          if (bone == null) continue;
          Vector3 lp = bone.InverseTransformPoint(o.T.position);
          Quaternion lr = Quaternion.Inverse(bone.rotation) * o.T.rotation;
          SendSlow(Op.Hold, Build(w => { w.Write(o.Id); w.Write(o.HeldRight); w.Write(true); WV(w, lp); WQ(w, lr); }), new[] { actor });
        }
        catch { }
      }
    }

    internal static void Tick(SandBoxWorld world)
    {
      Subscribe();
      string r = string.Empty;
      try { r = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name ?? string.Empty : string.Empty; } catch { }
      if (r != room)
      {
        room = r;
        users.Clear();
        snapshotSent.Clear();
        spawnOut.Clear(); spawnSet.Clear(); removeOut.Clear(); moveOut.Clear(); moveSet.Clear();
        shotOut.Clear(); hitPlayerOut.Clear(); hitObjOut.Clear();
        slowOut.Clear(); inbox.Clear();
        world.NetRoomChanged();
        helloAt = string.IsNullOrEmpty(r) ? -1f : Time.unscaledTime + 1.5f;
        helloTries = 0;
      }
      if (string.IsNullOrEmpty(room)) return;
      PumpQueues();
      if (helloAt > 0f && Time.unscaledTime >= helloAt)
      {
        // asked twice (the first can go out while the others are still loading the room)
        helloAt = ++helloTries < 2 ? Time.unscaledTime + 6f : -1f;
        Send(Op.Hello, new byte[0], null, true);
      }
      if (Time.unscaledTime >= nextRoster)
      {
        nextRoster = Time.unscaledTime + 1f;
        try
        {
          var present = new HashSet<int>();
          foreach (Player p in PhotonNetwork.PlayerListOthers) if (p != null) present.Add(p.ActorNumber);
          var gone = new List<int>();
          foreach (int a in users) if (!present.Contains(a)) gone.Add(a);
          foreach (int a in gone) { users.Remove(a); snapshotSent.Remove(a); DropInbox(a); world.NetLeft(a); }
        }
        catch { }
      }
      if (Time.unscaledTime < nextFlush) return;
      nextFlush = Time.unscaledTime + 0.1f;
      FlushSpawns();
      FlushShotsAndHits();
      if (removeOut.Count > 0)
      {
        var ids = new List<int>(removeOut);
        removeOut.Clear();
        for (int i = 0; i < ids.Count; i += 400)
        {
          int start = i, n = Mathf.Min(400, ids.Count - i);
          Send(Op.Remove, Build(w => { w.Write((ushort)n); for (int k = 0; k < n; k++) w.Write(ids[start + k]); }), null, true);
        }
      }
      if (moveOut.Count > 0)
      {
        var list = new List<SbObject>(moveOut);
        moveOut.Clear(); moveSet.Clear();
        list.RemoveAll(o => o == null || o.T == null);
        for (int i = 0; i < list.Count; i += 60)
        {
          int start = i, n = Mathf.Min(60, list.Count - i);
          Send(Op.Move, Build(w =>
          {
            w.Write((ushort)n);
            for (int k = 0; k < n; k++)
            {
              SbObject o = list[start + k];
              w.Write(o.Id); WV(w, o.T.position); WQ(w, o.T.rotation);
              WV(w, o.Rb != null && !o.Rb.isKinematic ? o.Rb.linearVelocity : Vector3.zero);
            }
          }), null, false);
        }
      }
    }

    // ---------------------------------------------------------------- incoming
    private static void OnEvent(EventData e)
    {
      if (e == null || e.Code != Code) return;
      try
      {
        object[] d = e.CustomData as object[];
        if (d == null || d.Length < 3 || !string.Equals(d[0] as string, Magic, StringComparison.Ordinal)) return;
        Op op = (Op)Convert.ToByte(d[1]);
        byte[] data = d[2] as byte[] ?? new byte[0];
        int actor = e.Sender;
        if (actor <= 0 || actor == LocalActor) return;
        users.Add(actor);
        // the catalog is needed to build what others send, tablet open or not
        SbCatalog.Init();
        SandBoxWorld world = SandBoxWorld.Instance;
        if (world == null) return;
        // "See Other Players' SandBox" off: their objects are kept (hidden) so turning it back
        // on shows them again, but their blasts, shots and hits don't reach you at all
        if (!SandBoxWorld.ShowOthers && (op == Op.Blast || op == Op.Shot || op == Op.HitPlayer || op == Op.HitObject)) return;
        using (var ms = new MemoryStream(data))
        using (var r = new BinaryReader(ms))
        {
          switch (op)
          {
            case Op.Hello:
              SendSnapshot(actor);
              break;
            case Op.Presence:
              SbPresence.Receive(actor, r);
              break;
            // builds, removals and links wait in line (PumpQueues) so they keep their order
            case Op.Attach:
              {
                int id = r.ReadInt32(), parent = r.ReadInt32(); Vector3 lp = RV(r); Quaternion lr = RQ(r); bool on = r.ReadBoolean();
                Later(actor, () => world.NetAttach(actor, id, parent, lp, lr, on), false);
                break;
              }
            case Op.Weld:
              {
                int id = r.ReadInt32(), parent = r.ReadInt32(); Vector3 lp = RV(r); Quaternion lr = RQ(r);
                Later(actor, () => world.NetWeld(actor, id, parent, lp, lr), false);
                break;
              }
            case Op.Terrain:
              {
                byte[] copy = data;
                Later(actor, () => world.NetTerrain(copy), false);
                break;
              }
            case Op.Snapshot:
            case Op.Spawn:
              {
                bool first = r.ReadBoolean();
                int n = r.ReadUInt16();
                if (op == Op.Snapshot && first) Later(actor, () => world.NetClear(actor), false);
                for (int i = 0; i < n; i++)
                {
                  Rec rec = ReadRec(r);
                  // everything pops in, a few a frame, so a big build streams in smoothly
                  Later(actor, () =>
                  {
                    SandBoxWorld.PopNext = true;
                    try { world.NetSpawn(actor, rec); }
                    finally { SandBoxWorld.PopNext = false; }
                  }, true);
                }
                if (op == Op.Snapshot && !snapshotSent.Contains(actor)) SendSnapshot(actor);
                break;
              }
            case Op.Remove:
              {
                int n = r.ReadUInt16();
                var ids = new int[n];
                for (int i = 0; i < n; i++) ids[i] = r.ReadInt32();
                Later(actor, () => { foreach (int id in ids) world.NetRemove(actor, id); }, false);
                break;
              }
            case Op.Armor:
              {
                int slot = r.ReadByte(); ushort key = r.ReadUInt16();
                if (slot >= 0 && slot < 2) world.NetArmor(actor, slot, key);
                break;
              }
            case Op.Toggle:
              {
                int owner = r.ReadInt32(), id = r.ReadInt32(); bool on = r.ReadBoolean(); int note = r.ReadByte();
                Later(actor, () => world.NetToggle(actor, owner, id, on, note), false);
                break;
              }
            case Op.Portal:
              {
                int kind = r.ReadByte(), slot = r.ReadByte(); bool on = r.ReadBoolean(); Vector3 p = RV(r), n = RV(r), u = RV(r);
                world.NetPortal(actor, kind, slot, on, p, n, u);
                break;
              }
            case Op.Break:
              {
                int owner = r.ReadInt32(), id = r.ReadInt32();
                if (owner == LocalActor) Later(actor, () => world.NetBreakMine(id), false);
                else Later(actor, () => world.NetRemove(owner, id), false);
                break;
              }
            case Op.Clear:
              Later(actor, () => world.NetClear(actor), false);
              break;
            case Op.Move:
              {
                int n = r.ReadUInt16();
                for (int i = 0; i < n; i++)
                {
                  int id = r.ReadInt32();
                  Vector3 p = RV(r); Quaternion q = RQ(r); Vector3 v = RV(r);
                  world.NetMove(actor, id, p, q, v);
                }
                break;
              }
            case Op.Blast:
              {
                Vector3 p = RV(r); float radius = Mathf.Clamp(r.ReadSingle(), 0.1f, 60f); SbBlast kind = (SbBlast)r.ReadByte();
                world.Explode(p, radius, kind, false);
                break;
              }
            case Op.Shot:
              {
                int n = Mathf.Min(r.ReadUInt16(), 60);
                for (int i = 0; i < n; i++)
                {
                  ushort key = r.ReadUInt16(); Vector3 o = RV(r); Vector3 dir = RV(r); int seed = r.ReadInt32(); float sc = r.ReadSingle();
                  world.NetShot(actor, key, o, dir.normalized, seed, Mathf.Clamp(sc, 0.1f, 5f));
                }
                break;
              }
            case Op.HitPlayer:
              {
                int target = r.ReadInt32(); Vector3 imp = RV(r);
                if (target == LocalActor) world.NetHitPlayer(Vector3.ClampMagnitude(imp, 16f));
                break;
              }
            case Op.HitObject:
              {
                int owner = r.ReadInt32();
                int n = r.ReadUInt16();
                for (int i = 0; i < n; i++)
                {
                  int id = r.ReadInt32(); Vector3 imp = RV(r); Vector3 pt = RV(r); bool ignite = r.ReadBoolean();
                  if (owner == LocalActor) world.NetHitObject(id, Vector3.ClampMagnitude(imp, 30f), pt, ignite);
                }
                break;
              }
            case Op.Hold:
              {
                int id = r.ReadInt32(); bool right = r.ReadBoolean(); bool holding = r.ReadBoolean(); Vector3 lp = RV(r); Quaternion lr = RQ(r);
                Later(actor, () => world.NetHold(actor, id, right, holding, Vector3.ClampMagnitude(lp, 1.5f), lr), false);
                break;
              }
            case Op.Prime:
              {
                int id = r.ReadInt32(); float fuse = r.ReadSingle();
                Later(actor, () => world.NetPrime(actor, id, Mathf.Clamp(fuse, 0f, 30f)), false);
                break;
              }
          }
        }
      }
      catch (Exception ex) { SbLog.Warn("event: " + ex.Message); }
    }
  }
}
