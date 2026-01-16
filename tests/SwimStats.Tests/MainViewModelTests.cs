using SwimStats.App.ViewModels;
using Xunit;

namespace SwimStats.Tests;

public class MainViewModelTests
{
    [Fact]
    public void CalculateMedian_WithOddNumberOfValues_ReturnsMiddleValue()
    {
        var values = new List<double> { 5.0, 3.0, 7.0, 1.0, 9.0 };
        
        var median = InvokeCalculateMedian(values);
        
        Assert.Equal(5.0, median);
    }

    [Fact]
    public void CalculateMedian_WithEvenNumberOfValues_ReturnsAverageOfMiddleTwo()
    {
        var values = new List<double> { 4.0, 2.0, 8.0, 6.0 };
        
        var median = InvokeCalculateMedian(values);
        
        Assert.Equal(5.0, median);
    }

    [Fact]
    public void CalculateMedian_WithSingleValue_ReturnsThatValue()
    {
        var values = new List<double> { 42.0 };
        
        var median = InvokeCalculateMedian(values);
        
        Assert.Equal(42.0, median);
    }

    [Fact]
    public void CalculateMedian_WithEmptyList_ReturnsZero()
    {
        var values = new List<double>();
        
        var median = InvokeCalculateMedian(values);
        
        Assert.Equal(0.0, median);
    }

    [Fact]
    public void CalculateMedian_WithNullList_ReturnsZero()
    {
        List<double>? values = null;
        
        var median = InvokeCalculateMedian(values!);
        
        Assert.Equal(0.0, median);
    }

    [Fact]
    public void CalculateMedian_WithUnsortedValues_SortsAndReturnsCorrectMedian()
    {
        var values = new List<double> { 100.0, 10.0, 50.0, 30.0, 70.0 };
        
        var median = InvokeCalculateMedian(values);
        
        Assert.Equal(50.0, median);
    }

    [Fact]
    public void CalculateMedian_WithDuplicateValues_ReturnsCorrectMedian()
    {
        var values = new List<double> { 5.0, 5.0, 5.0, 5.0, 5.0 };
        
        var median = InvokeCalculateMedian(values);
        
        Assert.Equal(5.0, median);
    }

    [Fact]
    public void CalculateMedian_WithNegativeValues_ReturnsCorrectMedian()
    {
        var values = new List<double> { -10.0, -5.0, 0.0, 5.0, 10.0 };
        
        var median = InvokeCalculateMedian(values);
        
        Assert.Equal(0.0, median);
    }

    [Fact]
    public void CalculateMedian_WithSwimTimes_ReturnsCorrectMedian()
    {
        // Realistic swim times in seconds
        var values = new List<double> { 25.5, 24.9, 26.1, 25.2, 25.8 };
        
        var median = InvokeCalculateMedian(values);
        
        Assert.Equal(25.5, median);
    }

    // Helper method to invoke private static CalculateMedian via reflection
    private static double InvokeCalculateMedian(List<double> values)
    {
        var method = typeof(MainViewModel).GetMethod("CalculateMedian", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        if (method == null)
            throw new InvalidOperationException("CalculateMedian method not found");
        
        var result = method.Invoke(null, new object[] { values });
        return (double)result!;
    }
}
