using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using GorillaNetworking;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace UnknownsSandbox
{
  // the SandBox tablet's SERVERS page. Besides the default server (_SANDBOX,
  // SandBoxLobby.cs) anyone can make their own: a name (bad words filtered), 1 - 20
  // players, and either a password (only people who have it get in) or public (anyone
  // can Quick Join it). Its code is _UC and six random letters; the in-game computer
  // can't type the _, so only SandBox players get in. Passwords are checked
  // twice: a client with the wrong one leaves by itself, and the server's master
  // client sends away anyone whose password doesn't match (SbServerPatches.cs makes
  // the rooms).
  public partial class SandboxPlugin
  {
    private const string SbServerPrefix = "_UC";
    private const float SbServerTimeout = 30f;
    private const string SbPlayerPwProp = "ucPw";

    private bool sbServerOpen;
    private string sbSrvName = string.Empty, sbSrvPassword = string.Empty, sbSrvCode = string.Empty, sbSrvJoinPw = string.Empty;
    private int sbSrvMax = 10;
    private bool sbSrvPublic = true;      // public by default
    private bool sbSrvCasual;
    private bool sbSrvCreating;           // the CREATE SERVER page is open
    private bool sbSrvListed = true;
    private int sbSrvField;          // 0 none, 1 name, 2 password, 3 code, 4 join password
    private readonly Dictionary<int, SbCtl> sbSrvFieldCtls = new Dictionary<int, SbCtl>();
    private TMP_Text sbSrvStatus;
    private float sbSrvStatusAt;
    private string sbSrvJoinedRoom = string.Empty;
    private float sbSrvJoinedAt;
    private readonly Dictionary<int, float> sbSrvSeen = new Dictionary<int, float>();
    private float sbSrvCheckAt;

    private static readonly System.Random sbSrvRng = new System.Random();

    internal static string SbNewServerCode()
    {
      const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
      var sb = new StringBuilder(SbServerPrefix);
      lock (sbSrvRng)
        for (int i = 0; i < 6; i++) sb.Append(letters[sbSrvRng.Next(letters.Length)]);
      return sb.ToString();
    }

    private static string SbServerHash(string code, string password)
    {
      if (string.IsNullOrEmpty(password)) return string.Empty;
      using (SHA256 sha = SHA256.Create())
      {
        byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes((code ?? string.Empty).ToUpperInvariant() + ":" + password));
        return Convert.ToBase64String(h, 0, 12);
      }
    }

    private static bool SbIsServerRoom(string room)
    {
      return !string.IsNullOrEmpty(room) && room.StartsWith(SbServerPrefix, StringComparison.Ordinal);
    }

    private static string SbRoomProp(Room r, string key)
    {
      try
      {
        object v;
        return r != null && r.CustomProperties != null && r.CustomProperties.TryGetValue(key, out v) && v != null ? v.ToString() : string.Empty;
      }
      catch { return string.Empty; }
    }

    private string SbCleanServerName(string name)
    {
      name = (name ?? string.Empty).Trim();
      if (name.Length > 20) name = name.Substring(0, 20);
      name = CensorWords(name);
      return string.IsNullOrEmpty(name) ? "UNKNOWN'S SERVER" : name.ToUpperInvariant();
    }

    // ================================================================ joining / making
    private bool SbServerBusy()
    {
      SbServerPatches.Request req = SbServerPatches.Pending;
      return (req != null && Time.unscaledTime - req.At < SbServerTimeout) || sbLobbyPhase == SbLobbyPhase.Joining;
    }

    private void SbServerJoin(SbServerPatches.Request req, string room)
    {
      if (SbServerBusy()) { this.ShowToast("Servers", "Already joining, give it a moment"); return; }
      if ((UnityEngine.Object)PhotonNetworkController.Instance == (UnityEngine.Object)null) { this.ShowToast("Servers", "Not connected to Gorilla Tag yet"); return; }
      req.At = Time.unscaledTime;
      SbServerPatches.Pending = req;
      if (req.Create) SbServerPrepareModdedMode(req);
      try { PhotonNetwork.LocalPlayer?.SetCustomProperties(new PhotonHashtable { { SbPlayerPwProp, req.PwHash ?? string.Empty } }); } catch { }
      try
      {
        PhotonNetworkController.Instance.AttemptToJoinSpecificRoomWithCallback(room, (JoinType)0, r =>
        {
          if (req.Create) SbServerRestoreGameMode(req);
          if (r == NetJoinResult.Failed_Full) this.ShowToast("Servers", "That server is full");
          if (r == NetJoinResult.Failed_Full || r == NetJoinResult.Failed_Other) SbServerPatches.Pending = null;
          if (r != NetJoinResult.Failed_Other) return;
          // a server bigger than the game allows is not made: make it again with 10
          if (req.Create && req.Max > 10)
          {
            this.ShowToast("Servers", "Gorilla Tag didn't allow " + req.Max + " players, making it with 10");
            req.Max = 10;
            sbSrvRetryReq = req;
            sbSrvRetryAt = Time.unscaledTime + 1.5f;
            return;
          }
          this.ShowToast("Servers", "Couldn't connect to that server, try again in a moment");
        });
        SbSetServerNote("JOINING...");
      }
      catch (Exception ex)
      {
        SbServerPatches.Pending = null;
        this.ShowToast("Servers", "Join failed: " + ex.Message);
      }
    }

    private void SbCreateServer()
    {
      if (!sbSrvPublic && string.IsNullOrEmpty(sbSrvPassword))
      {
        this.ShowToast("Servers", "Give it a password, or make it public");
        return;
      }
      string code = SbNewServerCode();
      SbServerJoin(new SbServerPatches.Request
      {
        Code = code,
        Create = true,
        Name = SbCleanServerName(sbSrvName),
        PwHash = sbSrvPublic ? string.Empty : SbServerHash(code, sbSrvPassword),
        Max = sbSrvMax,
        Public = sbSrvPublic,
        Listed = sbSrvListed,
        Casual = sbSrvCasual
      }, code);
    }

    private void SbJoinServerByCode()
    {
      string code = (sbSrvCode ?? string.Empty).Trim().ToUpperInvariant();
      if (code.Length == 0) { this.ShowToast("Servers", "Type the server's code first"); return; }
      if (!code.StartsWith("_")) code = "_" + code;
      if (!SbIsServerRoom(code)) { this.ShowToast("Servers", "Server codes start with _UC"); return; }
      SbServerJoin(new SbServerPatches.Request { Code = code, PwHash = SbServerHash(code, sbSrvJoinPw) }, code);
    }

    // the fullest public server in the list with room; none: a new public one
    private void SbQuickJoinPublic()
    {
      SbServerList.WantList();
      string here = string.Empty;
      try { here = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : string.Empty; } catch { }
      foreach (SbServerList.Entry e in SbServerList.Servers())
      {
        if (e.Locked || e.Code == here || (e.Max > 0 && e.Count >= e.Max)) continue;
        SbServerJoin(new SbServerPatches.Request { Code = e.Code }, e.Code);
        return;
      }
      string code = SbNewServerCode();
      this.ShowToast("Servers", "No public server with room, making one");
      SbServerJoin(new SbServerPatches.Request { Code = code, Create = true, Name = "PUBLIC SERVER", Max = 10, Public = true, Listed = true }, code);
    }

    private SbServerPatches.Request sbSrvRetryReq;
    private float sbSrvRetryAt;

    private void SbServerPrepareModdedMode(SbServerPatches.Request req)
    {
      try
      {
        if (GorillaComputer.instance != null) req.PrevGameMode = GorillaComputer.instance.currentGameMode.Value;
        if (GorillaComputer.instance != null) GorillaComputer.instance.currentGameMode.Value = req.Casual ? "Casual;MODDED_" : "Infection;MODDED_";
      }
      catch { }
    }

    private void SbServerRestoreGameMode(SbServerPatches.Request req)
    {
      try
      {
        if (req != null && !string.IsNullOrEmpty(req.PrevGameMode) && GorillaComputer.instance != null) GorillaComputer.instance.currentGameMode.Value = req.PrevGameMode;
      }
      catch { }
    }

    private void SbLeaveToSinglePlayer()
    {
      try { NetworkSystem.Instance?.ReturnToSinglePlayer(); } catch { }
    }

    // ================================================================ every frame
    private void SbServerTick()
    {
      float now = Time.unscaledTime;
      SbServerPatches.Request req = SbServerPatches.Pending;
      if (req != null && now - req.At > SbServerTimeout) { SbServerPatches.Pending = req = null; SbSetServerNote("TIMED OUT"); }
      if (sbSrvRetryReq != null && now >= sbSrvRetryAt && !SbServerBusy())
      {
        SbServerPatches.Request again = sbSrvRetryReq;
        sbSrvRetryReq = null;
        SbServerJoin(again, again.Code);
      }

      Room room = null;
      try { room = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom : null; } catch { }
      string name = room != null ? room.Name : string.Empty;
      // SERVER HOST button: only while you host a server (and its page closes when you stop)
      bool isHost = false;
      try { isHost = room != null && SbIsServerRoom(room.Name) && PhotonNetwork.IsMasterClient; } catch { }
      if (sbHostBtn != null && sbHostBtn.rt != null && sbHostBtn.rt.gameObject.activeSelf != isHost) sbHostBtn.rt.gameObject.SetActive(isHost);
      if (sbSrvHosting && !isHost) { sbSrvHosting = false; if (sbServerOpen && sbContent != null) SbRebuildGrid(); }
      if (name != sbSrvJoinedRoom)
      {
        sbSrvJoinedRoom = name;
        sbSrvJoinedAt = now;
        sbSrvSeen.Clear();
        if (room != null && SbIsServerRoom(name)) SbOnServerJoined(room, req);
      }
      if (room != null && SbIsServerRoom(name)) SbCheckServerPasswords(room, now);
      SbTickServerList(room, now);

      // typing from the desktop keyboard into the focused field
      if (sbVisible && sbServerOpen && sbSrvField != 0)
      {
        string typed = typedThisFrame;
        if (!string.IsNullOrEmpty(typed))
          foreach (char ch in typed)
          {
            if (ch == '\b') SbServerKey("BACK");
            else if (ch == '\n' || ch == '\r') SbServerKey("DONE");
            else if (!char.IsControl(ch)) SbServerKey(ch.ToString());
          }
      }

      if (sbSrvStatus != null && now >= sbSrvStatusAt)
      {
        sbSrvStatusAt = now + 0.5f;
        sbSrvStatus.text = SbServerStatusText(room);
      }
    }

    private void SbOnServerJoined(Room room, SbServerPatches.Request req)
    {
      bool mine = req != null && room.Name == req.Code;
      SbServerPatches.Pending = null;
      if (mine && req.Create) sbSrvCreating = false;
      string serverName = SbRoomProp(room, SbServerPatches.PropName);
      // a code with no server behind it: the game made an empty room
      if (string.IsNullOrEmpty(serverName) && room.PlayerCount <= 1)
      {
        this.ShowToast("Servers", "No server with the code " + room.Name);
        SbLeaveToSinglePlayer();
        return;
      }
      string pw = SbRoomProp(room, SbServerPatches.PropPw);
      string myHash = req != null ? req.PwHash ?? string.Empty : string.Empty;
      if (!string.IsNullOrEmpty(pw) && pw != myHash)
      {
        this.ShowToast("Servers", "Wrong password for " + serverName);
        SbLeaveToSinglePlayer();
        return;
      }
      try { PhotonNetwork.LocalPlayer?.SetCustomProperties(new PhotonHashtable { { SbPlayerPwProp, myHash } }); } catch { }
      if (mine) this.ShowToast("Servers", "Joined " + serverName + "  (" + room.Name + ")  " + room.PlayerCount + " / " + room.MaxPlayers);
      SbSetServerNote(string.Empty);
    }

    // the master client sends away anyone without the server's password
    private void SbCheckServerPasswords(Room room, float now)
    {
      if (now < sbSrvCheckAt) return;
      sbSrvCheckAt = now + 1f;
      if (!PhotonNetwork.IsMasterClient) return;
      string pw = SbRoomProp(room, SbServerPatches.PropPw);
      if (string.IsNullOrEmpty(pw)) return;
      foreach (Player p in PhotonNetwork.PlayerListOthers)
      {
        if (p == null) continue;
        float first;
        if (!sbSrvSeen.TryGetValue(p.ActorNumber, out first)) { sbSrvSeen[p.ActorNumber] = now; continue; }
        object v = null;
        string theirs = p.CustomProperties != null && p.CustomProperties.TryGetValue(SbPlayerPwProp, out v) && v != null ? v.ToString() : string.Empty;
        if (theirs == pw) continue;
        // a few seconds to send theirs after joining
        if (now - first < 6f) continue;
        try { PhotonNetwork.CloseConnection(p); } catch { }
        sbSrvSeen[p.ActorNumber] = now + 30f;   // not again straight away
        this.ShowToast("Servers", "Sent " + (p.NickName ?? "a player") + " away (no password)");
      }
    }

    private string SbServerStatusText(Room room)
    {
      if (SbServerBusy()) return "JOINING...";
      if (room == null) return "NOT IN A SERVER";
      if (SbLobbyIndexOf(room.Name) >= 0) return "DEFAULT SERVER  " + room.Name + "\n" + room.PlayerCount + " / " + room.MaxPlayers + " PLAYERS";
      if (!SbIsServerRoom(room.Name)) return "IN A NORMAL GORILLA TAG ROOM (" + room.Name + ")";
      string n = SbRoomProp(room, SbServerPatches.PropName);
      bool locked = !string.IsNullOrEmpty(SbRoomProp(room, SbServerPatches.PropPw));
      bool pub = SbRoomProp(room, SbServerPatches.PropPublic) == "True";
      return (string.IsNullOrEmpty(n) ? "SERVER" : n) + "   CODE " + room.Name + "\n" +
             room.PlayerCount + " / " + room.MaxPlayers + " PLAYERS   " + (pub ? "PUBLIC" : locked ? "PASSWORD" : "PRIVATE") +
             (PhotonNetwork.IsMasterClient ? "   (YOU HOST)" : string.Empty);
    }

    private string sbSrvNote = string.Empty;
    private void SbSetServerNote(string note)
    {
      sbSrvNote = note ?? string.Empty;
      sbSrvStatusAt = 0f;
    }

    // ================================================================ text fields
    private string SbFieldValue(int f)
    {
      switch (f)
      {
        case 1: return sbSrvName;
        case 2: return sbSrvPassword;
        case 3: return sbSrvCode;
        case 4: return sbSrvJoinPw;
      }
      return string.Empty;
    }

    private void SbSetField(int f, string v)
    {
      int max = f == 1 ? 20 : f == 3 ? 9 : 16;
      if (v.Length > max) v = v.Substring(0, max);
      switch (f)
      {
        case 1: sbSrvName = v; break;
        case 2: sbSrvPassword = v; break;
        case 3: sbSrvCode = v.ToUpperInvariant(); break;
        case 4: sbSrvJoinPw = v; break;
      }
      SbRefreshFields();
    }

    private string SbFieldShown(int f)
    {
      string v = SbFieldValue(f);
      string shown = (f == 2 || f == 4) ? new string('*', v.Length) : v;
      if (shown.Length == 0) shown = f == 1 ? "UNKNOWN'S SERVER" : f == 3 ? "_UC..." : "NONE";
      return (sbSrvField == f ? "> " : string.Empty) + shown + (sbSrvField == f ? " _" : string.Empty);
    }

    private void SbRefreshFields()
    {
      foreach (var kv in sbSrvFieldCtls)
        if (kv.Value != null && kv.Value.label != null) kv.Value.label.text = SbFieldShown(kv.Key);
    }

    private void SbServerKey(string key)
    {
      if (sbSrvField == 0) return;
      string v = SbFieldValue(sbSrvField);
      if (key == "BACK") { if (v.Length > 0) SbSetField(sbSrvField, v.Substring(0, v.Length - 1)); }
      else if (key == "CLEAR") SbSetField(sbSrvField, string.Empty);
      else if (key == "DONE") { sbSrvField = 0; SbRefreshFields(); }
      else if (key == "SPACE") SbSetField(sbSrvField, v + " ");
      else SbSetField(sbSrvField, v + key);
    }

    // ================================================================ the page (centre panel)
    private bool sbSrvHosting;   // the SERVER HOST page (settings of the server you host)

    private void SbBuildServers(SbStyle st)
    {
      sbHeaderText.text = sbSrvCreating ? "CREATE SERVER" : sbSrvHosting ? "SERVER HOST" : "SERVERS";
      sbCountText.text = "";
      sbSrvFieldCtls.Clear();
      float y = -6f;
      float w = sbContent.sizeDelta.x;

      Action<string> section = s =>
      {
        TMP_Text sl = SbText(sbContent, s, new Vector2(-w * 0.5f + 150f, y - 16f), new Vector2(280f, 28f), 17f, st.Accent, TextAlignmentOptions.Left);
        SbAnchorTop(sl.rectTransform);
        y -= 40f;
      };
      Action<string, float> note = (text, h) =>
      {
        TMP_Text t = SbText(sbContent, text, new Vector2(0f, y - h * 0.5f), new Vector2(w - 30f, h), 13f, st.Sub, TextAlignmentOptions.TopLeft, FontStyles.Normal, true);
        SbAnchorTop(t.rectTransform);
        y -= h + 8f;
      };

      // making a server and hosting one are pages of their own (header button, top right)
      if (sbSrvCreating || sbSrvHosting)
      {
        bool host = sbSrvHosting;
        if (host)
        {
          section("YOUR SERVER");
          sbSrvStatus = SbText(sbContent, SbServerStatusText(PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom : null), new Vector2(0f, y - 24f), new Vector2(w - 30f, 50f), 15f, st.Text, TextAlignmentOptions.Left, FontStyles.Bold, true);
          SbAnchorTop(sbSrvStatus.rectTransform);
          sbSrvStatusAt = 0f;
          y -= 58f;
        }
        section("NAME");
        SbServerField(st, ref y, "NAME", 1);
        section("WHO CAN JOIN");
        SbSettingToggle(st, ref y, "PUBLIC (NO PASSWORD)", () => sbSrvPublic, () => { sbSrvPublic = !sbSrvPublic; SbRebuildGrid(); });
        SbSettingToggle(st, ref y, "CASUAL (INSTEAD OF INFECTION)", () => sbSrvCasual, () => { sbSrvCasual = !sbSrvCasual; SbRebuildGrid(); });
        if (!sbSrvPublic) SbServerField(st, ref y, "PASSWORD", 2);
        SbSettingToggle(st, ref y, "SHOW IN THE SERVER LIST", () => sbSrvListed, () => { sbSrvListed = !sbSrvListed; });
        SbSettingSlider(st, ref y, "MAX PLAYERS", () => (sbSrvMax - 1) / 19f, v => { sbSrvMax = Mathf.Clamp(1 + Mathf.RoundToInt(v * 19f), 1, 20); }, () => sbSrvMax.ToString());
        y -= 6f;
        SbCtl go = SbMark(SbButton(1, sbContent, host ? "APPLY TO MY SERVER" : "CREATE SERVER", new Vector2(0f, y - 26f), new Vector2(w - 30f, 50f), null, host ? (Action)SbApplyHostSettings : SbCreateServer, 18f));
        go.baseCol = Color.Lerp(st.Card, st.Accent, 0.3f);
        go.hoverCol = Color.Lerp(st.CardHover, st.Accent, 0.45f);
        SbAnchorTop(go.rt);
        y -= 62f;
        if (host) note("Players already inside keep their place on a public server. If you add a password, the people inside need it too (the server sends away anyone without it).", 56f);
        else note("Point at NAME (or PASSWORD): a keyboard comes up under the tablet (your PC keyboard works too). The server gets a code like _UCKXQPRM to give out.", 56f);
        sbContentHeight = -y + 10f;
        sbContent.sizeDelta = new Vector2(sbContent.sizeDelta.x, sbContentHeight);
        sbScrollTarget = Mathf.Clamp(sbScrollTarget, 0f, Mathf.Max(0f, sbContentHeight - SbViewH));
        return;
      }

      // the first page: every server there is
      SbBuildServerListRows(st, ref y, section);

      section("YOU ARE IN");
      sbSrvStatus = SbText(sbContent, SbServerStatusText(PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom : null), new Vector2(0f, y - 24f), new Vector2(w - 30f, 50f), 15f, st.Text, TextAlignmentOptions.Left, FontStyles.Bold, true);
      SbAnchorTop(sbSrvStatus.rectTransform);
      sbSrvStatusAt = 0f;
      y -= 58f;

      section("JOIN WITH A CODE");
      SbServerField(st, ref y, "CODE", 3);
      SbServerField(st, ref y, "PASSWORD", 4);
      float half = (w - 42f) * 0.5f;
      SbCtl join = SbMark(SbButton(1, sbContent, "JOIN WITH CODE", new Vector2(-(half * 0.5f + 6f), y - 24f), new Vector2(half, 46f), null, SbJoinServerByCode, 16f));
      SbAnchorTop(join.rt);
      SbCtl quick = SbMark(SbButton(1, sbContent, "QUICK JOIN PUBLIC", new Vector2(half * 0.5f + 6f, y - 24f), new Vector2(half, 46f), null, SbQuickJoinPublic, 16f));
      SbAnchorTop(quick.rt);
      y -= 58f;
      SbCtl def = SbMark(SbButton(1, sbContent, "JOIN THE DEFAULT SERVER (_SANDBOX)", new Vector2(0f, y - 22f), new Vector2(w - 30f, 40f), () => SbCurrentLobby >= 0, SbJoinSandboxLobby, 15f));
      SbAnchorTop(def.rt);
      y -= 50f;
      note("Click a server to join it. A locked one asks for its password. Only SandBox players can join (the in-game computer cannot type the underscore).", 56f);

      sbContentHeight = -y + 10f;
      sbContent.sizeDelta = new Vector2(sbContent.sizeDelta.x, sbContentHeight);
      sbScrollTarget = Mathf.Clamp(sbScrollTarget, 0f, Mathf.Max(0f, sbContentHeight - SbViewH));
    }

    // CREATE SERVER (top right): a fresh page with the defaults: public, no password, in the list
    private void SbOpenCreatePage()
    {
      sbSrvName = string.Empty; sbSrvPassword = string.Empty;
      sbSrvPublic = true; sbSrvListed = true; sbSrvMax = 10; sbSrvCasual = false;
      sbSrvField = 0; sbSrvHosting = false; sbSrvCreating = true;
      sbScroll = sbScrollTarget = 0f;
      SbRebuildGrid();
    }

    // SERVER HOST (next to the settings icon, only on your own server): the settings it has now
    private void SbOpenHostPage()
    {
      Room room = null;
      try { room = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom : null; } catch { }
      if (room == null || !SbIsServerRoom(room.Name) || !PhotonNetwork.IsMasterClient) { this.ShowToast("Servers", "You are not hosting a server"); return; }
      sbSrvName = SbRoomProp(room, SbServerPatches.PropName);
      sbSrvPublic = SbRoomProp(room, SbServerPatches.PropPublic) == "True";
      sbSrvListed = SbRoomProp(room, SbServerPatches.PropListed) != "False";
      sbSrvMax = Mathf.Clamp(room.MaxPlayers, 1, 20);
      sbSrvPassword = string.Empty;
      sbSrvField = 0; sbSrvCreating = false; sbSrvHosting = true;
      sbServerOpen = true; sbSettingsOpen = false;
      sbScroll = sbScrollTarget = 0f;
      SbRebuildGrid();
    }

    // APPLY TO MY SERVER: the room's name, lock, list mark and size change for everyone in it
    private void SbApplyHostSettings()
    {
      Room room = null;
      try { room = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom : null; } catch { }
      if (room == null || !SbIsServerRoom(room.Name) || !PhotonNetwork.IsMasterClient) { this.ShowToast("Servers", "You are not hosting a server"); return; }
      string pwHash = string.Empty;
      if (!sbSrvPublic)
      {
        string existing = SbRoomProp(room, SbServerPatches.PropPw);
        if (string.IsNullOrEmpty(sbSrvPassword) && string.IsNullOrEmpty(existing)) { this.ShowToast("Servers", "Give it a password, or make it public"); return; }
        pwHash = string.IsNullOrEmpty(sbSrvPassword) ? existing : SbServerHash(room.Name, sbSrvPassword);
      }
      try
      {
        room.SetCustomProperties(new PhotonHashtable
        {
          { SbServerPatches.PropName, SbCleanServerName(sbSrvName) },
          { SbServerPatches.PropPw, pwHash },
          { SbServerPatches.PropPublic, sbSrvPublic },
          { SbServerPatches.PropListed, sbSrvListed }
        });
        room.MaxPlayers = (byte)Mathf.Clamp(sbSrvMax, Mathf.Max(1, room.PlayerCount), 20);
        // the host's own password for the check that sends away people without one
        try { PhotonNetwork.LocalPlayer?.SetCustomProperties(new PhotonHashtable { { SbPlayerPwProp, pwHash } }); } catch { }
        this.ShowToast("Servers", "Server settings changed");
        sbSrvStatusAt = 0f;
      }
      catch (Exception ex) { this.ShowToast("Servers", "Could not change the server: " + ex.Message); }
    }

    // CREATE ROOM (bottom left): a public room with no password, right away
    private void SbQuickCreateRoom()
    {
      string who = string.Empty;
      try { who = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.NickName : string.Empty; } catch { }
      if (string.IsNullOrWhiteSpace(who)) who = "SANDBOX";
      string code = SbNewServerCode();
      SbServerJoin(new SbServerPatches.Request
      {
        Code = code,
        Create = true,
        Name = SbCleanServerName(who + " ROOM"),
        PwHash = string.Empty,
        Max = 10,
        Public = true,
        Listed = true
      }, code);
    }

    private bool sbShiftLower;

    private void SbServerField(SbStyle st, ref float y, string label, int field)
    {
      float w = sbContent.sizeDelta.x;
      TMP_Text l = SbText(sbContent, label, new Vector2(-w * 0.5f + 80f, y - 24f), new Vector2(140f, 30f), 16f, st.Text, TextAlignmentOptions.Left);
      SbAnchorTop(l.rectTransform);
      SbCtl b = SbMark(SbButton(1, sbContent, SbFieldShown(field), new Vector2(70f, y - 24f), new Vector2(w - 170f, 42f), () => sbSrvField == field, () =>
      {
        sbSrvField = sbSrvField == field ? 0 : field;
        SbRefreshFields();
      }, 16f));
      SbAnchorTop(b.rt);
      sbSrvFieldCtls[field] = b;
      y -= 50f;
    }
  }
}
