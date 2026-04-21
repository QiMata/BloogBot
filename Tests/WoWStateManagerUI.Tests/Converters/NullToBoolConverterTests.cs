using System;
using System.Globalization;
using WoWStateManagerUI.ViewModels;

namespace WoWStateManagerUI.Tests.Converters;

public class NullToBoolConverterTests
{
    private readonly NullToBoolConverter _converter = new();

    [Fact]
    public void Convert_NonNullValue_ReturnsTrue()
    {
        var result = _converter.Convert(new object(), typeof(bool), null!, CultureInfo.InvariantCulture);

        Assert.True((bool)result);
    }

    [Fact]
    public void Convert_NullValue_ReturnsFalse()
    {
        var result = _converter.Convert(null, typeof(bool), null!, CultureInfo.InvariantCulture);

        Assert.False((bool)result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack(true, typeof(object), null!, CultureInfo.InvariantCulture));
    }
}
