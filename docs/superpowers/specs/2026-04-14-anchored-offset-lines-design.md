# AnchoredOffsetLines — NinjaTrader 8 Indicator Design

**Project:** 9:30 ORB NT8 Indicator
**Date:** 2026-04-14
**Status:** Design approved, ready for implementation planning

---

## 1. Purpose

A NinjaTrader 8 custom indicator written in C# that draws two horizontal lines on the chart — one at `+Offset` points above a reference price and one at `-Offset` points below it. The reference price changes based on the time of day:

- Most of the day, the reference is the **current bar's close**, and the lines track live price movement.
- Between **09:30:00 ET and 09:35:00 ET**, the reference is frozen to the close of the bar that ended at 09:30:00 ET (the last bar of the 09:29 minute). The lines lock in place for that 5-minute window.
- After 09:35:00 ET, the lines resume tracking live price.

The deliverable is a single file, `AnchoredOffsetLines.cs`, installed into the standard NT8 custom-indicators folder.

---

## 2. Scope

### In scope

- A single-class NT8 indicator with the behavior described in Section 3.
- Two user-configured drawing styles (upper line, lower line).
- User-configured offset, line-extension length, and anchor/release times.
- Correct behavior across intraday timeframes (seconds and minutes), multi-day charts, Eastern Time DST transitions, and non-Eastern chart timezones.
- Historical replay and live-data handling using the same code path.

### Out of scope

- Alerts, sounds, email, or bot signals on line breaks.
- Statistics, performance tracking, or backtest scaffolding.
- Daily-chart or weekly-chart behavior (this is an intraday-only indicator).
- Multi-instrument or multi-series logic.
- A strategy (NT8 `Strategy` class) wrapper.

---

## 3. Behavior

The indicator is always in one of two modes.

### 3.1 Tracking Mode

- The reference price is the current (most recent) bar's close.
- Two "tracking lines" are drawn — upper at `reference + Offset`, lower at `reference - Offset`.
- The tracking lines start at the current bar's time and extend `LineExtensionBars` bars into the future.
- As price moves, the tracking lines reposition on every price change (`Calculate.OnPriceChange`).
- This is the default mode at startup and after the daily reset.
- Tracking lines use fixed drawing-object tags (`"TrackingUpper"`, `"TrackingLower"`). Each redraw replaces the previous pair, so tracking lines never accumulate.

### 3.2 Anchored Mode

- The reference price is a fixed number called `anchorPrice`, captured once at the start of the phase.
- Two "anchored lines" are drawn — upper at `anchorPrice + Offset`, lower at `anchorPrice - Offset`.
- The anchored lines form a fixed horizontal segment spanning from the anchor bar (first bar whose close is at-or-past `AnchorCloseTime` that day) through the release bar (first bar whose close is at-or-past `ReleaseCloseTime` that day).
- Price changes **do not** affect the anchored lines during this phase.
- **Live tracking lines are hidden during this phase** — the tracking-line tags are removed on entry to anchored mode so only the frozen anchored segment is visible between 09:30 and 09:35.
- **Anchored segments persist after release.** Anchored lines use day-keyed drawing tags (e.g., `"AnchorUpper_20260414"`, `"AnchorLower_20260414"`) so the segment stays on the chart as a historical marker all day, and across multiple days on a multi-day chart. Each trading day gets its own pair of anchored-line tags.

### 3.3 Phase Transitions

Transitions are evaluated **on bar close only**, never mid-bar:

1. **Tracking → Anchored:** the indicator remembers the previous closed bar's Eastern time-of-day. When a new closed bar has an ET close at-or-past `AnchorCloseTime` **and** the previous closed bar today had an ET close strictly before `AnchorCloseTime`, this is the anchor bar. The indicator captures that bar's close price as `anchorPrice`, records its bar index, and flips into Anchored mode. The transition happens **before drawing** so the anchor bar itself renders as part of the anchored segment.

2. **Anchored → Tracking:** the indicator flags "release pending" when a closed bar's ET close is at-or-past `ReleaseCloseTime`. The transition happens **after drawing** so the release bar still renders as the final bar of the anchored segment. The next bar processed will be in Tracking mode.

3. **Daily reset:** at the start of each new Eastern Time trading day, the indicator resets: `phase = Tracking`, `anchorPrice = null`, and the "previous closed bar today" memory is cleared. The day's anchored segment (if any) remains visible via its day-keyed drawing tags.

### 3.4 Mid-Day Chart Load Safeguard

If the chart's first processed bar for a given day has an ET close already past `AnchorCloseTime`, the anchor trigger will not fire for that day — because the "previous bar today was before `AnchorCloseTime`" condition cannot be satisfied. The indicator stays in Tracking mode for the rest of the day. No anchored segment is retroactively invented. This is the intended behavior when a chart's loaded history does not include the morning open.

### 3.5 Odd-Timeframe Fallback

Most timeframes (1s, 5s, 10s, 15s, 30s, 1m, 5m) have a bar that closes exactly at 09:30:00 and another at 09:35:00. On oddball timeframes (e.g., 3-minute), there may be no exact match. The previous-bar-crossing rule in §3.3 handles these naturally: the anchor fires on whichever bar crosses the threshold, whether that's exactly at 09:30:00 or at 09:31:00 on a 3-minute chart. Same for release.

---

## 4. User-Configurable Parameters

Exposed as public C# properties with `[NinjaScriptProperty]` attributes so they appear in the NT8 indicator dialog.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `Offset` | `double` | `10.0` | Point distance between each line and the reference price. |
| `UpperLineColor` | `Brush` | `Brushes.DodgerBlue` | Color of the upper line. |
| `LowerLineColor` | `Brush` | `Brushes.Red` | Color of the lower line. |
| `LineWidth` | `int` | `2` | Line thickness in pixels. |
| `LineDashStyle` | `DashStyleHelper` | `DashStyleHelper.Solid` | Solid / Dash / Dot / DashDot. |
| `LineExtensionBars` | `int` | `5` | How many bars forward the lines extend in Tracking mode. |
| `AnchorCloseTime` | `TimeSpan` | `09:30:00` | Bar-close time (Eastern Time) at which the anchor price is captured. |
| `ReleaseCloseTime` | `TimeSpan` | `09:35:00` | Bar-close time (Eastern Time) at which tracking resumes. |

Times are always interpreted in Eastern Time, regardless of the chart's display timezone.

---

## 5. Architecture

### 5.1 File Layout

Single file: `AnchoredOffsetLines.cs`, placed in
`Documents/NinjaTrader 8/bin/Custom/Indicators/`.

### 5.2 Class Shape

```csharp
namespace NinjaTrader.NinjaScript.Indicators
{
    public class AnchoredOffsetLines : Indicator
    {
        // === User-configured properties ===
        // Offset, UpperLineColor, LowerLineColor, LineWidth,
        // LineDashStyle, LineExtensionBars, AnchorCloseTime, ReleaseCloseTime

        // === Internal state ===
        private enum Phase { Tracking, Anchored }
        private Phase phase;
        private double? anchorPrice;
        private int anchorBarIndex;           // bar number where anchor was captured
        private string anchorDayKey;          // "yyyyMMdd" for the current day's anchor segment tags
        private DateTime lastTradingDay;
        private DateTime lastProcessedBarEt;  // ET close of most recent bar evaluated today
        private TimeZoneInfo easternTz;       // cached

        private const string TrackingUpperTag = "TrackingUpper";
        private const string TrackingLowerTag = "TrackingLower";
        // Anchored segment tags are built dynamically: "AnchorUpper_" + yyyyMMdd, "AnchorLower_" + yyyyMMdd

        // === Lifecycle ===
        protected override void OnStateChange() { /* SetDefaults, Configure */ }
        protected override void OnBarUpdate()  { /* phase logic + drawing */ }

        // === Helpers ===
        private DateTime ToEastern(DateTime barTime) { ... }
        private void ResetDailyState(DateTime etDate) { ... }
        private void DrawTrackingLines() { ... }
        private void DrawAnchoredLines() { ... }
    }
}
```

No helper classes, no inheritance beyond `Indicator`, no external dependencies.

### 5.3 Calculate Mode

`Calculate = Calculate.OnPriceChange`, set in the `SetDefaults` block. This gives tick-accurate tracking without recalculating on duplicate-price ticks.

### 5.4 Timezone Handling

- On `State.Configure`, cache `easternTz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")`. On Windows, this ID automatically includes daylight-saving transitions for the US Eastern zone.
- For each bar, compute `etClose = TimeZoneInfo.ConvertTime(Time[0], easternTz)`. This works regardless of the chart's displayed timezone because `Time[0]` is always in the chart's source timezone, and `ConvertTime` handles it.

### 5.5 Drawing

Two independent tag families:

**Tracking lines** — `"TrackingUpper"`, `"TrackingLower"`, fixed tags, one pair total.
- Drawn on every `OnBarUpdate` call while `phase == Tracking`.
- Start at `0` bars ago (current bar), end at `-LineExtensionBars` bars ago (future), prices `Close[0] ± Offset`.
- Each redraw replaces the previous pair via same-tag replacement, so tracking lines never accumulate.
- On entry to Anchored phase, both tags are removed via `RemoveDrawObject` so no stale tracking lines linger during the anchor window.

**Anchored segments** — `"AnchorUpper_" + yyyyMMdd`, `"AnchorLower_" + yyyyMMdd`, one pair per trading day.
- Drawn on every `OnBarUpdate` call while `phase == Anchored`.
- Start at `anchorBarIndex` (the bar whose close set the anchor price), end at the current bar being processed while the anchor window is still open, or at the release bar index on the release update.
- Prices `anchorPrice ± Offset`.
- Because the tag includes the day key, these segments persist across day boundaries — a week-long 1-min chart will show a separate anchored bracket for each day.
- Each bar within the anchor window replaces the same day's tags with a slightly longer segment, so the segment grows from the anchor bar forward to the release bar.
- After release, the tags are no longer redrawn and the segment remains frozen until the chart is closed or the indicator is reloaded.

### 5.6 Historical vs Live

Same `OnBarUpdate` runs in both `State.Historical` and `State.Realtime`. Phase transitions always happen on bar close (`IsFirstTickOfBar` or equivalent on a price-change update), so historical replay and live data produce identical visual output.

---

## 6. Edge Cases

1. **Chart opened mid-day (after 09:35) with morning history included:** Historical replay walks through the day, captures the anchor on the 09:30 bar, draws the anchored segment with the day-keyed tags, releases at 09:35, and arrives at the current moment in Tracking mode. The anchored segment remains visible at its correct place because day-keyed tags are not reused.

2. **Chart opened mid-day (after 09:30) with no morning history:** The first processed bar's ET close is already past `AnchorCloseTime`, so the crossing check in §3.3 never satisfies. The indicator stays in Tracking mode all day; no retroactive anchored segment appears.

3. **Chart opened before 09:30:** Tracking mode only. Nothing special happens until the 09:30 bar closes.

4. **Weekend / market closed:** No data, no updates. Indicator sits in whatever mode it was in.

5. **Multi-day chart:** The daily reset fires once per Eastern Time trading day. Each day's anchored segment uses a different day-keyed tag pair, so all prior days' brackets remain visible alongside the current day's live tracking lines.

6. **Chart timezone is not Eastern:** Doesn't matter. Every bar's timestamp is converted to Eastern Time before comparing to `AnchorCloseTime` / `ReleaseCloseTime`.

7. **Odd timeframe (3-min, 7-min, etc.):** The previous-bar-crossing rule handles this — the anchor fires on whichever bar first crosses the threshold, whether exactly at 09:30:00 or at 09:31:00 on a 3-minute chart.

8. **Daylight saving transitions:** Handled by `TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")`, which covers both EST and EDT automatically.

9. **Settings changed mid-session:** NT8 recalculates the indicator from scratch on OK. Lines redraw at the new settings without extra code.

10. **Historical vs live handoff:** `OnBarUpdate` uses a small branch: in `State.Historical` it processes every bar as a closed bar; in `State.Realtime` it processes closed-bar events only on `IsFirstTickOfBar` (looking at `Time[1]` / `Close[1]`). Drawing runs unconditionally on every update so live tracking lines follow price in realtime.

---

## 7. Testing Strategy

NinjaTrader does not ship a formal unit-test harness for custom indicators, so verification is primarily manual, chart-driven:

1. **Compile cleanly** in the NT8 NinjaScript Editor — no errors, no warnings.
2. **Load on a 1-minute chart** of NQ or ES for today's session.
   - Confirm lines track price before 09:30.
   - Confirm anchor fires at the 09:30 bar close.
   - Confirm anchored segment spans exactly 09:30 through 09:35.
   - Confirm tracking resumes on the 09:36 bar.
3. **Load on a 15-second chart** for the same session.
   - Confirm anchor still fires at the 09:29:45 → 09:30:00 bar.
   - Confirm anchored segment length looks correct (20 bars on a 15s chart).
4. **Load on a 5-minute chart.**
   - Confirm anchor bar is the 09:25 → 09:30 bar.
   - Confirm anchored segment is a single bar wide (09:30 → 09:35).
5. **Load a multi-day chart** (e.g., 5-day 1-min).
   - Confirm each day shows its own independent anchored segment.
6. **Change chart timezone** to Pacific.
   - Confirm anchor still fires on the ET 09:30 bar, now displayed at 06:30 Pacific.
7. **Spring-forward / fall-back DST sanity check** (if possible with available historical data).
8. **Change settings mid-session** (Offset, colors, width).
   - Confirm lines redraw without stale duplicates.

---

## 8. Open Questions

None. All clarifying questions from the brainstorming session are resolved.

---

## 9. Risks & Unknowns

- **Chart source-time semantics:** NT8's `Time[0]` is documented as being in the chart's data-source timezone. If a specific data provider returns times in an unexpected zone, the ET conversion could be off. The implementation should verify with a quick sanity-check log line on first load during development.
- **Rare weird timeframes:** A user running a 7-minute or 13-minute chart will hit the fallback rule. This is expected but should be noted in code comments so a future reader understands why the comparison is `>` and not `==`.
- **`Brushes` serialization:** NT8 indicator properties of type `Brush` require a paired `[Browsable(false)]` string property for serialization. This is standard NT8 boilerplate and will be included in the implementation.
