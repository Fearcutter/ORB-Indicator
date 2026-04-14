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
        private const string TrackingUpperTag = "TrackingUpper";
        private const string TrackingLowerTag = "TrackingLower";

        private enum Phase { Tracking, Anchored }

        private Phase phase;
        private double? anchorPrice;
        private int anchorBarIndex;
        private string anchorDayKey;
        private DateTime lastTradingDay;
        private DateTime lastProcessedBar;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Draws two offset lines that track current price, then lock to the close of the bar just before Anchor Close Time and stay locked until Release Close Time. Anchor/Release times are compared against the chart's display timezone — set your chart to Eastern Time for the default 09:30-09:35 ET window. A midline at the anchor price can be shown during the locked window.";
                Name = "AnchoredOffsetLines";
                Calculate = Calculate.OnPriceChange;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = true;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = true;

                Offset = 10.0;
                UpperLineBrush = Brushes.DodgerBlue;
                LowerLineBrush = Brushes.Red;
                LineWidth = 2;
                LineDashStyle = DashStyleHelper.Solid;
                LineExtensionBars = 5;
                AnchorCloseTime = new TimeSpan(9, 30, 0);
                ReleaseCloseTime = new TimeSpan(9, 35, 0);

                ShowMidLine = true;
                MidLineBrush = Brushes.Gold;
                MidLineWidth = 1;
                MidLineDashStyle = DashStyleHelper.Dash;

                ShowTakeProfitLines = true;
                TakeProfitOffset = 12.5;
                TakeProfitBrush = Brushes.LimeGreen;
                TakeProfitLineWidth = 1;
                TakeProfitDashStyle = DashStyleHelper.Dot;
            }
            else if (State == State.Configure)
            {
                phase = Phase.Tracking;
                anchorPrice = null;
                anchorBarIndex = -1;
                anchorDayKey = null;
                lastTradingDay = DateTime.MinValue;
                lastProcessedBar = DateTime.MinValue;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 0) return;

            bool releasePending = false;
            int releaseBarIdx = -1;

            // --- Identify whether a bar-close event occurred on this update. ---
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

            // --- Per-day reset + phase transitions on closed bars. ---
            // closedBarTime is in the chart's display timezone (which NT8 sets from the user's
            // profile). AnchorCloseTime / ReleaseCloseTime are compared directly against it — no
            // timezone conversion. Set your chart to Eastern Time for 09:30 ET anchor behavior.
            if (processClosedBar)
            {
                if (closedBarTime.Date != lastTradingDay)
                {
                    lastTradingDay = closedBarTime.Date;
                    phase = Phase.Tracking;
                    anchorPrice = null;
                    anchorBarIndex = -1;
                    anchorDayKey = null;
                    lastProcessedBar = DateTime.MinValue;
                }

                bool havePrevToday =
                    lastProcessedBar != DateTime.MinValue &&
                    lastProcessedBar.Date == closedBarTime.Date;

                TimeSpan prevTod = havePrevToday ? lastProcessedBar.TimeOfDay : TimeSpan.MinValue;
                TimeSpan curTod = closedBarTime.TimeOfDay;

                if (phase == Phase.Tracking
                    && havePrevToday
                    && prevTod < AnchorCloseTime
                    && curTod >= AnchorCloseTime)
                {
                    anchorPrice = closedBarClose;
                    anchorBarIndex = closedBarIdx;
                    anchorDayKey = closedBarTime.ToString("yyyyMMdd");
                    phase = Phase.Anchored;

                    RemoveDrawObject(TrackingUpperTag);
                    RemoveDrawObject(TrackingLowerTag);
                }

                if (phase == Phase.Anchored && curTod >= ReleaseCloseTime)
                {
                    releasePending = true;
                    releaseBarIdx = closedBarIdx;
                }

                lastProcessedBar = closedBarTime;
            }

            // --- Drawing. ---
            if (phase == Phase.Anchored && anchorPrice.HasValue && anchorBarIndex >= 0 && anchorDayKey != null)
            {
                int startBarsAgo = CurrentBar - anchorBarIndex;
                int endBarsAgo = releasePending ? (CurrentBar - releaseBarIdx) : 0;

                double upperA = anchorPrice.Value + Offset;
                double lowerA = anchorPrice.Value - Offset;
                double midA = anchorPrice.Value;

                string upTag = "AnchorUpper_" + anchorDayKey;
                string loTag = "AnchorLower_" + anchorDayKey;
                string midTag = "AnchorMid_" + anchorDayKey;

                Draw.Line(this, upTag, false,
                          startBarsAgo, upperA,
                          endBarsAgo, upperA,
                          UpperLineBrush, LineDashStyle, LineWidth);

                Draw.Line(this, loTag, false,
                          startBarsAgo, lowerA,
                          endBarsAgo, lowerA,
                          LowerLineBrush, LineDashStyle, LineWidth);

                if (ShowMidLine)
                {
                    Draw.Line(this, midTag, false,
                              startBarsAgo, midA,
                              endBarsAgo, midA,
                              MidLineBrush, MidLineDashStyle, MidLineWidth);
                }

                if (ShowTakeProfitLines)
                {
                    double tpUpper = upperA + TakeProfitOffset;
                    double tpLower = lowerA - TakeProfitOffset;

                    string tpUpTag = "AnchorTPUpper_" + anchorDayKey;
                    string tpLoTag = "AnchorTPLower_" + anchorDayKey;

                    Draw.Line(this, tpUpTag, false,
                              startBarsAgo, tpUpper,
                              endBarsAgo, tpUpper,
                              TakeProfitBrush, TakeProfitDashStyle, TakeProfitLineWidth);

                    Draw.Line(this, tpLoTag, false,
                              startBarsAgo, tpLower,
                              endBarsAgo, tpLower,
                              TakeProfitBrush, TakeProfitDashStyle, TakeProfitLineWidth);
                }
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

            if (releasePending)
            {
                phase = Phase.Tracking;
                anchorPrice = null;
                anchorBarIndex = -1;
                anchorDayKey = null;
            }
        }

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
        [Display(Name = "Anchor close time (chart tz)", GroupName = "Parameters", Order = 6)]
        public TimeSpan AnchorCloseTime { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Release close time (chart tz)", GroupName = "Parameters", Order = 7)]
        public TimeSpan ReleaseCloseTime { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show midline (at anchor price)", GroupName = "Mid Line", Order = 8)]
        public bool ShowMidLine { get; set; }

        [XmlIgnore]
        [Display(Name = "Midline color", GroupName = "Mid Line", Order = 9)]
        public Brush MidLineBrush { get; set; }

        [Browsable(false)]
        public string MidLineBrushSerializable
        {
            get { return Serialize.BrushToString(MidLineBrush); }
            set { MidLineBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Midline width", GroupName = "Mid Line", Order = 10)]
        public int MidLineWidth { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Midline dash style", GroupName = "Mid Line", Order = 11)]
        public DashStyleHelper MidLineDashStyle { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show take-profit lines", GroupName = "Take Profit", Order = 12)]
        public bool ShowTakeProfitLines { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, double.MaxValue)]
        [Display(Name = "Take-profit offset (points)", GroupName = "Take Profit", Order = 13)]
        public double TakeProfitOffset { get; set; }

        [XmlIgnore]
        [Display(Name = "Take-profit color", GroupName = "Take Profit", Order = 14)]
        public Brush TakeProfitBrush { get; set; }

        [Browsable(false)]
        public string TakeProfitBrushSerializable
        {
            get { return Serialize.BrushToString(TakeProfitBrush); }
            set { TakeProfitBrush = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Take-profit line width", GroupName = "Take Profit", Order = 15)]
        public int TakeProfitLineWidth { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Take-profit dash style", GroupName = "Take Profit", Order = 16)]
        public DashStyleHelper TakeProfitDashStyle { get; set; }
        #endregion
    }
}
