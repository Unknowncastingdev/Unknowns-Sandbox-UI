using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UnknownsSandbox
{
  // "Click, then press the one you want": the tablet's open / close input is set by pressing
  // it, a keyboard key or a VR controller button, whichever you press. Both can be set and
  // both work at the same time.
  public partial class SandboxPlugin
  {
    private enum Capture { None, Tablet, WindowKey }

    private Capture capturing;
    private bool captureReady;   // everything was let go once, so the click that started it isn't taken

    private void StartCapture(Capture what)
    {
      capturing = capturing == what ? Capture.None : what;
      captureReady = false;
    }

    // Runs every frame. True while it is waiting, so the key being picked doesn't also open things.
    private bool PollCapture()
    {
      if (capturing == Capture.None) return false;
      Keyboard kb = Keyboard.current;
      if (kb != null && kb.escapeKey.wasPressedThisFrame)
      {
        capturing = Capture.None;
        return true;
      }

      if (capturing == Capture.Tablet)
      {
        int button = -1;
        for (int i = 0; i < 8 && button < 0; i++)
          if (GetTabletBindDown((TabletBind)i)) button = i;
        if (button < 0) captureReady = true;
        else if (captureReady)
        {
          sbOpenBind = button;
          capturing = Capture.None;
          SettingChanged();
          return true;
        }
        if (kb != null && kb.deleteKey.wasPressedThisFrame)
        {
          tabletKey.Value = Key.None;
          capturing = Capture.None;
          return true;
        }
        PickKey(kb, tabletKey);
        return true;
      }

      // the window key can't be cleared, or there'd be no way back to this window
      if (kb != null && kb.deleteKey.wasPressedThisFrame) capturing = Capture.None;
      else PickKey(kb, windowKey);
      return true;
    }

    private bool PickKey(Keyboard kb, ConfigEntry<Key> entry)
    {
      if (kb == null) return false;
      foreach (KeyControl key in kb.allKeys)
      {
        if (key.keyCode == Key.None || key.keyCode == Key.Delete || !key.wasPressedThisFrame) continue;
        entry.Value = key.keyCode;
        capturing = Capture.None;
        return true;
      }
      return false;
    }

    private static string KeyName(Key key)
    {
      return key == Key.None ? "NONE" : key.ToString().ToUpperInvariant();
    }

    // "LEFT PRIMARY + T": the controller button and the key that open the tablet
    private string TabletInputName()
    {
      string button = TabletBindName((TabletBind)sbOpenBind).ToUpperInvariant();
      return tabletKey.Value == Key.None ? button : button + " + " + KeyName(tabletKey.Value);
    }
  }
}
