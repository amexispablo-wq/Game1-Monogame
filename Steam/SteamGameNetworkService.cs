#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;

namespace ColorBlocks;

public sealed class SteamGameNetworkService
{
    private const int InputChannel = 0;
    private const int SnapshotChannel = 1;
    // Realtime gameplay: unreliable avoids HOL blocking from Reliable at 60 Hz.
    private const int SendFlags = Constants.k_nSteamNetworkingSend_UnreliableNoNagle;

    private readonly SteamManager _steam;
    private readonly SteamCallbackManager _callbacks;

    public SteamGameNetworkService(SteamManager steam, SteamCallbackManager callbacks)
    {
        _steam = steam;
        _callbacks = callbacks;
        _callbacks.NetworkingSessionRequest += OnNetworkingSessionRequest;
        _callbacks.NetworkingSessionFailed += OnNetworkingSessionFailed;
    }

    public bool IsAvailable => _steam.IsInitialized;

    public bool SendToUser(ulong steamId, byte[] payload, bool snapshot)
    {
        if (!IsAvailable || payload.Length == 0)
        {
            return false;
        }

        IntPtr buffer = Marshal.AllocHGlobal(payload.Length);
        try
        {
            Marshal.Copy(payload, 0, buffer, payload.Length);
            SteamNetworkingIdentity identity = new();
            identity.SetSteamID(new CSteamID(steamId));
            int channel = snapshot ? SnapshotChannel : InputChannel;
            EResult result = SteamNetworkingMessages.SendMessageToUser(
                ref identity,
                buffer,
                (uint)payload.Length,
                SendFlags,
                channel);
            return result == EResult.k_EResultOK;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public IReadOnlyList<ReceivedNetworkPacket> ReceiveAll()
    {
        List<ReceivedNetworkPacket> packets = new();
        if (!IsAvailable)
        {
            return packets;
        }

        ReceiveChannel(packets, InputChannel, snapshot: false);
        ReceiveChannel(packets, SnapshotChannel, snapshot: true);
        return packets;
    }

    public void CloseAllSessions()
    {
        if (!IsAvailable)
        {
            return;
        }

        // Sessions close automatically when leaving lobby; explicit close is optional for v1.
    }

    private static void ReceiveChannel(List<ReceivedNetworkPacket> packets, int channel, bool snapshot)
    {
        IntPtr[] messagePointers = new IntPtr[16];
        int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(channel, messagePointers, messagePointers.Length);
        for (int i = 0; i < count; i++)
        {
            IntPtr messagePtr = messagePointers[i];
            if (messagePtr == IntPtr.Zero)
            {
                continue;
            }

            try
            {
                SteamNetworkingMessage_t message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messagePtr)!;
                if (message.m_pData == IntPtr.Zero || message.m_cbSize <= 0)
                {
                    continue;
                }

                byte[] payload = new byte[message.m_cbSize];
                Marshal.Copy(message.m_pData, payload, 0, message.m_cbSize);
                ulong senderSteamId = message.m_identityPeer.GetSteamID64();
                packets.Add(new ReceivedNetworkPacket(senderSteamId, payload, snapshot));
            }
            finally
            {
                SteamNetworkingMessage_t.Release(messagePtr);
            }
        }
    }

    private static void OnNetworkingSessionRequest(SteamNetworkingMessagesSessionRequest_t data)
    {
        MultiplayerDebug.LogNet(
            $"ConnectionOpened session accepted peer={data.m_identityRemote.GetSteamID64()}");
        SteamNetworkingMessages.AcceptSessionWithUser(ref data.m_identityRemote);
    }

    private static void OnNetworkingSessionFailed(SteamNetworkingMessagesSessionFailed_t data)
    {
        MultiplayerDebug.LogError(
            "Net",
            $"ConnectionClosed session failed peer={data.m_info.m_identityRemote.GetSteamID64()} " +
            $"state={data.m_info.m_eState} reason={data.m_info.m_eEndReason}");
    }
}

public readonly record struct ReceivedNetworkPacket(ulong SenderSteamId, byte[] Payload, bool IsSnapshot);
