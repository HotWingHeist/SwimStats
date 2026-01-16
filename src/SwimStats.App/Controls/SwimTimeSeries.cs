using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using SwimStats.App.Resources;

namespace SwimStats.App.Controls;

public class SwimTimeSeries : LineSeries
{
    public override TrackerHitResult? GetNearestPoint(ScreenPoint point, bool interpolate)
    {
        var result = base.GetNearestPoint(point, interpolate);
        
        if (result != null)
        {
            var loc = LocalizationManager.Instance;
            var timeSeconds = result.DataPoint.Y;
            var formattedTime = FormatSwimTime(timeSeconds);
            
            // Create custom text with formatted time
            var date = DateTime.FromOADate(result.DataPoint.X);
            result.Text = $"{this.Title}\n{loc["Date"]}: {date:dd MMM yyyy}\n{loc["Time"]}: {formattedTime}";
        }
        
        return result;
    }
    
    private static string FormatSwimTime(double totalSeconds)
    {
        var minutes = (int)(totalSeconds / 60);
        var seconds = totalSeconds % 60;
        
        if (minutes > 0)
        {
            return $"{minutes}:{seconds:00.00}";
        }
        else
        {
            return $"{seconds:0.00}";
        }
    }
}
