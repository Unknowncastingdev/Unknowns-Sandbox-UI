using System;
using GorillaLocomotion;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace UnknownsSandbox
{
  // Ender pearls: throw one (let go of it, pull the trigger while holding
  // it, or point and pull the trigger with it picked on the tablet) and you are
  // teleported to where it lands, with Minecraft's sound and purple particles.
  // Teleporting yourself only works in a SandBox lobby (_SANDBOX...) where everyone
  // in the room runs the SandBox; anywhere else the pearl just breaks.
  internal sealed partial class SandBoxWorld
  {
    private const float PearlSpeed = 13f;
    private float pearlToastAt;

    // in a SandBox lobby, and everyone else in the room has the SandBox
    internal static bool PearlAllowed
    {
      get
      {
        try
        {
          if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return false;
          string room = PhotonNetwork.CurrentRoom.Name ?? "";
          if (!room.StartsWith("_SANDBOX", StringComparison.OrdinalIgnoreCase)) return false;
          foreach (Player p in PhotonNetwork.PlayerListOthers)
            if (p != null && !SbNet.IsSandBoxUser(p.ActorNumber)) return false;
          return true;
        }
        catch { return false; }
      }
    }

    // holding one, trigger: it leaves your hand forwards
    private void ThrowPearl(SbObject o, bool right)
    {
      Transform hand = HandProvider != null ? HandProvider(right) : null;
      Vector3 dir = PointerDirProvider != null ? PointerDirProvider(right) : Vector3.zero;
      if (dir.sqrMagnitude < 1e-4f && hand != null) dir = hand.forward;
      Vector3 handVel = HandVelocity(right);
      Release(right, false);
      Launch(o, dir.normalized * PearlSpeed + handVel * 0.5f);
    }

    // picked on the tablet, point and trigger: a new pearl flies from your hand
    internal void ThrowPearlFrom(Vector3 origin, Vector3 dir)
    {
      SbItem pearl = SbCatalog.Get("mc_enderpearl");
      if (pearl == null || MineCount >= MaxMine) return;
      SbObject o = Create(0, nextId++, pearl, origin + dir.normalized * 0.15f, Quaternion.LookRotation(dir), 1f, false, 0, Vector3Int.zero);
      if (o == null) return;
      SbNet.QueueSpawn(o);
      Launch(o, dir.normalized * PearlSpeed);
    }

    private void Launch(SbObject o, Vector3 velocity)
    {
      if (o == null || o.T == null) return;
      if (o.Rb == null || o.Anchored) MakeDynamic(o, velocity);
      if (o.Rb == null) return;
      o.Rb.linearVelocity = velocity;
      o.Rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 6f;
      o.PearlThrownAt = Time.time;
      SbFx.Play(SbSound.Swing, o.T.position, 0.5f, 1.4f);
    }

    // it hit something: you appear there (where allowed), it breaks
    private void PearlHit(SbObject o, Collision c)
    {
      if (o.PearlThrownAt < 0f || Time.time - o.PearlThrownAt < 0.12f) return;   // not just leaving your hand
      o.PearlThrownAt = -1f;
      Vector3 at = o.T.position, n = Vector3.up;
      if (c != null && c.contactCount > 0) { at = c.GetContact(0).point; n = c.GetContact(0).normal; }
      Portal(at);
      if (PearlAllowed)
      {
        try
        {
          GTPlayer p = GTPlayer.Instance;
          if (p != null)
          {
            Portal(p.bodyCollider.transform.position);
            // a little off the surface, standing up
            Vector3 dest = at + n * 0.35f + Vector3.up * 0.5f;
            p.TeleportTo(dest, p.transform.rotation, false, true);
            Haptic?.Invoke(true, 0.5f, 0.1f); Haptic?.Invoke(false, 0.5f, 0.1f);
          }
        }
        catch (Exception ex) { SbLog.Warn("ender pearl: " + ex.Message); }
      }
      else if (Time.time >= pearlToastAt)
      {
        pearlToastAt = Time.time + 6f;
        Toast?.Invoke("Ender Pearl", "Teleports only in a SandBox lobby where everyone has the SandBox");
      }
      Remove(o, true, false);
    }

    private static void Portal(Vector3 at)
    {
      SbFx.Burst(at, 28, new Color(0.75f, 0.35f, 1f), new Color(0.25f, 0.05f, 0.45f), 0.3f, 1.6f, 0.01f, 0.03f, 0.4f, 1f, -0.1f, true, 0.3f);
      SbFx.PlayMc("enderman_tp", SbSound.Suck, at, 0.9f, 1f, 20f);
    }
  }
}
