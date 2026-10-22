using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace UnknownsSandbox
{
  // The player rigs in the scene. Searching the scene is slow, so the list is only
  // rebuilt when the room or its player count changes (or a rig in it was destroyed).
  internal static class SbRigs
  {
    private static readonly List<VRRig> rigs = new List<VRRig>();
    private static string builtForRoom;
    private static int builtForCount = -1;

    internal static List<VRRig> All()
    {
      string room = null;
      int count = 0;
      try
      {
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
        {
          room = PhotonNetwork.CurrentRoom.Name;
          count = PhotonNetwork.CurrentRoom.PlayerCount;
        }
      }
      catch { }
      if (room != builtForRoom || count != builtForCount || rigs.Exists(r => r == null))
      {
        builtForRoom = room;
        builtForCount = count;
        rigs.Clear();
        rigs.AddRange(Object.FindObjectsByType<VRRig>(FindObjectsSortMode.None));
      }
      return rigs;
    }

    internal static bool IsMine(VRRig rig)
    {
      if (rig == null) return false;
      if (rig.isOfflineVRRig || rig.isMyPlayer || rig.isLocal) return true;
      return GorillaTagger.Instance != null && rig == GorillaTagger.Instance.offlineVRRig;
    }
  }
}
