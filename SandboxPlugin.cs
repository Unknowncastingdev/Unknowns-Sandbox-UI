using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using GorillaLocomotion;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnknownsSandbox
{
  [BepInPlugin(Guid, "Unknowns Sandbox", "1.0.0")]
  [BepInDependency("org.legoandmars.gorillatag.utilla", BepInDependency.DependencyFlags.HardDependency)]
  public partial class SandboxPlugin : BaseUnityPlugin
  {
    public const string Guid = "com.unknowns.sandbox";

    internal static SandboxPlugin Instance;

    private ConfigEntry<Key> windowKey, tabletKey;
    private bool sbTabletEnabled = true;
    private string typedThisFrame = string.Empty;

    private void Awake()
    {
      Instance = this;
      windowKey = Config.Bind("Keys", "WindowKey", Key.F6, "Opens and closes the SandBox desktop window.");
      tabletKey = Config.Bind("Keys", "TabletKey", Key.None, "Opens and closes the SandBox tablet from the keyboard (None = only the VR button).");
      // The tablet switch is on unless you turn it off here. An earlier "off" saved by the
      // casting mod is cleared once, so the tablet starts on.
      if (PlayerPrefs.GetInt("UnknownsSandbox_TabletOnByDefault", 0) == 0)
      {
        PlayerPrefs.SetInt("UnknownsTabletOn_SandBox", 1);
        PlayerPrefs.SetInt("UnknownsSandbox_TabletOnByDefault", 1);
      }
      sbTabletEnabled = PlayerPrefs.GetInt("UnknownsTabletOn_SandBox", 1) == 1;

      var harmony = new Harmony(Guid);
      SbTapSounds.Install(harmony);
      SbServerPatches.Install(harmony);
      try
      {
        MethodInfo left = AccessTools.Method(typeof(NetworkSystem), "PlayerLeft");
        if (left != null) harmony.Patch(left, prefix: new HarmonyMethod(typeof(SandboxPlugin), nameof(PlayerLeftPrefix)));
      }
      catch (Exception ex) { SbLog.Warn("Could not hook players leaving: " + ex.Message); }
    }

    private void Update()
    {
      typedThisFrame = TakeTypedText();
      if (WindowTyping) typedThisFrame = string.Empty;

      if (PollCapture()) return;

      Keyboard kb = Keyboard.current;
      if (kb != null && !WindowTyping && windowKey.Value != Key.None && kb[windowKey.Value].wasPressedThisFrame) ToggleWindow();

      // the game's player and rig have to be there first
      if (GorillaTagger.Instance == null || GTPlayer.Instance == null) return;
      try { UpdateSandBoxTablet(); }
      catch (Exception ex) { SbLog.Throttled("SandBox tablet", ex); }

      if (sbRefreshSettingsPage && !SbGui.SliderHeld && sbDragCtl == null)
      {
        sbRefreshSettingsPage = false;
        if (sbSettingsOpen && sbContent != null) SbRebuildGrid();
      }
    }

    private bool TabletKeyPressed()
    {
      Keyboard kb = Keyboard.current;
      return kb != null && !WindowTyping && tabletKey.Value != Key.None && kb[tabletKey.Value].wasPressedThisFrame;
    }

    private static void PlayerLeftPrefix(NetPlayer netPlayer)
    {
      Instance?.SbOnPlayerLeaving(netPlayer);
    }
  }
}
