#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace ColorBlocks;

/// <summary>
/// Verbose multiplayer lifecycle logging + F3 panel diagnostics.
/// No LocalPlayer/RemotePlayer types exist — roles are flags on Player / PartyMember.
/// No spawn packets — players spawn from party roster before net traffic.
/// </summary>
public static class MultiplayerDebug
{
    public const string Tag = "[MP]";

    private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
    private static double _sessionStartSeconds;
    private static double _lastRateSampleSeconds;
    private static long _ratePacketsSentBase;
    private static long _ratePacketsReceivedBase;
    private static long _rateSnapshotsAppliedBase;
    private static double _lastStallLogSeconds;
    private static double _lastTickLogSeconds;
    private static bool _entityCountValidated;
    private static bool _simulationRunningLogged;
    private static bool _ropeReplicatedLogged;
    private static long _rateSnapshotsSentBase;
    private static long _rateSnapshotsReceivedBase;

    public static long PacketsSent { get; private set; }
    public static long PacketsReceived { get; private set; }
    public static long BytesSent { get; private set; }
    public static long BytesReceived { get; private set; }
    public static long InputPacketsSent { get; private set; }
    public static long InputPacketsReceived { get; private set; }
    public static long SnapshotPacketsSent { get; private set; }
    public static long SnapshotPacketsReceived { get; private set; }
    public static long SnapshotsApplied { get; private set; }
    public static long MissingPlayerSnapshotHits { get; private set; }
    public static long MissingRopeSnapshotHits { get; private set; }
    public static bool SimulationStartedLogged { get; private set; }
    public static bool SnapshotConsumeStartedLogged { get; private set; }
    public static double PacketsSentPerSecond { get; private set; }
    public static double PacketsReceivedPerSecond { get; private set; }
    public static double SnapshotsPerSecond { get; private set; }
    public static List<string> StartupValidationErrors { get; } = new();

    public static void ResetSessionCounters()
    {
        PacketsSent = 0;
        PacketsReceived = 0;
        BytesSent = 0;
        BytesReceived = 0;
        InputPacketsSent = 0;
        InputPacketsReceived = 0;
        SnapshotPacketsSent = 0;
        SnapshotPacketsReceived = 0;
        SnapshotsApplied = 0;
        MissingPlayerSnapshotHits = 0;
        MissingRopeSnapshotHits = 0;
        SimulationStartedLogged = false;
        SnapshotConsumeStartedLogged = false;
        PacketsSentPerSecond = 0;
        PacketsReceivedPerSecond = 0;
        SnapshotsPerSecond = 0;
        StartupValidationErrors.Clear();
        _sessionStartSeconds = Clock.Elapsed.TotalSeconds;
        _lastRateSampleSeconds = _sessionStartSeconds;
        _ratePacketsSentBase = 0;
        _ratePacketsReceivedBase = 0;
        _rateSnapshotsAppliedBase = 0;
        _lastStallLogSeconds = 0;
        _lastTickLogSeconds = 0;
        _entityCountValidated = false;
        _simulationRunningLogged = false;
        _ropeReplicatedLogged = false;
        _rateSnapshotsSentBase = 0;
        _rateSnapshotsReceivedBase = 0;
    }

    public static void Log(string stage, string message)
    {
        Console.WriteLine($"{Tag}[{stage}] {message}");
        DiagnosticsLog.Write("INFO", stage, message);
    }

    public static void LogLobby(string message) => Log("Lobby", message);
    public static void LogParty(string message) => Log("Party", message);
    public static void LogSpawn(string message) => Log("Spawn", message);
    public static void LogNet(string message) => Log("Net", message);
    public static void LogRope(string message) => Log("Rope", message);
    public static void LogSim(string message) => Log("Sim", message);

    public static void LogWarn(string message)
    {
        Console.WriteLine($"{Tag}[WARN] {message}");
        DiagnosticsLog.Write("WARN", "MP", message);
    }

    public static void LogError(string stage, string message)
    {
        Console.WriteLine($"{Tag}[ERROR][{stage}] {message}");
        DiagnosticsLog.Write("ERROR", stage, message);
    }

    public static void RecordPacketSent(bool snapshot, int bytes)
    {
        PacketsSent++;
        BytesSent += bytes;
        if (snapshot)
        {
            SnapshotPacketsSent++;
        }
        else
        {
            InputPacketsSent++;
        }
    }

    public static void RecordPacketReceived(bool snapshot, int bytes)
    {
        PacketsReceived++;
        BytesReceived += bytes;
        if (snapshot)
        {
            SnapshotPacketsReceived++;
        }
        else
        {
            InputPacketsReceived++;
        }
    }

    public static void RecordSnapshotApplied()
    {
        SnapshotsApplied++;
    }

    public static void LogRopeReplicatedOnce(int networkId)
    {
        if (_ropeReplicatedLogged)
        {
            return;
        }

        _ropeReplicatedLogged = true;
        LogRope($"RopeReplicated first rope snapshot applied NetworkId={networkId}");
    }

    public static void RecordMissingPlayerSnapshot(int networkId)
    {
        MissingPlayerSnapshotHits++;
        if (MissingPlayerSnapshotHits <= 5 || (MissingPlayerSnapshotHits % 60) == 0)
        {
            LogWarn($"ApplySnapshot missing player NetworkId={networkId} (hit#{MissingPlayerSnapshotHits})");
        }
    }

    public static void RecordMissingRopeSnapshot(int networkId)
    {
        MissingRopeSnapshotHits++;
        if (MissingRopeSnapshotHits <= 5 || (MissingRopeSnapshotHits % 60) == 0)
        {
            LogWarn($"ApplySnapshot missing rope NetworkId={networkId} (hit#{MissingRopeSnapshotHits})");
        }
    }

    public static void LogSimulationStartedOnce(GameSession session, GameSimulation simulation)
    {
        if (SimulationStartedLogged)
        {
            return;
        }

        SimulationStartedLogged = true;
        LogSim(
            $"Gameplay init role={session.Role} tick={simulation.CurrentTick.Value} " +
            $"players={simulation.Players.Count} ropes={simulation.Ropes.Count} " +
            $"localOwner={session.LocalOwnerId} hostOwner={session.HostOwnerId}");
    }

    public static void LogSnapshotConsumeStartedOnce(GameSession session, int sequence, long tick)
    {
        if (SnapshotConsumeStartedLogged)
        {
            return;
        }

        SnapshotConsumeStartedLogged = true;
        LogNet(
            $"Client snapshot consume START role={session.Role} seq={sequence} tick={tick} " +
            $"(passive receive — no subscribe API)");
    }

    /// <summary>
    /// Every frame from GameScene. Samples pkt/s + snap/s once per second and,
    /// while an online session is exchanging traffic, writes a per-second stats line.
    /// </summary>
    public static void UpdateRates(int replicationQueueDepth = 0, bool onlineSession = false)
    {
        double now = Clock.Elapsed.TotalSeconds;
        double elapsed = now - _lastRateSampleSeconds;
        if (elapsed < 1.0)
        {
            return;
        }

        long snapshotsSentDelta = SnapshotPacketsSent - _rateSnapshotsSentBase;
        long snapshotsReceivedDelta = SnapshotPacketsReceived - _rateSnapshotsReceivedBase;
        PacketsSentPerSecond = (PacketsSent - _ratePacketsSentBase) / elapsed;
        PacketsReceivedPerSecond = (PacketsReceived - _ratePacketsReceivedBase) / elapsed;
        SnapshotsPerSecond = (SnapshotsApplied - _rateSnapshotsAppliedBase) / elapsed;
        _ratePacketsSentBase = PacketsSent;
        _ratePacketsReceivedBase = PacketsReceived;
        _rateSnapshotsAppliedBase = SnapshotsApplied;
        _rateSnapshotsSentBase = SnapshotPacketsSent;
        _rateSnapshotsReceivedBase = SnapshotPacketsReceived;
        _lastRateSampleSeconds = now;

        if (onlineSession)
        {
            DiagnosticsLog.Write(
                "INFO",
                "PacketStats",
                $"sent={PacketsSent} recv={PacketsReceived} " +
                $"bytesSent={BytesSent} bytesRecv={BytesReceived} " +
                $"snapSent={SnapshotPacketsSent} (+{snapshotsSentDelta}/s) " +
                $"snapRecv={SnapshotPacketsReceived} (+{snapshotsReceivedDelta}/s) " +
                $"replQueue={replicationQueueDepth}");
        }
    }

    /// <summary>Logs SimulationRunning once, when the sim first advances (host) or applies a snapshot (client).</summary>
    public static void LogSimulationRunningOnce(GameSession session, GameSimulation simulation)
    {
        if (_simulationRunningLogged)
        {
            return;
        }

        bool running = session.Role == GameSessionRole.Client
            ? SnapshotsApplied > 0
            : simulation.CurrentTick.Value > 0;
        if (!running)
        {
            return;
        }

        _simulationRunningLogged = true;
        LogSim($"SimulationRunning role={session.Role} tick={simulation.CurrentTick.Value}");
    }

    /// <summary>
    /// Entity validation dump — call once right after entering gameplay.
    /// Host and client dumps are directly comparable line-by-line.
    /// </summary>
    public static void DumpEntityState(GameSession session, GameSimulation simulation)
    {
        Log("EntityDump", $"BEGIN role={session.Role} tick={simulation.CurrentTick.Value}");
        foreach (Player player in simulation.Players)
        {
            Log(
                "EntityDump",
                $"Player NetworkId={player.NetworkId} OwnerId={player.OwnerId} " +
                $"{(player.IsLocal ? "Local" : "Remote")} pos=({player.Position.X:0.0},{player.Position.Y:0.0})");
        }

        foreach (Rope rope in simulation.Ropes)
        {
            Log(
                "EntityDump",
                $"Rope exists=true NetworkId={rope.NetworkId} " +
                $"attached=P{rope.StartPlayer.PlayerIndex + 1}(N{rope.StartPlayer.NetworkId})-" +
                $"P{rope.EndPlayer.PlayerIndex + 1}(N{rope.EndPlayer.NetworkId})");
        }

        int entityCount = simulation.Players.Count + simulation.Ropes.Count;
        Log(
            "EntityDump",
            $"END entityCount={entityCount} players={simulation.Players.Count} " +
            $"ropes={simulation.Ropes.Count} simTick={simulation.CurrentTick.Value}");
    }

    /// <summary>Periodic tick trace (5s): host tick vs replication tick. No prediction system exists.</summary>
    public static void LogTickPeriodic(GameSession session, GameSimulation simulation)
    {
        double now = Clock.Elapsed.TotalSeconds;
        if (now - _lastTickLogSeconds < 5.0)
        {
            return;
        }

        _lastTickLogSeconds = now;
        LogSim(
            $"TickTrace role={session.Role} simTick={simulation.CurrentTick.Value} " +
            $"replTick={simulation.LastSnapshot.Tick} replSeq={simulation.LastSnapshot.Sequence} " +
            $"snapsApplied={SnapshotsApplied} pktRx={PacketsReceived} pktTx={PacketsSent} " +
            $"missPlayer={MissingPlayerSnapshotHits} missRope={MissingRopeSnapshotHits}");
    }

    /// <summary>Client stall watchdog: no snapshots, or snapshots referencing entities we never spawned.</summary>
    public static void CheckClientStall(GameSession session, GameSimulation simulation)
    {
        if (session.Role != GameSessionRole.Client)
        {
            return;
        }

        double now = Clock.Elapsed.TotalSeconds;
        double sinceStart = now - _sessionStartSeconds;
        if (sinceStart < 3.0 || now - _lastStallLogSeconds < 5.0)
        {
            return;
        }

        if (SnapshotsApplied == 0)
        {
            _lastStallLogSeconds = now;
            LogError(
                "Replication",
                $"Client FROZEN: 0 snapshots applied after {sinceStart:0}s. " +
                $"pktRx={PacketsReceived} (snapPktRx={SnapshotPacketsReceived}). " +
                (PacketsReceived == 0
                    ? "Transport silent — host not sending or Steam session not established."
                    : "Packets arrive but decode/latch fails."));
        }
        else if (MissingPlayerSnapshotHits > 0 || MissingRopeSnapshotHits > 0)
        {
            _lastStallLogSeconds = now;
            LogError(
                "Replication",
                $"Simulation waiting on MISSING ENTITIES: snapshot references players/ropes never spawned locally " +
                $"(missPlayer={MissingPlayerSnapshotHits} missRope={MissingRopeSnapshotHits}). " +
                "Peer entity sets diverged at spawn — party rosters differed at level start.");
        }
    }

    /// <summary>First-snapshot cross-peer entity count check (client side). Snapshot = host truth.</summary>
    public static void ValidateEntityCountsFromSnapshot(GameSimulation simulation, GameSnapshot snapshot)
    {
        if (_entityCountValidated)
        {
            return;
        }

        _entityCountValidated = true;
        int localPlayers = simulation.Players.Count;
        int localRopes = simulation.Ropes.Count;
        if (snapshot.Players.Count == localPlayers && snapshot.Ropes.Count == localRopes)
        {
            LogSim(
                $"Peer entity count MATCH players={localPlayers} ropes={localRopes} (host snapshot agrees)");
            return;
        }

        LogError(
            "EntityCount",
            $"PEER MISMATCH: host snapshot players={snapshot.Players.Count} ropes={snapshot.Ropes.Count} " +
            $"vs local players={localPlayers} ropes={localRopes}. " +
            "Each peer spawned from its own party roster — rosters were NOT identical at level start.");
        StartupValidationErrors.Add(
            $"EntityCount: host={snapshot.Players.Count}p/{snapshot.Ropes.Count}r local={localPlayers}p/{localRopes}r");
    }

    /// <summary>
    /// Staged gameplay-start validation. Explicit ERROR naming failing stage.
    /// Call once from GameScene ctor after simulation built.
    /// </summary>
    public static void ValidateGameplayStart(
        SteamLobbyService lobby,
        PartyManager party,
        GameSession session,
        GameSimulation simulation)
    {
        StartupValidationErrors.Clear();
        bool online = session.Role is GameSessionRole.Host or GameSessionRole.Client;

        void Fail(string stage, string message)
        {
            LogError(stage, message);
            StartupValidationErrors.Add($"{stage}: {message}");
        }

        // Stage 1: lobby vs party membership.
        if (online)
        {
            int lobbyCount = lobby.GetLobbyMemberCount();
            HashSet<ulong> partyOwners = new();
            foreach (PartyMember m in party.Members)
            {
                if (m.OwningSteamId != 0)
                {
                    partyOwners.Add(m.OwningSteamId);
                }
            }

            if (lobbyCount > partyOwners.Count)
            {
                Fail(
                    "LobbySync",
                    $"Lobby has {lobbyCount} machines but party only covers {partyOwners.Count} steam ids — " +
                    "authoritative roster was NOT republished after member join.");
            }
        }

        // Stage 2: party composition.
        bool hasLocalMember = false;
        bool hasRemoteMember = false;
        foreach (PartyMember m in party.Members)
        {
            if (m.IsLocallyOwned)
            {
                hasLocalMember = true;
                if (m.MemberType == PartyMemberType.SteamRemote)
                {
                    Fail(
                        "PartySync",
                        $"Own member '{m.DisplayName}' is locally owned but typed SteamRemote — " +
                        "local input slot never published/merged into roster. Input will be None → frozen.");
                }
            }
            else
            {
                hasRemoteMember = true;
            }
        }

        if (!hasLocalMember)
        {
            Fail("PartySync", "No locally-owned party member — this peer cannot control anyone.");
        }

        if (online && !hasRemoteMember && lobby.GetLobbyMemberCount() > 1)
        {
            Fail(
                "PartySync",
                "Online session with >1 lobby machines but NO remote party member — remote peer missing from roster.");
        }

        // Stage 3: spawned players — host player + remote player exist.
        bool hostPlayerExists = false;
        bool remotePlayerExists = false;
        bool localPlayerExists = false;
        HashSet<int> networkIds = new();
        foreach (Player p in simulation.Players)
        {
            if (p.OwnerId == session.HostOwnerId)
            {
                hostPlayerExists = true;
            }

            if (p.IsRemote)
            {
                remotePlayerExists = true;
            }

            if (p.IsLocal)
            {
                localPlayerExists = true;
            }

            if (p.OwnerId == NetworkOwners.UnassignedOwnerId)
            {
                Fail("Ownership", $"Player N{p.NetworkId} has unassigned OwnerId.");
            }

            if (!networkIds.Add(p.NetworkId))
            {
                Fail("Ownership", $"Duplicate NetworkId {p.NetworkId} across spawned players.");
            }
        }

        foreach (Rope r in simulation.Ropes)
        {
            if (!networkIds.Add(r.NetworkId))
            {
                Fail("Ownership", $"Rope NetworkId {r.NetworkId} collides with another entity.");
            }
        }

        if (online)
        {
            if (!hostPlayerExists)
            {
                Fail("Spawn", $"No player owned by host (HostOwnerId={session.HostOwnerId}) was spawned.");
            }

            if (!localPlayerExists)
            {
                Fail("Spawn", "No LOCAL player spawned on this peer.");
            }

            if (!remotePlayerExists && lobby.GetLobbyMemberCount() > 1)
            {
                Fail(
                    "Spawn",
                    "No REMOTE player spawned although lobby has other machines — " +
                    "party roster at LockAssignments did not contain the remote peer.");
            }
        }

        // Stage 4: rope.
        int expectedRopes = Math.Max(0, simulation.Players.Count - 1);
        if (simulation.Ropes.Count != expectedRopes)
        {
            Fail(
                "Rope",
                $"Rope count {simulation.Ropes.Count} != expected {expectedRopes}. " +
                (simulation.Players.Count < 2
                    ? "Only one player spawned — rope chain needs ≥2 players (missing remote is upstream cause)."
                    : "Rope chain construction failed."));
        }

        // Stage 5: simulation ready.
        if (simulation.Players.Count == 0)
        {
            Fail("Simulation", "Zero players — simulation cannot start.");
        }

        if (StartupValidationErrors.Count == 0)
        {
            LogSim("ValidateGameplayStart OK — all stages passed");
        }
        else
        {
            LogError(
                "Validation",
                $"{StartupValidationErrors.Count} stage failure(s) at gameplay start — see [MP][ERROR] lines above.");
        }
    }

    public static List<string> BuildPanelLines(
        SteamLobbyService lobby,
        PartyManager party,
        GameSession session,
        GameSimulation simulation,
        GameNetworkCoordinator network,
        string levelId)
    {
        bool simRunning = SimulationStartedLogged
            || (session.Role != GameSessionRole.Client && simulation.CurrentTick.Value > 0)
            || SnapshotsApplied > 0;
        int entityCount = simulation.Players.Count + simulation.Ropes.Count;
        BuildInfo build = BuildInfo.Current;
        List<string> lines = new()
        {
            "MULTIPLAYER",
            $"VERSION {build.GameVersion} BUILD {build.ShortBuildId} COMMIT {build.GitCommit}",
            $"BUILD GUID {build.BuildGuid}",
            $"BUILD TS {build.BuildTimestampUtc} ({build.Configuration})",
            $"SESSION ID {DiagnosticsLog.SessionId}",
            $"HOST BUILD {SessionDiagnostics.HostBuildLabel} CLIENT BUILD {SessionDiagnostics.ClientBuildLabel}",
            $"BUILD MATCH {SessionDiagnostics.FormatMatch(SessionDiagnostics.BuildMatch)} " +
            $"LEVEL MATCH {SessionDiagnostics.FormatMatch(SessionDiagnostics.LevelMatch)}",
            $"SESSION {session.Role} STATE {session.State} NET {network.GetOnlineRoleLabel(session)}",
            $"SIM RUNNING {Bool(simRunning)} TICK {simulation.CurrentTick.Value} RATE {simulation.TickRate.TicksPerSecond}",
            $"ENTITIES {entityCount} (PLAYERS {simulation.Players.Count} ROPES {simulation.Ropes.Count})",
            $"SNAP APPLIED {SnapshotsApplied} SEQ {simulation.LastSnapshot.Sequence} REPL TICK {simulation.LastSnapshot.Tick}",
            $"PKT/S TX {PacketsSentPerSecond:0.0} RX {PacketsReceivedPerSecond:0.0} SNAP/S {SnapshotsPerSecond:0.0}",
            $"PKT SENT {PacketsSent} (IN {InputPacketsSent}/SNAP {SnapshotPacketsSent})",
            $"PKT RECV {PacketsReceived} (IN {InputPacketsReceived}/SNAP {SnapshotPacketsReceived})",
            $"INPUT BUF {simulation.InputBuffer.FrameCount} DROP {simulation.InputBuffer.DroppedFrameCount}",
            $"LOCAL OWNER {session.LocalOwnerId} HOST OWNER {session.HostOwnerId}",
            $"LEVEL {levelId} LOBBY {(lobby.IsInLobby ? lobby.CurrentLobbyId.ToString() : "NONE")}",
            string.Empty,
            $"LOBBY MEMBERS {lobby.GetLobbyMemberCount()}"
        };

        foreach (LobbyMemberInfo member in lobby.GetLobbyMembers())
        {
            string ownerTag = member.IsOwner ? " OWNER" : string.Empty;
            string selfTag = member.SteamId == lobby.LocalSteamId ? " YOU" : string.Empty;
            lines.Add($"  L {member.DisplayName} sid={member.SteamId}{ownerTag}{selfTag}");
        }

        lines.Add(string.Empty);
        lines.Add($"PARTY MEMBERS {party.Members.Count}/{PartyManager.MaxMembers} LOCKED {Bool(party.AssignmentsLocked)}");
        foreach (PartyMember member in party.Members)
        {
            string leader = member.IsLeader ? " LEADER" : string.Empty;
            string local = member.IsLocallyOwned ? "LOCAL" : "REMOTE";
            lines.Add(
                $"  P {member.DisplayName} {local} NET{member.NetworkPlayerId} OWNER{member.OwnerId} " +
                $"TYPE {member.MemberType}{leader}");
        }

        lines.Add(string.Empty);
        lines.Add($"SPAWNED PLAYERS {simulation.Players.Count}");
        foreach (Player player in simulation.Players)
        {
            string role = player.IsLocal ? "LocalPlayer" : "RemotePlayer";
            string ropeStatus = DescribePlayerRopeLinks(player, simulation.Ropes);
            lines.Add(
                $"  {role} P{player.PlayerIndex + 1} '{player.DisplayLabel}' " +
                $"N{player.NetworkId} O{player.OwnerId} " +
                $"{(player.IsLocal ? "LOCAL" : "REMOTE")} " +
                $"{(player.IsHostControlled ? "HOST-CTRL" : "PEER-CTRL")} " +
                $"ROPE {ropeStatus}");
        }

        lines.Add(string.Empty);
        lines.Add($"ROPES {simulation.Ropes.Count}");
        foreach (Rope rope in simulation.Ropes)
        {
            lines.Add(
                $"  Rope N{rope.NetworkId} O{rope.OwnerId} " +
                $"{(rope.IsLocal ? "LOCAL" : "REMOTE")} " +
                $"{(rope.IsHostControlled ? "HOST-CTRL" : "PEER-CTRL")} " +
                $"P{rope.StartPlayer.PlayerIndex + 1}-P{rope.EndPlayer.PlayerIndex + 1}");
        }

        lines.Add(string.Empty);
        lines.Add("DIAGNOSTICS");
        List<string> issues = DetectIssues(lobby, party, session, simulation);
        if (issues.Count == 0 && StartupValidationErrors.Count == 0)
        {
            lines.Add("  OK — no missing remotes / ownership mismatches");
        }

        foreach (string error in StartupValidationErrors)
        {
            lines.Add($"  ! {error}");
        }

        foreach (string issue in issues)
        {
            lines.Add($"  ! {issue}");
        }

        if (MissingPlayerSnapshotHits > 0 || MissingRopeSnapshotHits > 0)
        {
            lines.Add(
                $"  ! Snapshot miss hits player={MissingPlayerSnapshotHits} rope={MissingRopeSnapshotHits}");
        }

        if (session.Role == GameSessionRole.Client && SnapshotsApplied == 0)
        {
            lines.Add("  ! CLIENT WAITING — zero snapshots applied");
        }

        return lines;
    }

    public static List<string> DetectIssues(
        SteamLobbyService lobby,
        PartyManager party,
        GameSession session,
        GameSimulation simulation)
    {
        List<string> issues = new();

        if (session.Role is not (GameSessionRole.Host or GameSessionRole.Client))
        {
            return issues;
        }

        // Steam lobby peers vs party owning steam ids (splitscreen: many party rows per steam id).
        HashSet<ulong> lobbySteamIds = new();
        foreach (LobbyMemberInfo lobbyMember in lobby.GetLobbyMembers())
        {
            if (lobbyMember.SteamId != 0)
            {
                lobbySteamIds.Add(lobbyMember.SteamId);
            }
        }

        HashSet<ulong> partyOwnerSteamIds = new();
        foreach (PartyMember member in party.Members)
        {
            if (member.OwningSteamId != 0)
            {
                partyOwnerSteamIds.Add(member.OwningSteamId);
            }
        }

        foreach (ulong steamId in lobbySteamIds)
        {
            if (!partyOwnerSteamIds.Contains(steamId))
            {
                issues.Add($"Lobby steam {steamId} missing from party owners");
            }
        }

        foreach (ulong steamId in partyOwnerSteamIds)
        {
            if (lobby.IsInLobby && !lobbySteamIds.Contains(steamId))
            {
                issues.Add($"Party owner steam {steamId} missing from lobby");
            }
        }

        // Every party member must have a spawned player with matching NetworkId / OwnerId / local flag.
        Dictionary<int, Player> playersByNetworkId = new();
        foreach (Player player in simulation.Players)
        {
            playersByNetworkId[player.NetworkId] = player;
        }

        foreach (PartyMember member in party.Members)
        {
            if (member.NetworkPlayerId <= 0)
            {
                issues.Add($"Party '{member.DisplayName}' has NetworkPlayerId=0 (not locked?)");
                continue;
            }

            if (!playersByNetworkId.TryGetValue(member.NetworkPlayerId, out Player? player))
            {
                string kind = member.IsLocallyOwned ? "LocalPlayer" : "RemotePlayer";
                issues.Add($"Missing spawned {kind} for party '{member.DisplayName}' NET{member.NetworkPlayerId}");
                continue;
            }

            if (player.OwnerId != member.OwnerId)
            {
                issues.Add(
                    $"Owner mismatch '{member.DisplayName}' party.Owner={member.OwnerId} player.Owner={player.OwnerId}");
            }

            if (player.IsLocal != member.IsLocallyOwned)
            {
                issues.Add(
                    $"Local/Remote mismatch '{member.DisplayName}' party.Local={member.IsLocallyOwned} player.Local={player.IsLocal}");
            }
        }

        // Orphan spawned players not in party roster NetworkPlayerId set.
        HashSet<int> partyNetworkIds = new();
        foreach (PartyMember member in party.Members)
        {
            if (member.NetworkPlayerId > 0)
            {
                partyNetworkIds.Add(member.NetworkPlayerId);
            }
        }

        foreach (Player player in simulation.Players)
        {
            if (partyNetworkIds.Count > 0 && !partyNetworkIds.Contains(player.NetworkId))
            {
                issues.Add(
                    $"Orphan {(player.IsLocal ? "LocalPlayer" : "RemotePlayer")} N{player.NetworkId} not in party NetworkPlayerIds");
            }
        }

        // Expected rope count for N players: N-1 adjacent chain.
        int expectedRopes = Math.Max(0, simulation.Players.Count - 1);
        if (simulation.Ropes.Count != expectedRopes)
        {
            issues.Add($"Rope count {simulation.Ropes.Count} != expected {expectedRopes}");
        }

        // Host-owned ropes should use HostOwnerId.
        foreach (Rope rope in simulation.Ropes)
        {
            if (rope.OwnerId != session.HostOwnerId)
            {
                issues.Add($"Rope N{rope.NetworkId} Owner={rope.OwnerId} != HostOwner={session.HostOwnerId}");
            }

            if (!rope.IsHostControlled)
            {
                issues.Add($"Rope N{rope.NetworkId} not host-controlled");
            }
        }

        return issues;
    }

    private static string DescribePlayerRopeLinks(Player player, IReadOnlyList<Rope> ropes)
    {
        int links = 0;
        StringBuilder ids = new();
        foreach (Rope rope in ropes)
        {
            if (rope.StartPlayer == player || rope.EndPlayer == player)
            {
                if (links > 0)
                {
                    ids.Append(',');
                }

                ids.Append('N').Append(rope.NetworkId);
                links++;
            }
        }

        return links == 0 ? "NONE" : $"OK({links}:{ids})";
    }

    private static string Bool(bool value) => value ? "true" : "false";
}
