using System;
using System.Globalization;
using WoWStateManagerUI.ViewModels;

namespace WoWStateManagerUI.Tests.Converters;

public class PathToFilenameConverterTests
{
    private readonly PathToFilenameConverter _converter = new();

    [Theory]
    [InlineData(@"C:\Games\WoW\mangosd.conf", "mangosd.conf")]
    [InlineData(@"relative\path\realmd.conf", "realmd.conf")]
    [InlineData("plain.txt", "plain.txt")]
    public void Convert_StringPath_ReturnsFilename(string path, string expected)
    {
        var result = _converter.Convert(path, typeof(string), null!, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(123)]
    [InlineData(true)]
    public void Convert_NonStringValue_ReturnsEmptyString(object? value)
    {
        var result = _converter.Convert(value, typeof(string), null!, CultureInfo.InvariantCulture);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack("mangosd.conf", typeof(string), null!, CultureInfo.InvariantCulture));
    }
}
