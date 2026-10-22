using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace UnknownsSandbox
{
  // the SandBox server list. The servers themselves are Gorilla Tag rooms (the
  // game's own Photon); the list is kept on the SandBox's own Photon app, with
  // two small connections that never touch the game's:
  //  - the host (master client) of a listed server keeps a one-person "listing" room
  //    there, named by the server's code, with its name, player count, size and lock
  //    in the lobby properties. It goes when the host leaves or the game closes.
  //  - while the SERVERS page is open, a second connection sits in that app's lobby
  //    and gets the listing rooms (the server list) as they change.
  // Everyone connected counts towards the app's player limit (20 on the free plan), so
  // the list only connects while the page is open.
  internal static class SbServerList
  {
    private const string AppId = "240dac1b-559b-46ba-8d8f-afe712630c89";
    private const string AppVersion = "uc-servers-1";
    private const string Region = "us";   // one region for everyone: it is only a list
    internal const string KeyName = "n", KeyCount = "c", KeyMax = "m", KeyLocked = "l";

    internal sealed class Entry
    {
      public string Code, Name;
      public int Count, Max;
      public bool Locked;
    }

    // ---------------------------------------------------------------- the list (browsing)
    private sealed class LobbyListener : ILobbyCallbacks
    {
      public void OnJoinedLobby() { rooms.Clear(); Changed = true; lastProblem = string.Empty; }
      public void OnLeftLobby() { rooms.Clear(); Changed = true; }
      public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics) { }
      public void OnRoomListUpdate(List<RoomInfo> roomList)
      {
        if (roomList == null) return;
        foreach (RoomInfo r in roomList)
        {
          if (r == null || string.IsNullOrEmpty(r.Name)) continue;
          if (r.RemovedFromList) { rooms.Remove(r.Name); continue; }
          rooms[r.Name] = new Entry
          {
            Code = r.Name,
            Name = Prop(r.CustomProperties, KeyName, "SERVER"),
            Count = PropInt(r.CustomProperties, KeyCount),
            Max = PropInt(r.CustomProperties, KeyMax),
            Locked = Prop(r.CustomProperties, KeyLocked, "False") == "True"
          };
        }
        Changed = true;
      }
    }

    private static readonly Dictionary<string, Entry> rooms = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private static LoadBalancingClient browser;
    private static readonly LobbyListener listener = new LobbyListener();
    private static float browseWantedAt = -100f, browseRetryAt;
    private static string lastProblem = string.Empty;
    internal static bool Changed;

    internal static string BrowseStatus
    {
      get
      {
        if (browser == null) return string.IsNullOrEmpty(lastProblem) ? "CONNECTING..." : "COULDN'T REACH THE SERVER LIST (" + lastProblem + "), TRYING AGAIN";
        switch (browser.State)
        {
          case ClientState.JoinedLobby: return rooms.Count == 0 ? "NO SERVERS UP" : rooms.Count + (rooms.Count == 1 ? " SERVER UP" : " SERVERS UP");
          case ClientState.Disconnected: return "COULDN'T REACH THE SERVER LIST" + (string.IsNullOrEmpty(lastProblem) ? string.Empty : " (" + lastProblem + ")");
          default: return "LOADING THE SERVER LIST... (" + browser.State + ", " + protocols[protocolAt % protocols.Length] + ")";
        }
      }
    }

    internal static List<Entry> Servers()
    {
      var list = new List<Entry>(rooms.Values);
      // fullest first, then by name
      list.Sort((a, b) => b.Count != a.Count ? b.Count.CompareTo(a.Count) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
      return list;
    }

    // called while the SERVERS page is open
    internal static void WantList() { browseWantedAt = Time.unscaledTime; }

    // ---------------------------------------------------------------- the listing (hosting)
    private static LoadBalancingClient host;
    private static string hostCode, hostName;
    private static int hostCount = -1, hostMax;
    private static bool hostLocked;
    private static string hostSentKey = string.Empty;
    private static float hostRetryAt;

    // the server you host right now (null: none)
    internal static void SetHosted(string code, string name, int count, int max, bool locked)
    {
      hostCode = code;
      hostName = name;
      hostCount = count;
      hostMax = max;
      hostLocked = locked;
    }

    // ---------------------------------------------------------------- every frame
    internal static void Tick()
    {
      float now = Time.unscaledTime;
      try { TickBrowser(now); } catch (Exception ex) { SbLog.Throttled("Server list", ex); }
      try { TickHost(now); } catch (Exception ex) { SbLog.Throttled("Server listing", ex); }
    }

    // each failed try uses the next transport: UDP, then TCP, then secure web
    // sockets (port 443, gets through most firewalls). The list has its own sockets,
    // separate from the game's connection.
    private static readonly ConnectionProtocol[] protocols = { ConnectionProtocol.Udp, ConnectionProtocol.Tcp, ConnectionProtocol.WebSocketSecure };
    private static int protocolAt;

    private static LoadBalancingClient Connect()
    {
      ConnectionProtocol proto = protocols[protocolAt % protocols.Length];
      var c = new LoadBalancingClient(proto);
      try
      {
        c.LoadBalancingPeer.SocketImplementationConfig = new Dictionary<ConnectionProtocol, Type>
        {
          { ConnectionProtocol.Udp, typeof(SocketUdp) },
          { ConnectionProtocol.Tcp, typeof(SocketTcp) },
          { ConnectionProtocol.WebSocket, typeof(PhotonClientWebSocket) },
          { ConnectionProtocol.WebSocketSecure, typeof(PhotonClientWebSocket) }
        };
      }
      catch (Exception ex) { SbLog.Warn("Server list sockets: " + ex.Message); }
      var settings = new AppSettings { AppIdRealtime = AppId, AppVersion = AppVersion, FixedRegion = Region, UseNameServer = true, Protocol = proto };
      if (!c.ConnectUsingSettings(settings)) { lastProblem = "could not start " + proto; SbLog.Warn("Server list: could not start a " + proto + " connection"); }
      return c;
    }

    private static void TickBrowser(float now)
    {
      bool want = now - browseWantedAt < 8f;   // a few seconds after the page closes
      if (!want)
      {
        if (browser != null)
        {
          browser.RemoveCallbackTarget(listener);
          try { browser.Disconnect(); } catch { }
          browser = null;
          rooms.Clear();
          Changed = true;
        }
        return;
      }
      if (browser == null)
      {
        if (now < browseRetryAt) return;
        browser = Connect();
        browser.AddCallbackTarget(listener);
        browserStartedAt = now;
      }
      browser.Service();
      if (browser.State == ClientState.ConnectedToMasterServer) browser.OpJoinLobby(TypedLobby.Default);
      // stuck on the way (a blocked port): give up on this way after 12 s
      bool stuck = browser.State != ClientState.JoinedLobby && browser.State != ClientState.Disconnected && now - browserStartedAt > 12f;
      if (stuck || browser.State == ClientState.Disconnected)
      {
        // try again a little later, the next way
        lastProblem = stuck ? "timed out on " + protocols[protocolAt % protocols.Length] : browser.DisconnectedCause.ToString();
        SbLog.Warn("Server list failed: " + lastProblem);
        protocolAt++;
        browser.RemoveCallbackTarget(listener);
        try { if (stuck) browser.Disconnect(); } catch { }
        browser = null;
        browseRetryAt = now + 2f;
        Changed = true;
      }
    }

    private static float browserStartedAt;

    private static void TickHost(float now)
    {
      bool want = !string.IsNullOrEmpty(hostCode);
      if (!want)
      {
        if (host != null)
        {
          try { host.Disconnect(); } catch { }
          host = null;
          hostSentKey = string.Empty;
        }
        return;
      }
      if (host == null)
      {
        if (now < hostRetryAt) return;
        host = Connect();
        hostSentKey = string.Empty;
      }
      host.Service();
      switch (host.State)
      {
        case ClientState.ConnectedToMasterServer:
          if (now < hostRetryAt) break;
          hostRetryAt = now + 5f;   // the name may still be taken by the last host for a moment
          host.OpCreateRoom(new EnterRoomParams
          {
            RoomName = hostCode,
            RoomOptions = new RoomOptions
            {
              IsVisible = true,
              IsOpen = false,
              MaxPlayers = 1,
              EmptyRoomTtl = 0,
              PlayerTtl = 0,
              CustomRoomProperties = Props(),
              CustomRoomPropertiesForLobby = new[] { KeyName, KeyCount, KeyMax, KeyLocked }
            },
            Lobby = TypedLobby.Default
          });
          hostSentKey = Key();
          break;
        case ClientState.Joined:
          if (host.CurrentRoom == null) break;
          if (host.CurrentRoom.Name != hostCode) { host.OpLeaveRoom(false); break; }
          string key = Key();
          if (key != hostSentKey)
          {
            host.CurrentRoom.SetCustomProperties(Props());
            hostSentKey = key;
          }
          break;
        case ClientState.Disconnected:
          host = null;
          hostRetryAt = now + 6f;
          break;
      }
    }

    private static string Key() { return hostName + "|" + hostCount + "|" + hostMax + "|" + hostLocked; }

    private static PhotonHashtable Props()
    {
      return new PhotonHashtable { { KeyName, hostName ?? "SERVER" }, { KeyCount, hostCount }, { KeyMax, hostMax }, { KeyLocked, hostLocked } };
    }

    internal static void Shutdown()
    {
      try { browser?.Disconnect(); } catch { }
      try { host?.Disconnect(); } catch { }
      browser = host = null;
    }

    private static string Prop(PhotonHashtable h, string key, string fallback)
    {
      object v;
      return h != null && h.TryGetValue(key, out v) && v != null ? v.ToString() : fallback;
    }

    private static int PropInt(PhotonHashtable h, string key)
    {
      object v;
      if (h == null || !h.TryGetValue(key, out v) || v == null) return 0;
      try { return Convert.ToInt32(v); } catch { return 0; }
    }
  }
}

namespace UnknownsSandbox
{
  // the server list on the SERVERS page (rows made once, filled every second)
  public partial class SandboxPlugin
  {
    private const int SbListRows = 8;
    private readonly List<SbCtl> sbListRowCtls = new List<SbCtl>();
    private readonly List<SbServerList.Entry> sbListShown = new List<SbServerList.Entry>();
    private TMPro.TMP_Text sbListStatus;
    private float sbListRefreshAt;

    private void SbTickServerList(Photon.Realtime.Room room, float now)
    {
      // the server you host is listed while you host it
      bool hosting = false;
      try
      {
        hosting = room != null && SbIsServerRoom(room.Name) && Photon.Pun.PhotonNetwork.IsMasterClient &&
                  !string.IsNullOrEmpty(SbRoomProp(room, SbServerPatches.PropName)) && SbRoomProp(room, SbServerPatches.PropListed) != "False";
      }
      catch { }
      if (hosting)
        SbServerList.SetHosted(room.Name, SbRoomProp(room, SbServerPatches.PropName), room.PlayerCount, room.MaxPlayers,
          !string.IsNullOrEmpty(SbRoomProp(room, SbServerPatches.PropPw)));
      else SbServerList.SetHosted(null, null, 0, 0, false);

      if (sbVisible && sbServerOpen) SbServerList.WantList();
      SbServerList.Tick();

      if (sbListStatus == null || (!SbServerList.Changed && now < sbListRefreshAt)) return;
      SbServerList.Changed = false;
      sbListRefreshAt = now + 1f;
      SbFillServerListRows();
    }

    private readonly List<TMPro.TMP_Text> sbListCountTexts = new List<TMPro.TMP_Text>();
    private int sbListBuilt = -1;
    private TMPro.TMP_Text sbListEmptyText;

    // the list is the first thing on the SERVERS page: one row per server (name on the
    // left, how many are in it on the right), and NO SERVERS UP when there are none. The
    // rows are laid out for the servers there are now; the page lays itself out again when
    // that number changes.
    private void SbBuildServerListRows(SbStyle st, ref float y, Action<string> section)
    {
      sbListRowCtls.Clear();
      sbListCountTexts.Clear();
      sbListEmptyText = null;
      float w = sbContent.sizeDelta.x;
      section("SERVER LIST");
      sbListStatus = SbText(sbContent, SbServerList.BrowseStatus, new Vector2(0f, y - 14f), new Vector2(w - 30f, 26f), 14f, st.Sub, TMPro.TextAlignmentOptions.Left, TMPro.FontStyles.Normal);
      SbAnchorTop(sbListStatus.rectTransform);
      y -= 32f;
      int rows = Mathf.Clamp(SbServerList.Servers().Count, 0, SbListRows);
      sbListBuilt = rows;
      if (rows == 0)
      {
        sbListEmptyText = SbText(sbContent, "NO SERVERS UP", new Vector2(0f, y - 34f), new Vector2(w - 30f, 40f), 24f, st.Text, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);
        SbAnchorTop(sbListEmptyText.rectTransform);
        TMPro.TMP_Text sub = SbText(sbContent, "Press CREATE SERVER (top right) to start one.", new Vector2(0f, y - 70f), new Vector2(w - 30f, 26f), 14f, st.Sub, TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Normal);
        SbAnchorTop(sub.rectTransform);
        y -= 100f;
      }
      for (int i = 0; i < rows; i++)
      {
        int idx = i;
        // (the text is a space, not empty: a button with no text has no label to fill in later, which is why the names never showed)
        SbCtl row = SbMark(SbButton(1, sbContent, " ", new Vector2(0f, y - 25f), new Vector2(w - 30f, 46f), null, () => SbPickListedServer(idx), 17f));
        SbAnchorTop(row.rt);
        if (row.label != null)
        {
          row.label.alignment = TMPro.TextAlignmentOptions.Left;
          row.label.rectTransform.sizeDelta = new Vector2(w - 190f, 40f);
          row.label.rectTransform.anchoredPosition = new Vector2(-70f, 0f);
          row.label.fontStyle = TMPro.FontStyles.Bold;
        }
        TMPro.TMP_Text count = SbText(row.rt, " ", new Vector2((w - 30f) * 0.5f - 62f, 0f), new Vector2(110f, 40f), 19f, st.Accent, TMPro.TextAlignmentOptions.Right, TMPro.FontStyles.Bold);
        sbListRowCtls.Add(row);
        sbListCountTexts.Add(count);
        y -= 52f;
      }
      y -= 8f;
      SbFillServerListRows();
    }

    private void SbFillServerListRows()
    {
      if (sbListStatus == null) return;
      sbListStatus.text = SbServerList.BrowseStatus;
      sbListShown.Clear();
      sbListShown.AddRange(SbServerList.Servers());
      // the number of servers changed: the page is laid out again for it
      int want = Mathf.Clamp(sbListShown.Count, 0, SbListRows);
      if (want != sbListBuilt && sbServerOpen && !sbSrvCreating && !sbSrvHosting && sbContent != null && !SbServerBusy())
      {
        sbListBuilt = want;
        SbRebuildGrid();
        return;
      }
      string here = string.Empty;
      try { here = Photon.Pun.PhotonNetwork.InRoom && Photon.Pun.PhotonNetwork.CurrentRoom != null ? Photon.Pun.PhotonNetwork.CurrentRoom.Name : string.Empty; } catch { }
      for (int i = 0; i < sbListRowCtls.Count; i++)
      {
        SbCtl row = sbListRowCtls[i];
        if (row == null || row.rt == null || row.label == null) continue;
        if (i >= sbListShown.Count) { row.rt.gameObject.SetActive(false); continue; }
        row.rt.gameObject.SetActive(true);
        SbServerList.Entry e = sbListShown[i];
        bool full = e.Max > 0 && e.Count >= e.Max;
        row.label.text = (e.Locked ? "[LOCKED]  " : string.Empty) + e.Name + (e.Code == here ? "   (YOU ARE HERE)" : string.Empty);
        if (i < sbListCountTexts.Count && sbListCountTexts[i] != null) sbListCountTexts[i].text = e.Count + " / " + e.Max + (full ? " FULL" : string.Empty);
      }
    }

    private void SbPickListedServer(int idx)
    {
      if (idx < 0 || idx >= sbListShown.Count) return;
      SbServerList.Entry e = sbListShown[idx];
      if (e.Max > 0 && e.Count >= e.Max) { this.ShowToast("Servers", e.Name + " is full"); return; }
      if (e.Locked)
      {
        // its code goes in the box: type the password, then JOIN WITH CODE
        sbSrvCode = e.Code;
        sbSrvField = 4;
        SbRefreshFields();
        this.ShowToast("Servers", "Type the password for " + e.Name + ", then press JOIN WITH CODE");
        return;
      }
      SbServerJoin(new SbServerPatches.Request { Code = e.Code }, e.Code);
    }
  }
}
