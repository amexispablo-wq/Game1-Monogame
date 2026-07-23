using System;
using System.Collections.Generic;

namespace ColorBlocks;

public static class NetworkPacketCodec
{
    private const byte PacketVersion = 2;

    public static byte[] EncodeInputFrame(InputFrame frame)
    {
        PacketBuffer buffer = new();
        buffer.WriteByte(PacketVersion);
        buffer.WriteByte((byte)NetworkPacketType.InputFrame);
        buffer.WriteInt64(frame.Tick);
        buffer.WriteInt32(frame.OwnerId);
        buffer.WriteUInt16((ushort)Math.Min(frame.PlayerInputs.Count, ushort.MaxValue));

        for (int i = 0; i < frame.PlayerInputs.Count && i < ushort.MaxValue; i++)
        {
            PlayerInputEntry entry = frame.PlayerInputs[i];
            buffer.WriteInt32(entry.NetworkId);
            WritePlayerInput(buffer, entry.Input);
        }

        return buffer.ToArray();
    }

    public static byte[] EncodeGameplaySnapshot(GameSnapshot snapshot)
    {
        PacketBuffer buffer = new();
        buffer.WriteByte(PacketVersion);
        buffer.WriteByte((byte)NetworkPacketType.GameSnapshot);
        buffer.WriteInt64(snapshot.Tick);
        buffer.WriteInt32(snapshot.Sequence);
        buffer.WriteByte((byte)snapshot.RopeMode);
        WriteTimer(buffer, snapshot.Timer);

        buffer.WriteUInt16((ushort)Math.Min(snapshot.Players.Count, ushort.MaxValue));
        foreach (PlayerSnapshot player in snapshot.Players)
        {
            WritePlayer(buffer, player);
        }

        buffer.WriteUInt16((ushort)Math.Min(snapshot.Ropes.Count, ushort.MaxValue));
        foreach (RopeSnapshot rope in snapshot.Ropes)
        {
            WriteRope(buffer, rope);
        }

        return buffer.ToArray();
    }

    public static bool TryDecode(byte[] data, out NetworkPacketType packetType, out InputFrame? inputFrame, out GameSnapshot? snapshot)
    {
        inputFrame = null;
        snapshot = null;
        packetType = default;

        if (data is null || data.Length < 3)
        {
            return false;
        }

        try
        {
            PacketReader reader = PacketBuffer.CreateReader(data);
            byte version = reader.ReadByte();
            if (version != PacketVersion)
            {
                return false;
            }

            packetType = (NetworkPacketType)reader.ReadByte();
            switch (packetType)
            {
                case NetworkPacketType.InputFrame:
                    inputFrame = ReadInputFrame(ref reader);
                    return true;
                case NetworkPacketType.GameSnapshot:
                    snapshot = ReadGameplaySnapshot(ref reader);
                    return true;
                default:
                    return false;
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.IO.InvalidDataException)
        {
            return false;
        }
    }

    private static InputFrame ReadInputFrame(ref PacketReader reader)
    {
        InputFrame frame = new()
        {
            Tick = reader.ReadInt64(),
            OwnerId = reader.ReadInt32()
        };

        ushort count = reader.ReadUInt16();
        for (int i = 0; i < count; i++)
        {
            int networkId = reader.ReadInt32();
            PlayerInputState input = ReadPlayerInput(ref reader);
            frame.AddPlayerInput(networkId, input);
        }

        return frame;
    }

    private static GameSnapshot ReadGameplaySnapshot(ref PacketReader reader)
    {
        GameSnapshot snapshot = new()
        {
            Tick = reader.ReadInt64(),
            Sequence = reader.ReadInt32(),
            RopeMode = (RopeGameplayMode)reader.ReadByte(),
            Timer = ReadTimer(ref reader),
            Level = new LevelSnapshot()
        };

        ushort playerCount = reader.ReadUInt16();
        for (int i = 0; i < playerCount; i++)
        {
            snapshot.Players.Add(ReadPlayer(ref reader));
        }

        ushort ropeCount = reader.ReadUInt16();
        for (int i = 0; i < ropeCount; i++)
        {
            snapshot.Ropes.Add(ReadRope(ref reader));
        }

        return snapshot;
    }

    private static void WritePlayerInput(PacketBuffer buffer, PlayerInputState input)
    {
        buffer.WriteSingle(input.HorizontalMovement);
        byte flags = 0;
        if (input.JumpPressed)
        {
            flags |= 1;
        }

        if (input.RespawnPressed)
        {
            flags |= 2;
        }

        if (input.FastFallHeld)
        {
            flags |= 4;
        }

        if (input.PullRopeHeld)
        {
            flags |= 8;
        }

        buffer.WriteByte(flags);
        if (input.RequestedColor is GameColor color)
        {
            buffer.WriteByte(1);
            buffer.WriteByte((byte)color);
        }
        else
        {
            buffer.WriteByte(0);
        }
    }

    private static PlayerInputState ReadPlayerInput(ref PacketReader reader)
    {
        float horizontal = reader.ReadSingle();
        byte flags = reader.ReadByte();
        GameColor? color = null;
        if (reader.ReadByte() != 0)
        {
            color = (GameColor)reader.ReadByte();
        }

        return new PlayerInputState(
            horizontal,
            (flags & 1) != 0,
            (flags & 2) != 0,
            (flags & 4) != 0,
            (flags & 8) != 0,
            color);
    }

    private static void WriteTimer(PacketBuffer buffer, TimerSnapshot timer)
    {
        buffer.WriteSingle(timer.ElapsedTime);
        buffer.WriteBool(timer.IsRunning);
        buffer.WriteBool(timer.IsComplete);
        buffer.WriteSingle(timer.FinalTime);
        buffer.WriteBool(timer.NewRecord);
    }

    private static TimerSnapshot ReadTimer(ref PacketReader reader)
    {
        return new TimerSnapshot(
            reader.ReadSingle(),
            reader.ReadBool(),
            reader.ReadBool(),
            reader.ReadSingle(),
            reader.ReadBool());
    }

    private static void WritePlayer(PacketBuffer buffer, PlayerSnapshot player)
    {
        buffer.WriteInt32(player.NetworkId);
        buffer.WriteInt32(player.OwnerId);
        buffer.WriteInt32(player.PlayerIndex);
        buffer.WriteInt32((int)player.PlayerId);
        WriteVector(buffer, player.Position);
        WriteVector(buffer, player.Velocity);
        WriteVector(buffer, player.Acceleration);
        buffer.WriteByte((byte)player.Color);
        buffer.WriteByte((byte)player.State);
        buffer.WriteBool(player.IsGrounded);
        buffer.WriteBool(player.IsFrozen);
        buffer.WriteString(player.CosmeticSkinId ?? string.Empty);
        byte[] pixels = player.CosmeticSkinPixels is { Length: PlayerSkinCodec.PackedByteCount }
            ? player.CosmeticSkinPixels
            : new byte[PlayerSkinCodec.PackedByteCount];
        buffer.WriteBytes(pixels);
    }

    private static PlayerSnapshot ReadPlayer(ref PacketReader reader)
    {
        return new PlayerSnapshot(
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            (PlayerId)reader.ReadInt32(),
            ReadVector(ref reader),
            ReadVector(ref reader),
            ReadVector(ref reader),
            (GameColor)reader.ReadByte(),
            (PlayerState)reader.ReadByte(),
            reader.ReadBool(),
            reader.ReadBool(),
            reader.ReadString(),
            reader.ReadBytes(PlayerSkinCodec.PackedByteCount));
    }

    private static void WriteRope(PacketBuffer buffer, RopeSnapshot rope)
    {
        buffer.WriteInt32(rope.NetworkId);
        buffer.WriteInt32(rope.OwnerId);
        buffer.WriteInt32(rope.StartPlayerNetworkId);
        buffer.WriteInt32(rope.EndPlayerNetworkId);
        buffer.WriteByte((byte)rope.RopeMode);
        buffer.WriteSingle(rope.Tension);
        buffer.WriteBool(rope.IsTense);
        buffer.WriteSingle(rope.PullIntensity);
        buffer.WriteInt32(rope.PulledNodeCount);
        buffer.WriteUInt16((ushort)Math.Min(rope.NodePositions.Count, ushort.MaxValue));
        foreach (NetworkVector2 node in rope.NodePositions)
        {
            WriteVector(buffer, node);
        }
    }

    private static RopeSnapshot ReadRope(ref PacketReader reader)
    {
        RopeSnapshot rope = new()
        {
            NetworkId = reader.ReadInt32(),
            OwnerId = reader.ReadInt32(),
            StartPlayerNetworkId = reader.ReadInt32(),
            EndPlayerNetworkId = reader.ReadInt32(),
            RopeMode = (RopeGameplayMode)reader.ReadByte(),
            Tension = reader.ReadSingle(),
            IsTense = reader.ReadBool(),
            PullIntensity = reader.ReadSingle(),
            PulledNodeCount = reader.ReadInt32()
        };

        ushort nodeCount = reader.ReadUInt16();
        for (int i = 0; i < nodeCount; i++)
        {
            rope.NodePositions.Add(ReadVector(ref reader));
        }

        return rope;
    }

    private static void WriteVector(PacketBuffer buffer, NetworkVector2 vector)
    {
        buffer.WriteSingle(vector.X);
        buffer.WriteSingle(vector.Y);
    }

    private static NetworkVector2 ReadVector(ref PacketReader reader)
    {
        return new NetworkVector2(reader.ReadSingle(), reader.ReadSingle());
    }
}
