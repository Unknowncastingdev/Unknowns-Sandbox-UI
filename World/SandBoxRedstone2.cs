using System.Collections.Generic;
using UnityEngine;

namespace UnknownsSandbox
{
  // more redstone. Redstone dust carries power along a line (15 blocks, one step
  // up or down), repeaters pass it on at full strength after a delay (touch: 1 - 4),
  // comparators pass it on (subtract mode: not while a side is powered), observers give
  // a short pulse out of their back when the block in front of their face changes, and
  // pistons push a row of up to 12 blocks the way they face (sticky pistons pull the
  // block in front back). Like the rest of the redstone, everyone works it out from what
  // they see; each player moves their own blocks.
  internal sealed partial class SandBoxWorld
  {
    private readonly Dictionary<SbObject, string> observerSeen = new Dictionary<SbObject, string>();
    private readonly Dictionary<SbObject, GameObject> pistonHeads = new Dictionary<SbObject, GameObject>();
    private readonly HashSet<long> dustLit = new HashSet<long>();
    private readonly Queue<KeyValuePair<SbObject, int>> dustQueue = new Queue<KeyValuePair<SbObject, int>>();

    // the cell a block faces (+Z: away from whoever placed it), as a step on the grid
    private static Vector3Int Facing(SbObject o)
    {
      Vector3 f = o.T != null ? o.T.forward : Vector3.forward;
      return Mathf.Abs(f.x) > Mathf.Abs(f.z) ? new Vector3Int(f.x > 0f ? 1 : -1, 0, 0) : new Vector3Int(0, 0, f.z > 0f ? 1 : -1);
    }

    private bool PoweredAt(int s, Vector3Int c) { return powered.Contains(Pack(s, c.x, c.y, c.z)); }

    private void PowerNeighbours(int s, Vector3Int c)
    {
      powered.Add(Pack(s, c.x, c.y, c.z));
      foreach (Vector3Int d in Dirs6) powered.Add(Pack(s, c.x + d.x, c.y + d.y, c.z + d.z));
    }

    private void PowerFromDiodesAndDust()
    {
      // what the diodes put out (their state from the last step: that is their delay)
      foreach (SbObject o in List)
      {
        if (!o.IsCell || o.T == null || !o.RedOn) continue;
        SbShape sh = o.Item.Shape;
        if (sh == SbShape.Repeater || sh == SbShape.Comparator) PowerNeighbours(o.SizeIdx, o.Cell + Facing(o));
        else if (o.Item.Id == "mc_observer") PowerNeighbours(o.SizeIdx, o.Cell + Facing(o));   // out of its back (+Z)
      }
      // the dust: from every powered dust cell along the dust, 15 steps
      dustLit.Clear();
      dustQueue.Clear();
      foreach (SbObject o in List)
        if (o.IsCell && o.Item.Shape == SbShape.Dust && PoweredAt(o.SizeIdx, o.Cell) && dustLit.Add(Pack(o.SizeIdx, o.Cell.x, o.Cell.y, o.Cell.z)))
          dustQueue.Enqueue(new KeyValuePair<SbObject, int>(o, 15));
      while (dustQueue.Count > 0)
      {
        var kv = dustQueue.Dequeue();
        SbObject d = kv.Key;
        PowerNeighbours(d.SizeIdx, d.Cell + Vector3Int.down);   // the block under it, and round it
        PowerNeighbours(d.SizeIdx, d.Cell);
        if (kv.Value <= 1) continue;
        for (int dx = -1; dx <= 1; dx++)
          for (int dz = -1; dz <= 1; dz++)
          {
            if ((dx == 0) == (dz == 0)) continue;   // the four sides only
            for (int dy = -1; dy <= 1; dy++)
            {
              Vector3Int n = d.Cell + new Vector3Int(dx, dy, dz);
              SbObject next = CellAt(d.SizeIdx, n);
              if (next == null || next.Item.Shape != SbShape.Dust) continue;
              if (!dustLit.Add(Pack(d.SizeIdx, n.x, n.y, n.z))) continue;
              dustQueue.Enqueue(new KeyValuePair<SbObject, int>(next, kv.Value - 1));
            }
          }
      }
      // the dust: lines to what it connects to, bright red while it carries power
      foreach (SbObject o in List)
        if (o.IsCell && o.Item.Shape == SbShape.Dust) SetDustLook(o, dustLit.Contains(Pack(o.SizeIdx, o.Cell.x, o.Cell.y, o.Cell.z)), DustMask(o));
    }

    // which sides the dust joins (1 +Z, 2 +X, 4 -Z, 8 -X): dust next to it (or a
    // step up / down), and redstone parts next to it
    private static readonly Vector3Int[] dustSides = { new Vector3Int(0, 0, 1), new Vector3Int(1, 0, 0), new Vector3Int(0, 0, -1), new Vector3Int(-1, 0, 0) };

    private int DustMask(SbObject d)
    {
      int mask = 0;
      for (int i = 0; i < 4; i++)
      {
        Vector3Int c = d.Cell + dustSides[i];
        SbObject n = CellAt(d.SizeIdx, c);
        bool joins = n != null && (n.Item.Shape == SbShape.Dust || IsRedstoneSource(n.Item) || n.Item.Shape == SbShape.Torch || n.Item.Shape == SbShape.Repeater ||
                                   n.Item.Shape == SbShape.Comparator || n.Item.Id == "mc_observer" || n.Item.Id == "mc_redstone");
        if (!joins)
        {
          SbObject up = CellAt(d.SizeIdx, c + Vector3Int.up), down = CellAt(d.SizeIdx, c + Vector3Int.down);
          joins = (up != null && up.Item.Shape == SbShape.Dust) || (down != null && down.Item.Shape == SbShape.Dust);
        }
        if (joins) mask |= 1 << i;
      }
      return mask;
    }

    private static MaterialPropertyBlock dustTint;

    private void SetDustLook(SbObject o, bool lit, int mask)
    {
      if (o.Go == null) return;
      int look = mask | (lit ? 16 : 0);
      if (o.ShapeState == lit && o.Note == look + 1) return;
      o.ShapeState = lit;
      o.Note = look + 1;
      var mf = o.Go.GetComponent<MeshFilter>();
      if (mf != null) mf.sharedMesh = SbMcModels.DustMesh(mask);
      var mr = o.Go.GetComponent<MeshRenderer>();
      if (mr == null) return;
      if (dustTint == null) dustTint = new MaterialPropertyBlock();
      // Minecraft's dust: dark red with no power, bright red with full power
      Color c = lit ? Color.white : new Color(0.38f, 0.3f, 0.3f, 1f);
      mr.GetPropertyBlock(dustTint);
      dustTint.SetColor("_BaseColor", c);
      dustTint.SetColor("_Color", c);
      mr.SetPropertyBlock(dustTint);
    }

    // after the power is known: the diodes' next state
    private void UpdateDiodes(float now)
    {
      foreach (SbObject o in List)
      {
        if (!o.IsCell || o.T == null) continue;
        SbShape sh = o.Item.Shape;
        Vector3Int f = Facing(o);
        if (sh == SbShape.Repeater)
        {
          bool input = PoweredAt(o.SizeIdx, o.Cell - f);
          if (input != o.Powered) { o.Powered = input; o.OnUntil = now + 0.1f * Mathf.Max(1, o.Note); }
          if (now >= o.OnUntil && o.RedOn != o.Powered) { o.RedOn = o.Powered; SetShapeState(o, o.RedOn); }
        }
        else if (sh == SbShape.Comparator)
        {
          bool back = PoweredAt(o.SizeIdx, o.Cell - f);
          Vector3Int side = new Vector3Int(f.z, 0, -f.x);
          bool sides = PoweredAt(o.SizeIdx, o.Cell + side) || PoweredAt(o.SizeIdx, o.Cell - side);
          bool on = back && (o.Note == 0 || !sides);
          if (on != o.RedOn) { o.RedOn = on; SetShapeState(o, on); }
        }
        else if (o.Item.Id == "mc_observer")
        {
          // what is in front of its face (-Z)
          SbObject seen = CellAt(o.SizeIdx, o.Cell - f);
          string sig = seen == null ? "" : seen.Item.Id + (seen.ShapeState ? "+" : "-") + (seen.Powered ? "p" : "");
          string was;
          bool known = observerSeen.TryGetValue(o, out was);
          observerSeen[o] = sig;
          if (known && was != sig) { o.RedOn = true; o.OnUntil = now + 0.2f; }
          else if (o.RedOn && now >= o.OnUntil) o.RedOn = false;
        }
      }
      if (observerSeen.Count > 400) observerSeen.Clear();
    }

    // ---------------------------------------------------------------- pistons
    private void PistonMove(SbObject piston, bool extend)
    {
      int s = piston.SizeIdx;
      Vector3Int dir = -Facing(piston);   // the face (it faces you when placed)
      SbFx.PlayMc(extend ? "piston_out" : "piston_in", SbSound.Thunk, piston.Center, 0.7f, extend ? 1.1f : 0.9f, 16f);
      if (extend)
      {
        // the row in front, up to 12 blocks, and a free cell after it
        var row = new List<SbObject>();
        Vector3Int c = piston.Cell + dir;
        while (true)
        {
          SbObject b = CellAt(s, c);
          if (b == null) break;
          if (IsImmovable(b) || b.Item.Kind != SbKind.Block || row.Count >= 12) { ShowPistonHead(piston, false); return; }
          row.Add(b);
          c += dir;
        }
        // the far end first, so every block has a free cell to go to
        for (int i = row.Count - 1; i >= 0; i--)
        {
          SbObject b = row[i];
          if (b.Mine || b.Generated) MoveCell(b, b.Cell + dir);
        }
        ShowPistonHead(piston, true);
      }
      else
      {
        ShowPistonHead(piston, false);
        if (piston.Item.Id != "mc_sticky_piston") return;
        SbObject b = CellAt(s, piston.Cell + dir * 2);
        if (b != null && (b.Mine || b.Generated) && b.Item.Kind == SbKind.Block && !IsImmovable(b) && CellAt(s, piston.Cell + dir) == null)
          MoveCell(b, piston.Cell + dir);
      }
    }

    // the piston's head and arm, out in front of its face while it is on
    private void ShowPistonHead(SbObject piston, bool out_)
    {
      GameObject head;
      pistonHeads.TryGetValue(piston, out head);
      if (!out_) { if (head != null) Destroy(head); pistonHeads.Remove(piston); return; }
      if (head != null || piston.T == null) return;
      head = new GameObject("piston head");
      head.transform.SetParent(piston.T, false);
      int face = piston.Item.Id == "mc_sticky_piston" ? SbBlockArt.T("piston_top_sticky") : SbBlockArt.T("piston_top");
      Mesh plate = SbBlockMesh.Get(SbBlockArt.T("piston_side"), SbBlockArt.T("piston_side"), SbBlockArt.T("piston_side"), face);
      var p = new GameObject("plate");
      p.transform.SetParent(head.transform, false);
      p.transform.localPosition = new Vector3(0f, 0f, -1.375f);
      p.transform.localScale = new Vector3(1f, 1f, 0.25f);
      p.AddComponent<MeshFilter>().sharedMesh = plate;
      p.AddComponent<MeshRenderer>().sharedMaterials = new[] { SbMats.BlockLit };
      var arm = new GameObject("arm");
      arm.transform.SetParent(head.transform, false);
      arm.transform.localPosition = new Vector3(0f, 0f, -0.875f);
      arm.transform.localScale = new Vector3(0.25f, 0.25f, 0.75f);
      arm.AddComponent<MeshFilter>().sharedMesh = SbBlockMesh.Get(SbBlockArt.T("piston_side"), SbBlockArt.T("piston_side"), SbBlockArt.T("piston_side"), SbBlockArt.T("piston_side"));
      arm.AddComponent<MeshRenderer>().sharedMaterials = new[] { SbMats.BlockLit };
      pistonHeads[piston] = head;
    }
  }
}
