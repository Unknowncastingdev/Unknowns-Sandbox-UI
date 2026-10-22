using System.Text;
using System.Text.RegularExpressions;
using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnknownsSandbox
{
  // Controller buttons, hands, head and haptics for the tablet.
  public partial class SandboxPlugin
  {
    private enum TabletBind
    {
      RightPrimary = 0,
      LeftPrimary = 1,
      RightSecondary = 2,
      LeftSecondary = 3,
      RightGrip = 4,
      LeftGrip = 5,
      RightTrigger = 6,
      LeftTrigger = 7
    }

    private static string TabletBindName(TabletBind bind)
    {
      switch (bind)
      {
        case TabletBind.RightPrimary: return "Right Primary";
        case TabletBind.LeftPrimary: return "Left Primary";
        case TabletBind.RightSecondary: return "Right Secondary";
        case TabletBind.LeftSecondary: return "Left Secondary";
        case TabletBind.RightGrip: return "Right Grip";
        case TabletBind.LeftGrip: return "Left Grip";
        case TabletBind.RightTrigger: return "Right Trigger";
        default: return "Left Trigger";
      }
    }

    private bool GetTabletBindDown(TabletBind bind)
    {
      ControllerInputPoller p = ControllerInputPoller.instance;
      if (p == null) return false;
      switch (bind)
      {
        case TabletBind.RightPrimary: return p.rightControllerPrimaryButton;
        case TabletBind.LeftPrimary: return p.leftControllerPrimaryButton;
        case TabletBind.RightSecondary: return p.rightControllerSecondaryButton;
        case TabletBind.LeftSecondary: return p.leftControllerSecondaryButton;
        case TabletBind.RightGrip: return p.rightControllerGripFloat > 0.65f;
        case TabletBind.LeftGrip: return p.leftControllerGripFloat > 0.65f;
        case TabletBind.RightTrigger: return p.rightControllerIndexFloat > 0.65f;
        default: return p.leftControllerIndexFloat > 0.65f;
      }
    }

    private bool GetTabletPointerPressed(bool right)
    {
      ControllerInputPoller p = ControllerInputPoller.instance;
      if (p == null) return false;
      return (right ? p.rightControllerIndexFloat : p.leftControllerIndexFloat) > 0.55f;
    }

    private void PulseTabletHaptic(bool right, float strength, float duration)
    {
      try
      {
        GorillaTagger tagger = GorillaTagger.Instance;
        if (tagger != null) tagger.StartVibration(!right, Mathf.Clamp01(strength), Mathf.Max(0.01f, duration));
      }
      catch { }
    }

    private Transform GetTabletHand(bool right)
    {
      VRRig local = GorillaTagger.Instance != null ? GorillaTagger.Instance.offlineVRRig : null;
      if (local == null) return null;
      VRMap map = right ? local.rightHand : local.leftHand;
      return map != null ? map.rigTarget : null;
    }

    private Transform GetTabletHead()
    {
      GTPlayer player = GTPlayer.Instance;
      if (player != null && player.headCollider != null) return player.headCollider.transform;
      VRRig local = GorillaTagger.Instance != null ? GorillaTagger.Instance.offlineVRRig : null;
      if (local == null) return null;
      return local.headMesh != null ? local.headMesh.transform : local.transform;
    }

    private static float PlayerScale => GTPlayer.Instance != null ? GTPlayer.Instance.scale : 1f;

    private static bool IsHeadsetActive()
    {
      try { return UnityEngine.XR.XRSettings.isDeviceActive; }
      catch { return false; }
    }

    // The fingertip is the game's own index-finger trigger, the point its real buttons are
    // pressed with. Null when the game has not made it yet.
    private Vector3? FingerTip(bool right)
    {
      GorillaTagger tagger = GorillaTagger.Instance;
      GameObject trigger = tagger != null ? (right ? tagger.rightHandTriggerCollider : tagger.leftHandTriggerCollider) : null;
      if (trigger == null || !trigger.activeInHierarchy) return null;
      SphereCollider sphere = trigger.GetComponent<SphereCollider>();
      return sphere != null ? trigger.transform.TransformPoint(sphere.center) : trigger.transform.position;
    }

    // Where the fingertip sits in the hand's own space, taken while the fingers are relaxed
    // (trigger and grip let go). The pointer is fixed to the hand at that spot, so curling
    // the finger to pull the trigger or grip doesn't move it.
    private readonly Vector3[] restTip = new Vector3[2];
    private readonly bool[] hasRestTip = new bool[2];

    private void LearnRestTip(bool right, Transform hand)
    {
      ControllerInputPoller p = ControllerInputPoller.instance;
      if (p == null) return;
      float trigger = right ? p.rightControllerIndexFloat : p.leftControllerIndexFloat;
      float grip = right ? p.rightControllerGripFloat : p.leftControllerGripFloat;
      if (trigger > 0.05f || grip > 0.05f) return;
      Vector3? found = FingerTip(right);
      if (!found.HasValue) return;
      Vector3 local = hand.InverseTransformPoint(found.Value);
      if (local.sqrMagnitude < 0.02f * 0.02f) return;
      int h = right ? 1 : 0;
      restTip[h] = hasRestTip[h] ? Vector3.Lerp(restTip[h], local, 0.2f) : local;
      hasRestTip[h] = true;
    }

    // The pointer follows the hand, starts at the tip of the finger and points out of it.
    private bool TryGetTabletFingerPose(bool right, Transform tabletRoot, out Vector3 tip, out Vector3 direction)
    {
      tip = Vector3.zero;
      direction = Vector3.forward;
      Transform hand = GetTabletHand(right);
      if (hand == null) return false;
      LearnRestTip(right, hand);
      int h = right ? 1 : 0;
      if (hasRestTip[h])
      {
        tip = hand.TransformPoint(restTip[h]);
        direction = hand.TransformDirection(restTip[h]).normalized;
      }
      else
      {
        direction = hand.forward;
        tip = hand.position + direction * 0.105f;
      }
      return true;
    }

    private bool TryGetStableTabletPointerPose(bool right, Transform tabletRoot, out Vector3 origin, out Vector3 direction)
    {
      return TryGetTabletFingerPose(right, tabletRoot, out origin, out direction);
    }

    // Typed characters from the desktop keyboard, collected by the Input System and read once a frame.
    private readonly StringBuilder typedText = new StringBuilder();
    private Keyboard typedFrom;

    private string TakeTypedText()
    {
      Keyboard kb = Keyboard.current;
      if (kb != typedFrom)
      {
        if (typedFrom != null) typedFrom.onTextInput -= OnTextInput;
        typedFrom = kb;
        if (kb != null) kb.onTextInput += OnTextInput;
      }
      if (kb != null)
      {
        if (kb.backspaceKey.wasPressedThisFrame) typedText.Append('\b');
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) typedText.Append('\n');
      }
      string s = typedText.ToString();
      typedText.Length = 0;
      return s;
    }

    private void OnTextInput(char c)
    {
      if (!char.IsControl(c)) typedText.Append(c);
    }

    private static readonly Regex BadWords = new Regex(
      @"\b(f+u+c+k+\w*|sh+i+t+\w*|b+i+t+c+h+\w*|c+u+n+t+\w*|d+i+c+k+\w*|p+u+s+s+y+|a+s+s+h+o+l+e+\w*|bastard\w*|wh+o+r+e+\w*|sl+u+t+\w*|motherf\w*|n+i+g+g+\w*|f+a+g+\w*|retard\w*|k+y+s+|kill\s+yourself|rape\w*|cock\w*|twat\w*|wank\w*|spastic|tranny\w*)\b",
      RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Server names are shown to everyone, so swear words are starred out.
    private static string CensorWords(string text)
    {
      if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
      return BadWords.Replace(text, m => m.Value[0] + new string('*', m.Value.Length - 1));
    }
  }
}
