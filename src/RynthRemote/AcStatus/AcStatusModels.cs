namespace RynthRemote.AcStatus;

/// Consumer-side mirror of the RynthCore StatusAgent payload (schema
/// "rynthcore.status-agent/1") served at GET &lt;url&gt;/status. Deserialized
/// case-insensitively, so the agent's camelCase JSON maps onto these PascalCase
/// properties without per-field attributes. See RynthCore docs/STATUS_AGENT.md.
public sealed class AcStatusPayload
{
    public string? Schema { get; set; }
    public string? Host { get; set; }
    public string? AgentVersion { get; set; }
    public DateTimeOffset? GeneratedAtUtc { get; set; }
    public int ClientCount { get; set; }
    public List<AcClientStatus> Clients { get; set; } = new();
}

/// One AC client's rolled-up status. Bot-derived fields (MacroRunning, BotAction,
/// Target, Player, profiles) are only populated when Source == "status-file";
/// for the "heartbeat-log" fallback they're empty/false.
public sealed class AcClientStatus
{
    public int Pid { get; set; }
    public string? Host { get; set; }
    public string? Account { get; set; }
    public string? Character { get; set; }
    public string? Server { get; set; }

    /// botting | idle | running | loading | wedged | hung | dead | unknown.
    public string State { get; set; } = "unknown";
    public bool Healthy { get; set; }
    /// Seconds since this client's snapshot last refreshed (large ⇒ stale).
    public double AgeSec { get; set; }
    /// "status-file" (rich) or "heartbeat-log" (basic fallback).
    public string? Source { get; set; }

    public long UptimeSec { get; set; }
    public int Fps { get; set; }
    public int PluginTicksPerSec { get; set; }
    public long WorkingSetMB { get; set; }
    public bool InWorld { get; set; }

    public bool MacroRunning { get; set; }
    public string? CurrentState { get; set; }
    public string? BotAction { get; set; }
    public string? Profile { get; set; }
    public string? NavProfile { get; set; }
    public string? LootProfile { get; set; }
    public string? MetaProfile { get; set; }
    public string? Target { get; set; }
    public AcVitals? Player { get; set; }

    // Control state (status-file clients) for the on-screen switches + profile pickers.
    public bool CombatEnabled { get; set; }
    public bool BuffingEnabled { get; set; }
    public bool NavigationEnabled { get; set; }
    public bool LootingEnabled { get; set; }
    public bool MetaEnabled { get; set; }
    public List<string>? NavProfiles { get; set; }
    public List<string>? LootProfiles { get; set; }
    public List<string>? MetaProfiles { get; set; }
    public int SelectedNavIdx { get; set; } = -1;
    public int SelectedLootIdx { get; set; } = -1;
    public int SelectedMetaIdx { get; set; } = -1;

    public long QueueDropped { get; set; }
    public long Reconciles { get; set; }
    public long ForceClears { get; set; }

    // Session stats (status-file clients only). Deaths is all-time; rates are session averages.
    public int Deaths { get; set; }
    public double VitaePct { get; set; }
    public double KillsPerHour { get; set; }
    public double XpPerHour { get; set; }
    public double LuminancePerHour { get; set; }

    // Activity / health (status-file clients only). Session-scoped counters reset per launch.
    public int DeathsSession { get; set; }
    public long XpSession { get; set; }
    public double BurdenPct { get; set; }
    public string? Area { get; set; }
    public int SessionKills { get; set; }
    /// Seconds since the last kill; -1 = no kill yet this session.
    public int SecsSinceLastKill { get; set; } = -1;
    /// Main-pack empty slots; -1 = unknown.
    public int FreeSlots { get; set; } = -1;
    /// True when the in-game RynthCore/RynthAi UI is hidden (remote "Hide UI").
    public bool UiHidden { get; set; }
    /// True when the client's game window is minimized on the PC — its stream can't be captured
    /// (a minimized D3D9 window has no surface), so the app shows a "restore on PC" placeholder.
    public bool IsMinimized { get; set; }
    /// Total scarab casting components in inventory; -1 = unknown.
    public int Scarabs { get; set; } = -1;
    /// Total prismatic tapers in inventory; -1 = unknown.
    public int Tapers { get; set; } = -1;
    /// Per-tier scarab breakdown (name + count); null/empty when unknown.
    public List<AcScarabCount>? ScarabsByType { get; set; }
    /// Worn/wielded gear (name + instance id + slot mask); null/empty when unknown.
    public List<AcEquipItem>? Equipment { get; set; }
    /// Recent chat lines (oldest → newest); null/empty when unknown.
    public List<AcChatLine>? RecentChat { get; set; }
    /// Most recent engine warning/error text, or null if none this session.
    public string? LastIssue { get; set; }
    /// Seconds since LastIssue; -1 = none.
    public long LastIssueAgeSec { get; set; } = -1;

    /// Display name: character if known, else account, else PID.
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Character) ? Character!
        : !string.IsNullOrWhiteSpace(Account) ? Account!
        : $"PID {Pid}";
}

/// One captured chat line (text + AC chat-type for colouring).
public sealed class AcChatLine
{
    [System.Text.Json.Serialization.JsonPropertyName("t")] public string Text { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("c")] public int Type { get; set; }

    /// The bot's own [RynthAi]/[Rynth…] status output.
    private bool IsRynth => Text.StartsWith("[Rynth", System.StringComparison.Ordinal);

    /// Which standard chat tab this line belongs to — mirrors AC's default chat-type → tab routing.
    /// (The user's exact in-game tab config isn't readable from the client, so this is the standard set.)
    public string Route =>
        IsRynth ? "Rynth"
        : Type switch
        {
            0x06 or 0x07 or 0x11 or 0x15 or 0x16 => "Combat",                          // combat / magic / spellcasting
            0x00 or 0x05 or 0x0D or 0x14 or 0x17 or 0x18 or 0x19 or 0x1F => "System",   // broadcast / system / advancement / craft …
            0x08 or 0x09 => "Channels",                                                // turbine channels (low ids)
            0x02 or 0x03 or 0x04 or 0x0A or 0x0B or 0x0C or 0x12 or 0x13 => "Chat",     // speech / tell / social / emote / alleg / fellow
            >= 0x1B and <= 0x2F => "Channels",                                         // turbine channels (high range)
            _ => "Other",
        };

    /// Colour by common AC chat type (speech/tell/combat/magic/system/social/channel), else default text.
    public string Color =>
        IsRynth ? "#5eead4"                        // Rynth bot output
        : Type switch
        {
            3 or 4 => "#86efac",                   // tell (in / out)
            6 or 0x15 or 0x16 => "#fca5a5",        // combat
            7 or 0x11 => "#c4b5fd",                // magic / spellcasting
            5 => "#94a3b8",                        // system event
            0x0A or 0x0B or 0x0C => "#fcd34d",     // social / emote
            >= 0x1B and <= 0x2F => "#7dd3fc",      // channels (general/trade/allegiance/etc.)
            _ => "#e6ecf7",                        // speech / default
        };
}

/// One worn/wielded item with its full appraisal (Assess/Identify data).
public sealed class AcEquipItem
{
    public string Name { get; set; } = "";
    public uint Id { get; set; }
    public int Slot { get; set; }
    public int ArmorLevel { get; set; }
    public List<double>? Resist { get; set; }   // 7: slash,pierce,bludge,cold,fire,acid,electric
    public int Value { get; set; }
    public int Burden { get; set; }
    public int Workmanship { get; set; }
    public int Material { get; set; }
    public int MaxMana { get; set; }
    public int CurMana { get; set; }
    public int Damage { get; set; }
    public int DamageType { get; set; }
    public double WeaponDef { get; set; }
    public double MissileDef { get; set; }
    public double MagicDef { get; set; }
    public double Variance { get; set; }
    public double ElementalMod { get; set; }
    public List<string>? Spells { get; set; }
    public string? LongDesc { get; set; }

    /// AC-style hex instance id, e.g. "0x50001234".
    public string HexId => "0x" + Id.ToString("X8");
    public bool IsArmor => ArmorLevel > 0;
    public bool IsWeapon => Damage > 0;
    /// True if there's any appraisal detail worth expanding to.
    public bool HasAppraisal => ArmorLevel > 0 || Damage > 0 || Value > 0 || Workmanship > 0
        || (Spells is { Count: > 0 }) || !string.IsNullOrWhiteSpace(LongDesc);

    private static readonly string[] ResistLabels = { "Slash", "Pierce", "Bludge", "Cold", "Fire", "Acid", "Elec" };
    /// Non-trivial resistances (mod != 1.0) as "Fire ×1.20" — the banes/vulnerabilities worth seeing.
    public IEnumerable<(string Label, double Mod)> NotableResists()
    {
        if (Resist is null || Resist.Count < 7) yield break;
        for (int i = 0; i < 7; i++)
            if (Math.Abs(Resist[i] - 1.0) > 0.001)
                yield return (ResistLabels[i], Resist[i]);
    }

    private static readonly Dictionary<int, string> DamageTypes = new()
    {
        [1] = "Slash", [2] = "Pierce", [4] = "Bludgeon", [8] = "Cold",
        [16] = "Fire", [32] = "Acid", [64] = "Electric", [1024] = "Nether",
    };
    public string DamageTypeName()
    {
        if (DamageType == 0) return "";
        var parts = DamageTypes.Where(kv => (DamageType & kv.Key) != 0).Select(kv => kv.Value).ToList();
        return parts.Count > 0 ? string.Join("/", parts) : DamageType.ToString();
    }
}

/// One scarab tier and its inventory count.
public sealed class AcScarabCount
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
    /// Compact tier label for the tile (e.g. "Lead Scarab" -> "lead").
    public string ShortLabel =>
        Name.Replace(" Scarab", "", StringComparison.OrdinalIgnoreCase).Trim() is { Length: > 0 } s
            ? s.ToLowerInvariant() : Name.ToLowerInvariant();
}

public sealed class AcVitals
{
    public uint Hp { get; set; }
    public uint MaxHp { get; set; }
    public uint St { get; set; }
    public uint MaxSt { get; set; }
    public uint Mn { get; set; }
    public uint MaxMn { get; set; }
}

/// Consumer-side mirror of the StatusAgent's GET &lt;url&gt;/runs payload (schema "rynthcore.runs/1") —
/// the play-session history for the Archive tab. Deserialized case-insensitively.
public sealed class AcRunsPayload
{
    public string? Schema { get; set; }
    public string? Host { get; set; }
    public DateTimeOffset? GeneratedAtUtc { get; set; }
    public int Count { get; set; }
    public List<AcRun> Runs { get; set; } = new();
}

/// One play session (login → exit) with its final/last stats.
public sealed class AcRun
{
    public string? RunId { get; set; }
    public int Pid { get; set; }
    public string? Account { get; set; }
    public string? Character { get; set; }
    public string? Server { get; set; }
    public DateTimeOffset? StartUtc { get; set; }
    /// Null while the run is still in progress.
    public DateTimeOffset? EndUtc { get; set; }
    public long DurationSec { get; set; }
    public int Kills { get; set; }
    public double KillsPerHour { get; set; }
    public long Xp { get; set; }
    public double XpPerHour { get; set; }
    public double LuminancePerHour { get; set; }
    /// Deaths during this session (not all-time).
    public int Deaths { get; set; }
    public double VitaePct { get; set; }
    public string? Area { get; set; }
    /// True for the live, not-yet-finished run (shown pinned at the top, refreshes each cycle).
    public bool Ongoing { get; set; }

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Character) ? Character!
        : !string.IsNullOrWhiteSpace(Account) ? Account!
        : $"PID {Pid}";

    /// Stable key for expand/collapse + @key (falls back to pid+start if RunId is missing).
    public string Key => !string.IsNullOrWhiteSpace(RunId) ? RunId! : $"{Pid}-{StartUtc?.ToUnixTimeSeconds()}";
}

/// Consumer-side mirror of the StatusAgent's GET &lt;url&gt;/maps payload (schema "rynthcore.maps/1") —
/// the list of baked dungeon floor-plan maps the agent can serve as PNGs. Deserialized case-insensitively.
public sealed class AcMapsPayload
{
    public string? Schema { get; set; }
    public int Count { get; set; }
    public List<AcMapEntry> Maps { get; set; } = new();
}

/// One baked dungeon floor-plan: landblock + Z-layer + the raster's ABSOLUTE-world-frame bounds (grid-cell
/// units; 0.5 world-units/pixel; row 0 = north). The world→pixel transform for an overlay dot is
/// pixelX = wx/0.5 - XMin, pixelY = (H-1) - (wy/0.5 - YMin).
public sealed class AcMapEntry
{
    public string? Landblock { get; set; }   // "0000002B"
    public int Layer { get; set; }
    public long Bytes { get; set; }
    public DateTimeOffset? Mtime { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public int XMin { get; set; }
    public int YMin { get; set; }
    public string? Name { get; set; }

    /// Parsed landblock id (hex) for matching a client's current landblock; 0 if unparseable.
    public uint LandblockId => uint.TryParse(Landblock, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0;
    /// Short floor label, 1-based, mirroring the in-game "F1..Fn".
    public string FloorLabel => "F" + (Layer + 1);
}
