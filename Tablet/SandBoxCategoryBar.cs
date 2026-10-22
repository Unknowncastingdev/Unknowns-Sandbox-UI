using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UnknownsSandbox
{
  // the category bar over the item grid (Minecraft, Objects and Weapons): ALL,
  // then one chip per section of that tab (BLOCKS, WOOD, MOBS, GUNS ...). Only the
  // chips that fit are shown; the arrows page through the rest.
  public partial class SandboxPlugin
  {
    private const float SbViewH = 430f, SbViewY = -52f;
    private RectTransform sbCatBar;
    private string sbCatFilter;     // null = ALL
    private int sbCatPage;

    private void SbClearCategoryBar()
    {
      for (int i = sbCtls.Count - 1; i >= 0; i--) if (sbCtls[i].catBar) sbCtls.RemoveAt(i);
      if (sbCatBar == null) return;
      for (int i = sbCatBar.childCount - 1; i >= 0; i--) Destroy(sbCatBar.GetChild(i).gameObject);
    }

    private static float SbChipWidth(string text) { return 22f + text.Length * 9.5f; }

    private void SbBuildCategoryBar(SbStyle st, List<SbItem> items)
    {
      if (sbCatBar == null) return;
      // the sections of this tab, in grid order
      var sections = new List<string>();
      foreach (SbItem i in items) if (!string.IsNullOrEmpty(i.Section) && !sections.Contains(i.Section)) sections.Add(i.Section);
      if (sbCatFilter != null && !sections.Contains(sbCatFilter)) sbCatFilter = null;
      var chips = new List<string> { null };
      chips.AddRange(sections);

      // pages of chips that fit between the arrows
      float barW = sbCatBar.sizeDelta.x, room = barW - 2f * 34f, gap = 6f;
      var pages = new List<List<string>>();
      var page = new List<string>();
      float used = 0f;
      foreach (string c in chips)
      {
        float cw = SbChipWidth(c ?? "ALL");
        if (page.Count > 0 && used + cw > room) { pages.Add(page); page = new List<string>(); used = 0f; }
        page.Add(c);
        used += cw + gap;
      }
      if (page.Count > 0) pages.Add(page);
      // the page with the picked chip on it, unless the arrows moved off it
      sbCatPage = Mathf.Clamp(sbCatPage, 0, pages.Count - 1);

      float x = -room * 0.5f;
      foreach (string c in pages[sbCatPage])
      {
        string chip = c;
        string label = chip ?? "ALL";
        float cw = SbChipWidth(label);
        SbCtl b = SbButton(1, sbCatBar, label, new Vector2(x + cw * 0.5f, 0f), new Vector2(cw, 30f), () => sbCatFilter == chip, () =>
        {
          if (sbCatFilter == chip) return;
          sbCatFilter = chip;
          sbScroll = sbScrollTarget = 0f;
          SbRebuildGrid();
        }, 13f);
        b.catBar = true;
        x += cw + gap;
      }
      if (pages.Count > 1)
      {
        SbCtl l = SbButton(1, sbCatBar, "<", new Vector2(-barW * 0.5f + 15f, 0f), new Vector2(30f, 30f), null, () =>
        {
          sbCatPage = (sbCatPage + pages.Count - 1) % pages.Count;
          SbRebuildGrid();
        }, 16f);
        l.catBar = true;
        SbCtl r = SbButton(1, sbCatBar, ">", new Vector2(barW * 0.5f - 15f, 0f), new Vector2(30f, 30f), null, () =>
        {
          sbCatPage = (sbCatPage + 1) % pages.Count;
          SbRebuildGrid();
        }, 16f);
        r.catBar = true;
      }
    }
  }
}
