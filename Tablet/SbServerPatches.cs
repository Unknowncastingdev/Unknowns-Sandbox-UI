using System;
using System.Reflection;
using HarmonyLib;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace UnknownsSandbox
{
  // SandBox servers (SandBoxServers.cs). They are joined the game's own way
  // (PhotonNetworkController.AttemptToJoinSpecificRoomWithCallback), and just before
  // the game connects, the room it would make is changed: its size (1 - 20: the game keeps 19 other gorillas), public or
  // not, its own queue (so the game's matchmaking never puts anyone in it) and the
  // server's name / password check / public mark as room properties. Quick Join asks
  // the game for a random room with the public mark instead of a named one.
  internal static class SbServerPatches
  {
    internal const string QueueName = "UCSERVER";
    internal const string PropName = "ucName", PropPw = "ucPw", PropPublic = "ucPublic", PropListed = "ucList";

    internal sealed class Request
    {
      public string Code;
      public bool Create;
      public string Name = string.Empty, PwHash = string.Empty;
      public int Max = 10;
      public bool Public;
      public bool Listed = true;   // shown in the server list (SandBoxServerList.cs)
      public bool Casual;
      public string PrevGameMode;
      public float At;
    }

    // the server being joined / made, null when none
    internal static volatile Request Pending;
    private static bool installed;

    internal static void Install(Harmony harmony)
    {
      if (installed || harmony == null) return;
      installed = true;
      try
      {
        MethodInfo connect = AccessTools.Method(typeof(NetworkSystemPUN), "ConnectToRoom");
        if (connect != null) harmony.Patch(connect, prefix: new HarmonyMethod(typeof(SbServerPatches), nameof(ConnectPrefix)));
        else SbLog.Warn("NetworkSystemPUN.ConnectToRoom not found");
      }
      catch (Exception ex)
      {
        SbLog.Warn("Could not patch the room joining: " + ex.Message);
      }
    }

    // Servers are made like the game's own private rooms (joinable by their code only,
    // in the game's own queue), so matchmaking never puts anyone random in them.
    // "Public" is just the server's mark: no lock in the server list, and Quick Join picks it.
    private static void ConnectPrefix(ref string roomName, RoomConfig opts)
    {
      try
      {
        Request req = Pending;
        if (req == null || opts == null) return;
        if (!string.Equals(roomName, req.Code, StringComparison.Ordinal)) return;
        if (req.Create)
        {
          opts.isPublic = false;
          opts.isJoinable = true;
          opts.MaxPlayers = (byte)Math.Max(1, Math.Min(20, req.Max));
          SetServerProps(opts, req.Name, req.Public ? string.Empty : req.PwHash, req.Public, req.Listed);
        }
        else
        {
          // joining by code: if there is no such server the game makes an empty room,
          // which is left at once (SandBoxServers.cs); nobody else can get into it
          opts.isPublic = false;
          opts.MaxPlayers = 1;
        }
      }
      catch (Exception ex) { SbLog.Warn("server join: " + ex.Message); }
    }

    private static void SetServerProps(RoomConfig opts, string name, string pwHash, bool isPublic, bool listed)
    {
      if (opts.CustomProps == null) opts.CustomProps = new PhotonHashtable();
      opts.CustomProps[PropName] = name ?? string.Empty;
      opts.CustomProps[PropPw] = pwHash ?? string.Empty;
      opts.CustomProps[PropPublic] = isPublic;
      opts.CustomProps[PropListed] = listed;
    }
  }
}
