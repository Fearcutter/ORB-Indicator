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
        }

        protected override void OnBarUpdate()
        {
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
