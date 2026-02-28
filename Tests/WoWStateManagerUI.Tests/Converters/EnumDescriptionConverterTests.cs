using System.ComponentModel;
using System.Globalization;
using WoWStateManagerUI.Converters;

namespace WoWStateManagerUI.Tests.Converters;

public class EnumDescriptionConverterTests
{
    private readonly EnumDescriptionConverter _converter = new();

    private enum TestEnum
    {
        [Description("First Value Description")]
        First,

        [Description("Second Value Description")]
        Second,

        NoDescription
    }

    [Fact]
    public void Convert_EnumWithDescription_ReturnsDescription()
    {
        var result = _converter.Convert(TestEnum.First, typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal("First Value Description", result);
    }

    [Fact]
    public void Convert_EnumWithDifferentDescription_ReturnsCorrectDescription()
    {
        var result = _converter.Convert(TestEnum.Second, typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal("Second Value Description", result);
    }

    [Fact]
    public void Convert_EnumWithoutDescription_ReturnsNull()
    {
        var result = _converter.Convert(TestEnum.NoDescription, typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Null(result);
    }

    [Fact]
    public void Convert_NonStringTargetType_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(
            () => _converter.Convert(TestEnum.First, typeof(int), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(
            () => _converter.ConvertBack("First Value Description", typeof(TestEnum), null!, CultureInfo.InvariantCulture));
    }
}
