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
        private DateTime lastProcessedBarEt;
        private TimeZoneInfo easternTz;

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

                Offset = 10.0;
                UpperLineBrush = Brushes.DodgerBlue;
                LowerLineBrush = Brushes.Red;
                LineWidth = 2;
                LineDashStyle = DashStyleHelper.Solid;
                LineExtensionBars = 5;
                AnchorCloseTime = new TimeSpan(9, 30, 0);
                ReleaseCloseTime = new TimeSpan(9, 35, 0);
            }
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

                if (phase == Phase.Anchored && curTod >= ReleaseCloseTime)
                {
                    releasePending = true;
                    releaseBarIdx = closedBarIdx;
                }

                lastProcessedBarEt = etClose;
            }

            // --- Drawing. ---
            if (phase == Phase.Anchored && anchorPrice.HasValue && anchorBarIndex >= 0 && anchorDayKey != null)
            {
                int startBarsAgo = CurrentBar - anchorBarIndex;
                int endBarsAgo = releasePending ? (CurrentBar - releaseBarIdx) : 0;

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
        [Display(Name = "Anchor close time (ET)", GroupName = "Parameters", Order = 6)]
        public TimeSpan AnchorCloseTime { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Release close time (ET)", GroupName = "Parameters", Order = 7)]
        public TimeSpan ReleaseCloseTime { get; set; }
        #endregion
    }
}
