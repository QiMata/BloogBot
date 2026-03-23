using System.Text;
namespace ForegroundBotRunner.Tests;

internal sealed class WoWExeImage
{
    private const uint ImageBase = 0x400000;
    private const string DefaultPath = @"D:\World of Warcraft\WoW.exe";

    private readonly byte[] _imageBytes;
    private readonly Section[] _sections;

    private WoWExeImage(byte[] imageBytes, Section[] sections)
    {
        _imageBytes = imageBytes;
        _sections = sections;
    }

    public static WoWExeImage LoadDefault()
    {
        Assert.True(File.Exists(DefaultPath), $"WoW.exe not found at '{DefaultPath}'");

        byte[] imageBytes = File.ReadAllBytes(DefaultPath);
        int peHeaderOffset = BitConverter.ToInt32(imageBytes, 0x3C);
        ushort sectionCount = BitConverter.ToUInt16(imageBytes, peHeaderOffset + 6);
        ushort optionalHeaderSize = BitConverter.ToUInt16(imageBytes, peHeaderOffset + 20);
        int sectionTableOffset = peHeaderOffset + 24 + optionalHeaderSize;

        var sections = new Section[sectionCount];
        for (int i = 0; i < sectionCount; i++)
        {
            int offset = sectionTableOffset + (40 * i);
            string name = Encoding.ASCII.GetString(imageBytes, offset, 8).TrimEnd('\0');
            uint virtualSize = BitConverter.ToUInt32(imageBytes, offset + 8);
            uint virtualAddress = BitConverter.ToUInt32(imageBytes, offset + 12);
            uint rawSize = BitConverter.ToUInt32(imageBytes, offset + 16);
            uint rawPointer = BitConverter.ToUInt32(imageBytes, offset + 20);
            sections[i] = new Section(name, virtualAddress, virtualSize, rawPointer, rawSize);
        }

        return new WoWExeImage(imageBytes, sections);
    }

    public byte[] ReadBytes(uint virtualAddress, int count)
    {
        int rva = checked((int)(virtualAddress - ImageBase));
        var section = FindSection(rva);
        var bytes = new byte[count];
        int virtualOffset = checked(rva - (int)section.VirtualAddress);

        for (int i = 0; i < count; i++)
        {
            int currentOffset = virtualOffset + i;
            if (currentOffset >= section.VirtualSize)
                break;

            if (currentOffset >= section.RawSize)
            {
                bytes[i] = 0;
                continue;
            }

            bytes[i] = _imageBytes[checked((int)section.RawPointer + currentOffset)];
        }

        return bytes;
    }

    public string ReadAsciiZ(uint virtualAddress, int maxLength)
    {
        var bytes = ReadBytes(virtualAddress, maxLength);
        int terminatorIndex = Array.IndexOf(bytes, (byte)0);
        int length = terminatorIndex >= 0 ? terminatorIndex : bytes.Length;
        return Encoding.ASCII.GetString(bytes, 0, length);
    }

    public uint ReadUInt32(uint virtualAddress)
    {
        var bytes = ReadBytes(virtualAddress, sizeof(uint));
        return BitConverter.ToUInt32(bytes, 0);
    }

    public string GetSectionName(uint virtualAddress)
    {
        int rva = checked((int)(virtualAddress - ImageBase));
        return FindSection(rva).Name;
    }

    private Section FindSection(int rva)
    {
        foreach (var section in _sections)
        {
            uint span = Math.Max(section.VirtualSize, section.RawSize);
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + span)
                return section;
        }

        throw new InvalidOperationException($"VA 0x{ImageBase + (uint)rva:X8} is not inside any PE section");
    }

    private sealed record Section(string Name, uint VirtualAddress, uint VirtualSize, uint RawPointer, uint RawSize);
}
