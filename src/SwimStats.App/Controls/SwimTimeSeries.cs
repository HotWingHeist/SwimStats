using OxyPlot;
using OxyPlot.Series;
using SwimStats.App.Resources;

namespace SwimStats.App.Controls;

public class SwimTimeSeries : LineSeries
{
    private readonly List<SwimDataPoint> _dataItems = new();

    public void AddDataPoint(double x, double y, string? location)
    {
        var dataPoint = new DataPoint(x, y);
        Points.Add(dataPoint);
        _dataItems.Add(new SwimDataPoint { X = x, Y = y, Location = location });
    }

    public override TrackerHitResult? GetNearestPoint(ScreenPoint point, bool interpolate)
    {
        var result = base.GetNearestPoint(point, interpolate);
        if (result != null)
        {
            var loc = LocalizationManager.Instance;
            var timeSeconds = result.DataPoint.Y;
            var formattedTime = FormatSwimTime(timeSeconds);
            var date = DateTime.FromOADate(result.DataPoint.X);
            
            string location = null;
            var matchingItem = _dataItems.FirstOrDefault(i => 
                Math.Abs(i.X - result.DataPoint.X) < 0.0001 && 
                Math.Abs(i.Y - result.DataPoint.Y) < 0.0001);
            
            if (matchingItem != null)
            {
                location = matchingItem.Location;
            }
            
            if (!string.IsNullOrWhiteSpace(location))
            {
                result.Text = $"{this.Title}\n{loc["Date"]}: {date:dd MMM yyyy}\n{loc["Time"]}: {formattedTime}\n{loc["Location"]}: {location}";
            }
            else
            {
                result.Text = $"{this.Title}\n{loc["Date"]}: {date:dd MMM yyyy}\n{loc["Time"]}: {formattedTime}";
            }
        }
        return result;
    }

    private static string FormatSwimTime(double totalSeconds)
    {
        var minutes = (int)(totalSeconds / 60);
        var remainingSeconds = (int)(totalSeconds % 60);
        var centiseconds = (int)((totalSeconds % 1) * 100);

        if (minutes > 0)
        {
            return $"{minutes:00}:{remainingSeconds:00}.{centiseconds:00}";
        }
        else
        {
            return $"{remainingSeconds:00}.{centiseconds:00}";
        }
    }
}

public class SwimDataPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public string? Location { get; set; }
}