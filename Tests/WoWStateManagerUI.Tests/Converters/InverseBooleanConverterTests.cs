using System.Globalization;
using WoWStateManagerUI.Converters;

namespace WoWStateManagerUI.Tests.Converters;

public class InverseBooleanConverterTests
{
    private readonly InverseBooleanConverter _converter = new();

    [Fact]
    public void Convert_True_ReturnsFalse()
    {
        var result = _converter.Convert(true, typeof(bool), null!, CultureInfo.InvariantCulture);
        Assert.False((bool)result);
    }

    [Fact]
    public void Convert_False_ReturnsTrue()
    {
        var result = _converter.Convert(false, typeof(bool), null!, CultureInfo.InvariantCulture);
        Assert.True((bool)result);
    }

    [Fact]
    public void Convert_NonBoolTargetType_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(
            () => _converter.Convert(true, typeof(string), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(
            () => _converter.ConvertBack(true, typeof(bool), null!, CultureInfo.InvariantCulture));
    }
}
