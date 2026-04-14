# AnchoredOffsetLines Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a single-file NinjaTrader 8 C# indicator that draws two offset lines that track live price, then freeze to the close of the 09:29:59 bar between 09:30 and 09:35 ET as a persistent daily bracket.

**Architecture:** One class (`AnchoredOffsetLines : Indicator`), one source file (`AnchoredOffsetLines.cs`), built incrementally. Each task leaves the file in a compiling, loadable state on a NinjaTrader chart and is verified manually by loading on a chart and observing the expected visible behavior. No unit tests (NT8 has no standard unit-test harness for indicators).

**Tech Stack:** C# targeting .NET Framework 4.8 (the NinjaTrader 8 runtime), NinjaScript API, NT8 `Draw.Line` drawing tools.

---

## Developer Environment Notes

- **Dev machine:** macOS. Source of truth lives at `/Users/fearcutter/projects/930-ORB-NT8/AnchoredOffsetLines.cs` under git.
- **Run/test machine:** Windows running NinjaTrader 8. The `.cs` file must be copied into `Documents/NinjaTrader 8/bin/Custom/Indicators/` on the Windows machine, then compiled via the NinjaScript Editor (F5) before it can be added to a chart. Use whatever transport you have (shared folder, rsync, Dropbox, git pull from the Windows side, etc.).
- **Spec:** `docs/superpowers/specs/2026-04-14-anchored-offset-lines-design.md`.
- **Verification pattern used at every task:** "Deploy + compile + visually verify on chart" — described once in Task 1 and referenced afterwards.

---

## File Structure

Single file: `AnchoredOffsetLines.cs` at the root of the project directory.

Nothing else. No helper classes, no additional files, no external dependencies.

---

## Task 1: Scaffold a minimal compiling indicator

Create the file with just enough boilerplate that it compiles in NinjaTrader, can be added to a chart, and does nothing visible yet.

**Files:**
- Create: `AnchoredOffsetLines.cs`

- [ ] **Step 1: Create the file with minimal boilerplate**

Write the following to `AnchoredOffsetLines.cs`:

```csharp
#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class AnchoredOffsetLines : Indicator
    {
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Draws two lines offset above and below a reference price. Before 09:30 and after 09:35 ET the reference is the current bar's close. Between 09:30 and 09:35 ET the reference is locked to the close of the 09:29:59 bar and the segment persists as a daily bracket.";
                Name = "AnchoredOffsetLines";
                Calculate = Calculate.OnPriceChange;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = true;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = true;
            }
        }

        protected override void OnBarUpdate()
        {
            // Intentionally empty — Task 1 scaffold.
        }
    }
}
```

- [ ] **Step 2: Deploy to NinjaTrader and compile**

On the Windows NT8 machine:

1. Copy `AnchoredOffsetLines.cs` to `Documents/NinjaTrader 8/bin/Custom/Indicators/AnchoredOffsetLines.cs` (overwriting any previous version).
2. In NinjaTrader, open **Tools → NinjaScript Editor**.
3. Open `AnchoredOffsetLines.cs` inside the editor.
4. Press **F5** to compile.

Expected: "Compile successful" message at the bottom. No red error underlines. No warnings.

- [ ] **Step 3: Verify the indicator loads on a chart**

1. Open any intraday chart (e.g., NQ 1-minute).
2. Right-click the chart → **Indicators…**.
3. Find **AnchoredOffsetLines** in the list → Add → OK.

Expected: the indicator appears in the "Active" list for the chart. No error dialogs. No visible drawing on the chart yet (this is correct — the scaffold draws nothing).

- [ ] **Step 4: Commit**

```bash
git add AnchoredOffsetLines.cs
git commit -m "Task 1: scaffold minimal AnchoredOffsetLines indicator"
```

---

## Task 2: User-configurable properties + tracking-mode drawing

Add every user-facing property and implement Tracking Mode drawing: two lines at `Close[0] ± Offset` that follow live price and extend forward by `LineExtensionBars` bars. At the end of this task the indicator visually behaves like a simple "live offset channel" that tracks the current bar all day with no anchor logic yet.

**Files:**
- Modify: `AnchoredOffsetLines.cs`

- [ ] **Step 1: Add the property backing fields and the tag constants**

Inside the `AnchoredOffsetLines` class, above `OnStateChange`, add:

```csharp
        private const string TrackingUpperTag = "TrackingUpper";
        private const string TrackingLowerTag = "TrackingLower";
```

- [ ] **Step 2: Add the public properties at the bottom of the class**

Inside the class, below `OnBarUpdate`, add a properties region:

```csharp
        #region Properties
        [NinjaScriptProperty]
        [Range(0.0, double.MaxValue)]
        [Display(Name = "Offset (points)", GroupName = "Parameters", Order = 0)]
        public double Offset { get; set; }

        [XmlIgnore]
        [Display(Name = "Upper line color", GroupName = "Visual", Order = 1)]
        public Brush UpperLineBrush { get; set; }

        [Browsable(false)]
        public string UpperLineBrushSerializable
        {
            get { return Serialize.BrushToString(UpperLineBrush); }
            set { UpperLineBrush = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Lower line color", GroupName = "Visual", Order = 2)]
        public Brush LowerLineBrush { get; set; }

        [Browsable(false)]
        public string LowerLineBrushSerializable
        {
            get { return Serialize.BrushToString(LowerLineBrush); }
            set { LowerLineBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Line width", GroupName = "Visual", Order = 3)]
        public int LineWidth { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Line dash style", GroupName = "Visual", Order = 4)]
        public DashStyleHelper LineDashStyle { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Line extension bars", GroupName = "Parameters", Order = 5)]
        public int LineExtensionBars { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Anchor close time (ET)", GroupName = "Parameters", Order = 6)]
        public TimeSpan AnchorCloseTime { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Release close time (ET)", GroupName = "Parameters", Order = 7)]
        public TimeSpan ReleaseCloseTime { get; set; }
        #endregion
```

- [ ] **Step 3: Set property defaults in `State.SetDefaults`**

Inside `OnStateChange`, append the following lines to the `State == State.SetDefaults` block (after the existing `IsSuspendedWhileInactive = true;` line):

```csharp
                Offset = 10.0;
                UpperLineBrush = Brushes.DodgerBlue;
                LowerLineBrush = Brushes.Red;
                LineWidth = 2;
                LineDashStyle = DashStyleHelper.Solid;
                LineExtensionBars = 5;
                AnchorCloseTime = new TimeSpan(9, 30, 0);
                ReleaseCloseTime = new TimeSpan(9, 35, 0);
```

- [ ] **Step 4: Implement tracking-mode drawing in `OnBarUpdate`**

Replace the body of `OnBarUpdate` with:

```csharp
            if (CurrentBar < 0) return;

            double refPrice = Close[0];
            double upper = refPrice + Offset;
            double lower = refPrice - Offset;

            Draw.Line(this, TrackingUpperTag, false,
                      0, upper,
                      -LineExtensionBars, upper,
                      UpperLineBrush, LineDashStyle, LineWidth);

            Draw.Line(this, TrackingLowerTag, false,
                      0, lower,
                      -LineExtensionBars, lower,
                      LowerLineBrush, LineDashStyle, LineWidth);
```

- [ ] **Step 5: Deploy, compile, and visually verify**

Follow the same "copy → F5 → compile successful" flow from Task 1 Step 2, then:

1. Remove the existing AnchoredOffsetLines from the chart (right-click → Indicators → Remove) and re-add it.
2. Observe the chart.

Expected:
- Two horizontal lines appear near the current bar: one Dodger Blue about 10 points above the last close, one Red about 10 points below.
- Each line starts at the current bar and extends 5 bars into the future (to the right of the current bar).
- As new ticks arrive, the lines slide up and down with live price.
- Open the indicator settings (right-click chart → Indicators → AnchoredOffsetLines → click the row): verify **Offset**, **Upper line color**, **Lower line color**, **Line width**, **Line dash style**, **Line extension bars**, **Anchor close time (ET)**, **Release close time (ET)** all appear with the defaults listed in the spec. Change Offset to 20 and click OK → lines redraw at ±20 points.

- [ ] **Step 6: Commit**

```bash
git add AnchoredOffsetLines.cs
git commit -m "Task 2: add properties and tracking-mode drawing"
```

---

## Task 3: Eastern Time helper, state fields, daily reset

Add the internal state needed for phase logic — timezone cache, phase enum, anchor fields, daily reset — and wire up the ET conversion helper. No phase transitions yet; this task just adds the scaffolding. Visual behavior is unchanged from Task 2.

**Files:**
- Modify: `AnchoredOffsetLines.cs`

- [ ] **Step 1: Add the state field declarations**

Inside the `AnchoredOffsetLines` class, below the existing tag constants from Task 2, add:

```csharp
        private enum Phase { Tracking, Anchored }

        private Phase phase;
        private double? anchorPrice;
        private int anchorBarIndex;
        private string anchorDayKey;
        private DateTime lastTradingDay;
        private DateTime lastProcessedBarEt;
        private TimeZoneInfo easternTz;
```

- [ ] **Step 2: Initialize state in `State.Configure`**

Inside `OnStateChange`, add a new `else if` branch after the existing `State == State.SetDefaults` block:

```csharp
            else if (State == State.Configure)
            {
                try
                {
                    easternTz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                }
                catch
                {
                    easternTz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
                }

                phase = Phase.Tracking;
                anchorPrice = null;
                anchorBarIndex = -1;
                anchorDayKey = null;
                lastTradingDay = DateTime.MinValue;
                lastProcessedBarEt = DateTime.MinValue;
            }
```

The `try/catch` handles both Windows (which uses the `"Eastern Standard Time"` ID and automatically covers EST/EDT) and any rare edge case where NT8 is running on Mono/Linux and the IANA ID is required instead.

- [ ] **Step 3: Deploy, compile, verify no regression**

Follow the deploy + compile flow. Remove and re-add the indicator. Expected: identical behavior to end of Task 2 — two live tracking lines. No errors, no warnings, no visible change.

- [ ] **Step 4: Commit**

```bash
git add AnchoredOffsetLines.cs
git commit -m "Task 3: add state fields and Eastern Time cache"
```

---

## Task 4: Closed-bar evaluation skeleton (historical + realtime branches)

Add the plumbing that identifies "a bar just closed" in both historical and realtime, and runs the per-day reset. No anchor capture yet; this task is a surgical refactor of `OnBarUpdate` to split "closed-bar event" processing from drawing. At the end, the indicator still shows only tracking lines, but has the hooks the next tasks need.

**Files:**
- Modify: `AnchoredOffsetLines.cs`

- [ ] **Step 1: Rewrite `OnBarUpdate` with the evaluate-then-draw structure**

Replace the entire body of `OnBarUpdate` with:

```csharp
            if (CurrentBar < 0) return;

            // --- Identify whether a bar-close event occurred on this update. ---
            bool processClosedBar = false;
            DateTime closedBarTime = DateTime.MinValue;

            if (State == State.Historical)
            {
                processClosedBar = true;
                closedBarTime = Time[0];
            }
            else if (State == State.Realtime && IsFirstTickOfBar && CurrentBar >= 1)
            {
                processClosedBar = true;
                closedBarTime = Time[1];
            }

            // --- Per-day reset runs on every closed bar. Phase transitions land here in Tasks 5 & 6. ---
            if (processClosedBar)
            {
                DateTime etClose = TimeZoneInfo.ConvertTime(closedBarTime, easternTz);

                if (etClose.Date != lastTradingDay)
                {
                    lastTradingDay = etClose.Date;
                    phase = Phase.Tracking;
                    anchorPrice = null;
                    anchorBarIndex = -1;
                    anchorDayKey = null;
                    lastProcessedBarEt = DateTime.MinValue;
                }

                lastProcessedBarEt = etClose;
            }

            // --- Drawing (phase-specific logic added in later tasks). ---
            // For now, always draw tracking lines — this preserves Task 2 behavior.
            double refPrice = Close[0];
            double upper = refPrice + Offset;
            double lower = refPrice - Offset;

            Draw.Line(this, TrackingUpperTag, false,
                      0, upper,
                      -LineExtensionBars, upper,
                      UpperLineBrush, LineDashStyle, LineWidth);

            Draw.Line(this, TrackingLowerTag, false,
                      0, lower,
                      -LineExtensionBars, lower,
                      LowerLineBrush, LineDashStyle, LineWidth);
```

Task 5 will expand the `State.Historical` / `State.Realtime` branches to also capture `closedBarClose` and `closedBarIdx` for use in the anchor trigger.

- [ ] **Step 2: Deploy, compile, verify no regression**

Deploy + F5. Expected: compile successful, no warnings. Remove/re-add indicator on an intraday chart. Expected visual: identical to end of Task 2 — two live tracking lines. No errors.

- [ ] **Step 3: Commit**

```bash
git add AnchoredOffsetLines.cs
git commit -m "Task 4: split OnBarUpdate into closed-bar evaluation and draw"
```

---

## Task 5: Anchor capture + anchored-segment drawing + hide tracking lines

Implement the Tracking → Anchored transition and the day-keyed anchored-segment drawing. When the first closed bar whose ET close is at-or-past `AnchorCloseTime` is processed (and the prior bar today was strictly before `AnchorCloseTime`), capture the anchor, flip phase, remove the tracking-line draw objects, and start drawing the anchored segment with day-keyed tags. The segment grows from the anchor bar to the currently-processing bar on every subsequent update. Release is **not** implemented yet — the indicator will stay anchored from 09:30 onward in this task, which is expected.

**Files:**
- Modify: `AnchoredOffsetLines.cs`

- [ ] **Step 1: Expand the closed-bar capture to include price and bar index**

Inside `OnBarUpdate`, replace the closed-bar identification block (the one added in Task 4 that declares `processClosedBar` and `closedBarTime`) with:

```csharp
            bool processClosedBar = false;
            DateTime closedBarTime = DateTime.MinValue;
            double closedBarClose = 0.0;
            int closedBarIdx = -1;

            if (State == State.Historical)
            {
                processClosedBar = true;
                closedBarTime = Time[0];
                closedBarClose = Close[0];
                closedBarIdx = CurrentBar;
            }
            else if (State == State.Realtime && IsFirstTickOfBar && CurrentBar >= 1)
            {
                processClosedBar = true;
                closedBarTime = Time[1];
                closedBarClose = Close[1];
                closedBarIdx = CurrentBar - 1;
            }
```

- [ ] **Step 2: Insert the anchor-trigger block inside the closed-bar handler**

Inside `OnBarUpdate`, inside the existing `if (processClosedBar)` block, **after** the daily-reset block and **before** the `lastProcessedBarEt = etClose;` line, add:

```csharp
                bool havePrevToday =
                    lastProcessedBarEt != DateTime.MinValue &&
                    lastProcessedBarEt.Date == etClose.Date;

                TimeSpan prevTod = havePrevToday ? lastProcessedBarEt.TimeOfDay : TimeSpan.MinValue;
                TimeSpan curTod = etClose.TimeOfDay;

                if (phase == Phase.Tracking
                    && havePrevToday
                    && prevTod < AnchorCloseTime
                    && curTod >= AnchorCloseTime)
                {
                    anchorPrice = closedBarClose;
                    anchorBarIndex = closedBarIdx;
                    anchorDayKey = etClose.ToString("yyyyMMdd");
                    phase = Phase.Anchored;

                    RemoveDrawObject(TrackingUpperTag);
                    RemoveDrawObject(TrackingLowerTag);
                }
```

The `havePrevToday` check is the mid-day-load safeguard described in spec §3.4 — if the chart's loaded history doesn't include a pre-09:30 bar today, the anchor trigger cannot fire.

- [ ] **Step 3: Replace the drawing block with a phase-conditional draw**

Inside `OnBarUpdate`, replace the existing drawing block (the final block that calls `Draw.Line` twice for `TrackingUpperTag` / `TrackingLowerTag`) with:

```csharp
            if (phase == Phase.Anchored && anchorPrice.HasValue && anchorBarIndex >= 0 && anchorDayKey != null)
            {
                int startBarsAgo = CurrentBar - anchorBarIndex;
                int endBarsAgo = 0;

                double upperA = anchorPrice.Value + Offset;
                double lowerA = anchorPrice.Value - Offset;

                string upTag = "AnchorUpper_" + anchorDayKey;
                string loTag = "AnchorLower_" + anchorDayKey;

                Draw.Line(this, upTag, false,
                          startBarsAgo, upperA,
                          endBarsAgo, upperA,
                          UpperLineBrush, LineDashStyle, LineWidth);

                Draw.Line(this, loTag, false,
                          startBarsAgo, lowerA,
                          endBarsAgo, lowerA,
                          LowerLineBrush, LineDashStyle, LineWidth);
            }
            else
            {
                double refPrice = Close[0];
                double upper = refPrice + Offset;
                double lower = refPrice - Offset;

                Draw.Line(this, TrackingUpperTag, false,
                          0, upper,
                          -LineExtensionBars, upper,
                          UpperLineBrush, LineDashStyle, LineWidth);

                Draw.Line(this, TrackingLowerTag, false,
                          0, lower,
                          -LineExtensionBars, lower,
                          LowerLineBrush, LineDashStyle, LineWidth);
            }
```

- [ ] **Step 4: Deploy, compile, visually verify**

Deploy + F5. Expected: compile successful.

Add the indicator to a 1-minute chart of NQ or ES. If the chart is showing today's session and the morning included 09:30 ET:

Expected visual:
- Before the 09:30 bar (historical or live): two tracking lines follow live price, extending 5 bars to the right.
- On the 09:30 bar: the anchor triggers. Tracking lines disappear. A horizontal Dodger Blue line appears 10 points above the 09:30 close, and a Red line 10 points below. Both start at the 09:30 bar.
- On each subsequent bar (09:31, 09:32, …): the anchored segment grows further right, ending at the current bar.
- Because Task 5 does not implement release yet, the segment keeps growing past 09:35 and never flips back to tracking — this is expected and will be fixed in Task 6. Do not be alarmed.

If the loaded chart does not include the 09:30 ET bar for any visible day, the anchor will not fire and only tracking lines will be visible — also correct.

- [ ] **Step 5: Commit**

```bash
git add AnchoredOffsetLines.cs
git commit -m "Task 5: implement anchor trigger and day-keyed anchored drawing"
```

---

## Task 6: Release-after-draw + final release-bar pinning

Implement the Anchored → Tracking transition. The release must happen *after* drawing so the release bar still renders as the final bar of the anchored segment. The anchored segment's right endpoint must be pinned to the release bar on the release update (not extend to the next bar). On the next closed bar after release, the indicator is in Tracking mode and draws live tracking lines alongside the persisted anchored segment.

**Files:**
- Modify: `AnchoredOffsetLines.cs`

- [ ] **Step 1: Declare the release flags at the top of `OnBarUpdate`**

Inside `OnBarUpdate`, immediately below the `if (CurrentBar < 0) return;` line, add:

```csharp
            bool releasePending = false;
            int releaseBarIdx = -1;
```

- [ ] **Step 2: Add release detection inside the closed-bar evaluation**

Inside `OnBarUpdate`, immediately **after** the anchor-trigger block added in Task 5 (still inside the `if (processClosedBar)` block, before the `lastProcessedBarEt = etClose;` line), add:

```csharp
                if (phase == Phase.Anchored && curTod >= ReleaseCloseTime)
                {
                    releasePending = true;
                    releaseBarIdx = closedBarIdx;
                }
```

- [ ] **Step 3: Pin the anchored segment end-point on release**

Inside `OnBarUpdate`, in the drawing block, replace the line:

```csharp
                int endBarsAgo = 0;
```

with:

```csharp
                int endBarsAgo = releasePending ? (CurrentBar - releaseBarIdx) : 0;
```

This ensures that on the single update where release fires, the anchored segment is drawn with its right endpoint at the release bar (not one bar further right at the still-in-progress next bar).

- [ ] **Step 4: Apply the state transition after drawing**

Inside `OnBarUpdate`, immediately **after** the drawing block (the closing brace of the `else` for tracking), add:

```csharp
            if (releasePending)
            {
                phase = Phase.Tracking;
                anchorPrice = null;
                anchorBarIndex = -1;
                anchorDayKey = null;
            }
```

- [ ] **Step 5: Deploy, compile, visually verify**

Deploy + F5.

Expected visual on a 1-minute chart covering today's full morning:
- Tracking lines before 09:30.
- Anchored segment appears on the 09:30 bar, grows across 09:31, 09:32, 09:33, 09:34, and **pins** at the 09:35 bar. The segment's right edge sits on the 09:35 bar, not the 09:36 bar.
- From the 09:36 bar onward: the anchored segment remains visible as a frozen horizontal bracket from 09:30 to 09:35, AND two live tracking lines reappear around the current bar.
- As the session continues, the tracking lines follow live price; the 09:30–09:35 bracket stays put.

- [ ] **Step 6: Multi-day chart verification**

Switch the chart to a 5-day, 1-minute view (or any multi-day intraday view that includes multiple morning opens).

Expected: each day has its own 09:30–09:35 anchored bracket, all visible simultaneously. The current day also shows live tracking lines at the current bar.

- [ ] **Step 7: Commit**

```bash
git add AnchoredOffsetLines.cs
git commit -m "Task 6: implement release with after-draw transition"
```

---

## Task 7: Cross-timeframe verification + final polish

No new code by default. This task is the final verification sweep across the timeframes, edge cases, and settings listed in the spec. If any verification fails, this task also includes the fix steps for the specific failure modes most likely to show up.

**Files:**
- Modify (only if a verification step fails): `AnchoredOffsetLines.cs`

- [ ] **Step 1: Verify on a 15-second chart**

Load a 15-second chart of NQ covering today's open. Remove any prior version of AnchoredOffsetLines from the chart and re-add it.

Expected: the anchor fires on the bar that closes at exactly 09:30:00 (i.e., the 09:29:45–09:30:00 bar). The anchored segment spans roughly 20 bars (09:30:00 through 09:35:00). Release pins at the 09:35:00 bar. Tracking lines reappear on the 09:35:15 bar.

- [ ] **Step 2: Verify on a 5-minute chart**

Load a 5-minute chart for the same session.

Expected: the anchor fires on the 09:30 bar (the 09:25:00 → 09:30:00 bar). The anchored segment is a single 5-minute bar wide — the 09:30 → 09:35 bar IS the release bar. Tracking lines resume on the 09:40 bar.

- [ ] **Step 3: Verify on a 3-minute chart (odd-timeframe fallback)**

Load a 3-minute chart for the same session.

Expected: there is no bar that closes exactly at 09:30:00. The anchor fires on the first bar whose close is strictly past 09:30:00 (most likely the 09:28 → 09:31 bar, closing at 09:31:00). Similarly, release fires on the first bar past 09:35:00. The indicator does not silently go dormant.

- [ ] **Step 4: Verify chart in non-Eastern timezone**

Right-click the chart → **Properties → Time Zone** → set to Pacific Standard Time. Reload the chart.

Expected: the anchored bracket still appears at the bars representing 09:30–09:35 Eastern Time — which will be displayed as 06:30–06:35 Pacific Time on the chart's time axis. The anchor/release logic is driven by ET regardless of the chart's display timezone.

Reset the chart timezone to whatever default you normally use.

- [ ] **Step 5: Verify settings change mid-session**

With the indicator loaded on a live 1-minute chart during tracking mode:

1. Right-click the chart → Indicators → AnchoredOffsetLines → change **Offset** from 10 to 25 → OK.

Expected: the tracking lines immediately redraw at ±25 points. The morning's anchored segment also redraws at ±25 points from the anchor price (NinjaTrader recalculates the indicator from scratch when settings change).

2. Change **Line width** to 4, **Upper line color** to Lime, **Lower line color** to Magenta, **Line dash style** to Dash → OK.

Expected: all visible lines (tracking + any anchored segments) redraw with the new styling.

3. Reset the settings to defaults (Offset 10, width 2, DodgerBlue/Red, Solid).

- [ ] **Step 6: Verify clean recompile with no warnings**

In the NinjaScript Editor, do **Build → Clean** (if available in your NT8 version, otherwise skip) followed by **F5**.

Expected: "Compile successful" with zero warnings and zero errors. If any warnings appear (e.g., unused variable), fix them in `AnchoredOffsetLines.cs` before continuing. The most likely warning is an unused local inside `OnBarUpdate` if any of the release variables are declared but unreferenced in an edge path — address by removing the unused declaration or wiring it up.

- [ ] **Step 7: Verify the indicator description appears in the dialog**

Open the Indicators dialog, select AnchoredOffsetLines. At the bottom of the dialog NinjaTrader shows the `Description` property set in `State.SetDefaults`. Verify the description text is readable and matches the spec.

- [ ] **Step 8: Commit (if any fixes were required)**

If Steps 1–7 required any code changes:

```bash
git add AnchoredOffsetLines.cs
git commit -m "Task 7: cross-timeframe verification fixes"
```

If no changes were required, create an empty verification-complete commit to mark the end of the plan:

```bash
git commit --allow-empty -m "Task 7: cross-timeframe verification complete"
```

---

## Implementation Notes

- **Never reuse a day-keyed anchor tag.** `anchorDayKey` is `"yyyyMMdd"`, which is unique per calendar day. If NT8 replays more than one day in the same session, each day gets its own tag pair and prior days' segments persist automatically via tag uniqueness.
- **`Draw.Line` signature used everywhere:** `Draw.Line(this, tag, isAutoScale, startBarsAgo, startY, endBarsAgo, endY, brush, dashStyle, width)` where `startBarsAgo` / `endBarsAgo` are int (0 = current bar, positive = into the past, negative = into the future). Do not confuse with the overload that takes `DateTime` arguments — this plan uses the bars-ago overload throughout.
- **Why `IsFirstTickOfBar` and `Time[1]` / `Close[1]` in the realtime branch:** in realtime with `Calculate.OnPriceChange`, `Close[0]` is the live, still-forming bar's current price. The closed bar we want to evaluate phase transitions on is the previous bar, at index 1. `IsFirstTickOfBar` is NinjaScript's documented hook for "a new bar just started, so the previous bar just closed."
- **Why phase transitions run before drawing for anchor but after drawing for release:** the anchor bar itself should render as part of the anchored segment, so we flip to Anchored *before* drawing. The release bar also renders as part of the anchored segment, so we flip back to Tracking *after* drawing. This keeps the visual bracket pinned to exactly the 09:30 → 09:35 window.
- **`Brush` serialization:** NinjaTrader requires `Brush` properties to be marked `[XmlIgnore]` and paired with a helper string property (`...Serializable`) that uses `Serialize.BrushToString` / `Serialize.StringToBrush`. This is standard NT8 boilerplate and is already in Task 2.

---

## Verification Checklist (for the end of Task 7)

- [ ] Compiles clean, zero warnings.
- [ ] Tracking lines follow live price on a 1-minute chart before 09:30 ET.
- [ ] Anchor bracket fires at the 09:30 bar and pins at 09:35 on a 1-minute chart.
- [ ] Anchor bracket and live tracking lines coexist after 09:35.
- [ ] Behavior matches on 15-second, 5-minute, and 3-minute charts.
- [ ] Behavior matches when the chart timezone is not Eastern.
- [ ] Anchor segment persists across day boundaries on a multi-day chart.
- [ ] Settings changes mid-session redraw everything correctly.
- [ ] Indicator description appears in the NT8 Indicators dialog.
