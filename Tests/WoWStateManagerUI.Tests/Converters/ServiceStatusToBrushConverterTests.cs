using System;
using System.Globalization;
using System.Windows.Media;
using WoWStateManagerUI.Converters;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.Tests.Converters;

public class ServiceStatusToBrushConverterTests
{
    private readonly ServiceStatusToBrushConverter _converter = new();

    [Theory]
    [InlineData(ServiceStatus.Up, 76, 175, 80)]
    [InlineData(ServiceStatus.Down, 244, 67, 54)]
    [InlineData(ServiceStatus.Unknown, 158, 158, 158)]
    public void Convert_ServiceStatus_ReturnsExpectedBrush(ServiceStatus status, byte red, byte green, byte blue)
    {
        var result = _converter.Convert(status, typeof(Brush), null!, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(red, green, blue), brush.Color);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("up")]
    [InlineData(1)]
    public void Convert_NonServiceStatus_ReturnsGrayBrush(object? value)
    {
        var result = _converter.Convert(value!, typeof(Brush), null!, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(158, 158, 158), brush.Color);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack(Brushes.Gray, typeof(ServiceStatus), null!, CultureInfo.InvariantCulture));
    }
}
