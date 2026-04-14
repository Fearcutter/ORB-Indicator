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
