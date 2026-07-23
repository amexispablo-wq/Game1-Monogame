using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace ColorBlocks;

internal sealed class PacketBuffer
{
    private readonly MemoryStream _stream = new();

    public int Length => (int)_stream.Length;

    public byte[] ToArray() => _stream.ToArray();

    public void Reset() => _stream.SetLength(0);

    public void WriteByte(byte value) => _stream.WriteByte(value);

    public void WriteBool(bool value) => _stream.WriteByte(value ? (byte)1 : (byte)0);

    public void WriteInt16(short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteUInt16(ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteInt32(int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteInt64(long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteSingle(float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        _stream.Write(value);
    }

    public void WriteString(string value)
    {
        string text = value ?? string.Empty;
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        WriteUInt16((ushort)Math.Min(bytes.Length, ushort.MaxValue));
        _stream.Write(bytes, 0, Math.Min(bytes.Length, ushort.MaxValue));
    }

    public static PacketReader CreateReader(ReadOnlySpan<byte> data) => new(data);
}

internal ref struct PacketReader
{
    private ReadOnlySpan<byte> _data;
    private int _offset;

    public PacketReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _offset = 0;
    }

    public bool HasData => _offset < _data.Length;

    public byte ReadByte()
    {
        Ensure(1);
        return _data[_offset++];
    }

    public bool ReadBool() => ReadByte() != 0;

    public short ReadInt16() => Read<short>(BinaryPrimitives.ReadInt16LittleEndian);

    public ushort ReadUInt16() => Read<ushort>(BinaryPrimitives.ReadUInt16LittleEndian);

    public int ReadInt32() => Read<int>(BinaryPrimitives.ReadInt32LittleEndian);

    public long ReadInt64() => Read<long>(BinaryPrimitives.ReadInt64LittleEndian);

    public float ReadSingle() => Read<float>(BinaryPrimitives.ReadSingleLittleEndian);

    public string ReadString()
    {
        ushort length = ReadUInt16();
        Ensure(length);
        string text = Encoding.UTF8.GetString(_data.Slice(_offset, length));
        _offset += length;
        return text;
    }

    public byte[] ReadBytes(int length)
    {
        Ensure(length);
        byte[] value = _data.Slice(_offset, length).ToArray();
        _offset += length;
        return value;
    }

    private T Read<T>(ReadSpan<T> reader) where T : struct
    {
        Ensure(sizeof(byte) * (typeof(T) == typeof(long) ? 8 : typeof(T) == typeof(int) || typeof(T) == typeof(float) ? 4 : 2));
        T value = reader(_data.Slice(_offset));
        _offset += typeof(T) == typeof(long) ? 8 : typeof(T) == typeof(int) || typeof(T) == typeof(float) ? 4 : 2;
        return value;
    }

    private void Ensure(int count)
    {
        if (_offset + count > _data.Length)
        {
            throw new InvalidDataException("Packet truncated.");
        }
    }

    private delegate T ReadSpan<T>(ReadOnlySpan<byte> span) where T : struct;
}
