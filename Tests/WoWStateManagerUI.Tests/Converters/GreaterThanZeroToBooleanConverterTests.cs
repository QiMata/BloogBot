using System.Globalization;
using System.Windows.Data;
using WoWStateManagerUI.Converters;

namespace WoWStateManagerUI.Tests.Converters;

public class GreaterThanZeroToBooleanConverterTests
{
    private readonly GreaterThanZeroToBooleanConverter _converter = new();

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(100, true)]
    [InlineData(int.MaxValue, true)]
    public void Convert_PositiveInt_ReturnsTrue(int value, bool expected)
    {
        var result = _converter.Convert(value, typeof(bool), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(-100, false)]
    [InlineData(int.MinValue, false)]
    public void Convert_ZeroOrNegativeInt_ReturnsFalse(int value, bool expected)
    {
        var result = _converter.Convert(value, typeof(bool), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_NegativeOne_ReturnsFalse_SelectionGating()
    {
        // -1 is the default unselected index in StateManagerViewModel
        var result = _converter.Convert(-1, typeof(bool), null!, CultureInfo.InvariantCulture);
        Assert.False((bool)result);
    }

    [Fact]
    public void Convert_Zero_ReturnsFalse_BoundaryCase()
    {
        // Zero is NOT greater than zero; UI controls should be disabled
        var result = _converter.Convert(0, typeof(bool), null!, CultureInfo.InvariantCulture);
        Assert.False((bool)result);
    }

    [Theory]
    [InlineData("not an int")]
    [InlineData(3.14)]
    [InlineData(true)]
    [InlineData(null)]
    public void Convert_NonIntValue_ReturnsFalse(object? value)
    {
        var result = _converter.Convert(value!, typeof(bool), null!, CultureInfo.InvariantCulture);
        Assert.False((bool)result);
    }

    [Fact]
    public void ConvertBack_ReturnsDoNothing()
    {
        // One-way converter: ConvertBack must not attempt reverse conversion
        var result = _converter.ConvertBack(true, typeof(int), null!, CultureInfo.InvariantCulture);
        Assert.Same(Binding.DoNothing, result);
    }

    [Fact]
    public void ConvertBack_WithFalse_ReturnsDoNothing()
    {
        var result = _converter.ConvertBack(false, typeof(int), null!, CultureInfo.InvariantCulture);
        Assert.Same(Binding.DoNothing, result);
    }
}
