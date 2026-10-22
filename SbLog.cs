using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace UnknownsSandbox
{
  // Warnings go to the BepInEx log. The same message is written at most once every
  // ten seconds, so a problem inside a per-frame update can't flood the log.
  internal static class SbLog
  {
    private static readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("Unknowns Sandbox");
    private static readonly Dictionary<string, float> lastWritten = new Dictionary<string, float>();

    internal static void Info(string message) => log.LogInfo(message);

    internal static void Warn(string message)
    {
      float now = Time.realtimeSinceStartup;
      if (lastWritten.TryGetValue(message, out float last) && now - last < 10f) return;
      lastWritten[message] = now;
      log.LogWarning(message);
    }

    internal static void Throttled(string section, Exception ex)
    {
      float now = Time.realtimeSinceStartup;
      if (lastWritten.TryGetValue(section, out float last) && now - last < 10f) return;
      lastWritten[section] = now;
      log.LogWarning(section + " failed: " + ex);
    }
  }
}
