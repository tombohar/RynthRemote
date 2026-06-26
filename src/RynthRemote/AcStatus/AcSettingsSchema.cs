namespace RynthRemote.AcStatus;

/// How a setting renders + writes. Bool=toggle, Int/Float=stepper, Enum=segmented, ReadOnly=text.
public enum AcSettingType { Bool, Int, Float, Enum, ReadOnly }

/// Presentation metadata for one bridged RynthAi setting. Keys EXACTLY match SettingsBridgePayload property
/// names (the producer's BuildSettingsJson/ApplySettingsJson round-trip uses the same names). Min/Max mirror
/// the in-AC SettingsPanel rows = the producer's authoritative clamp; the app clamps client-side only for UX.
public sealed record AcSettingDescriptor(
    string Key, string Label, string Group, AcSettingType Type,
    double Min = 0, double Max = 0, double Step = 1, string[]? Options = null);

/// The static descriptor table for the phone settings form. The VALUES come live from GET /settings; this
/// supplies grouping/labels/ranges/enum-options. Group order = the in-AC SettingsPanel tab order.
public static class AcSettingsSchema
{
    public static readonly string[] Groups =
        { "Display", "UI", "Misc", "Recharge", "Melee Combat", "Spell Combat", "Ranges", "Navigation", "Buffing", "Crafting", "Looting" };

    // The single setting the phone may turn ON but never OFF (matches the producer block; buffing keeps you alive).
    public const string BuffingKey = "EnableBuffing";

    private static readonly string[] Heights = { "Low", "Medium", "High" };
    private static readonly string[] MoveModes = { "Legacy", "Tier 1", "Tier 2" };
    private static readonly string[] LootFrom = { "My Kills", "Fellowship", "All Corpses" };

    private static AcSettingDescriptor B(string k, string g, string l) => new(k, l, g, AcSettingType.Bool);
    private static AcSettingDescriptor I(string k, string g, string l, double min, double max) => new(k, l, g, AcSettingType.Int, min, max, 1);
    private static AcSettingDescriptor F(string k, string g, string l, double min, double max, double step) => new(k, l, g, AcSettingType.Float, min, max, step);
    private static AcSettingDescriptor E(string k, string g, string l, string[] o) => new(k, l, g, AcSettingType.Enum, 0, o.Length - 1, 1, o);
    private static AcSettingDescriptor RO(string k, string g, string l) => new(k, l, g, AcSettingType.ReadOnly);

    public static readonly AcSettingDescriptor[] All =
    {
        // Display
        B("ShowTargetStaminaMana", "Display", "Show Target Stam / Mana"),
        // UI
        B("SuppressRetailRadar", "UI", "Hide Retail Radar"),
        B("ShowRynthRadar", "UI", "Show RynthRadar"),
        B("RadarClickThrough", "UI", "Radar Click-Through"),
        B("ShowRynthChat", "UI", "Show RynthChat"),
        B("ChatClickThrough", "UI", "Chat Click-Through"),
        B("SuppressRetailPowerbar", "UI", "Hide Retail Power Bar"),
        // Misc
        B("EnableFPSLimit", "Misc", "FPS Limit"),
        I("TargetFPSFocused", "Misc", "Focused FPS", 10, 240),
        I("TargetFPSBackground", "Misc", "Background FPS", 5, 60),
        B("EnableAutocram", "Misc", "Auto Cram"),
        B("PeaceModeWhenIdle", "Misc", "Peace Mode When Idle"),
        B("StartMacroOnLogin", "Misc", "Start Macro On Login"),
        B("PatrolOnLogin", "Misc", "Patrol On Login"),
        B("EnableRaycasting", "Misc", "Enable Raycasting"),
        B("UseArcs", "Misc", "Use Arcs for Missile LoS"),
        F("BowArcVelocity", "Misc", "Bow Arc Velocity", 10, 60, 1),
        F("CrossbowArcVelocity", "Misc", "Crossbow Arc Velocity", 10, 80, 1),
        F("AtlatlArcVelocity", "Misc", "Atlatl Arc Velocity", 10, 60, 1),
        F("MagicArcVelocity", "Misc", "Magic Arc Velocity", 10, 60, 1),
        I("BlacklistAttempts", "Misc", "Blacklist Attempts", 1, 20),
        I("BlacklistTimeoutSec", "Misc", "Blacklist Timeout (s)", 5, 120),
        I("BlacklistCastSettleMs", "Misc", "Cast Settle (ms)", 500, 5000),
        I("TargetNoProgressTimeoutSec", "Misc", "No-Progress Timeout (s)", 0, 300),
        I("GiveQueueIntervalMs", "Misc", "Give Interval (ms)", 50, 2000),
        // Recharge
        I("HealAt", "Recharge", "Heal At %", 0, 100),
        I("RestamAt", "Recharge", "Re-stam At %", 0, 100),
        I("GetManaAt", "Recharge", "Get Mana At %", 0, 100),
        I("TopOffHP", "Recharge", "Top HP %", 0, 100),
        I("TopOffStam", "Recharge", "Top Stam %", 0, 100),
        I("TopOffMana", "Recharge", "Top Mana %", 0, 100),
        I("HealOthersAt", "Recharge", "Heal Others %", 0, 100),
        I("RestamOthersAt", "Recharge", "Re-stam Others %", 0, 100),
        I("InfuseOthersAt", "Recharge", "Infuse Others %", 0, 100),
        // Melee Combat
        B("UseRecklessness", "Melee Combat", "Use Recklessness"),
        I("MeleeAttackPower", "Melee Combat", "Melee Power % (-1 = auto)", -1, 100),
        E("MeleeAttackHeight", "Melee Combat", "Melee Attack Height", Heights),
        I("MissileAttackPower", "Melee Combat", "Missile Power % (-1 = auto)", -1, 100),
        E("MissileAttackHeight", "Melee Combat", "Missile Attack Height", Heights),
        B("UseNativeAttack", "Melee Combat", "Use Native Attack"),
        B("SummonPets", "Melee Combat", "Summon Pets"),
        I("PetMinMonsters", "Melee Combat", "Pet Min Monsters", 1, 20),
        // Spell Combat
        I("SpellCastIntervalMs", "Spell Combat", "Buff Spell Interval (ms)", 100, 1500),
        I("AttackSpellIntervalMs", "Spell Combat", "Attack Spell Delay (ms)", 250, 5000),
        B("CastDispelSelf", "Spell Combat", "Cast Dispel Self"),
        I("MinRingTargets", "Spell Combat", "Min Ring Targets", 1, 20),
        I("MinSkillLevelTier1", "Spell Combat", "War Tier 1 Skill", 1, 500),
        I("MinSkillLevelTier2", "Spell Combat", "War Tier 2 Skill", 1, 500),
        I("MinSkillLevelTier3", "Spell Combat", "War Tier 3 Skill", 1, 500),
        I("MinSkillLevelTier4", "Spell Combat", "War Tier 4 Skill", 1, 500),
        I("MinSkillLevelTier5", "Spell Combat", "War Tier 5 Skill", 1, 500),
        I("MinSkillLevelTier6", "Spell Combat", "War Tier 6 Skill", 1, 500),
        I("MinSkillLevelTier7", "Spell Combat", "War Tier 7 Skill", 1, 500),
        I("MinSkillLevelTier8", "Spell Combat", "War Tier 8 Skill", 1, 500),
        // Ranges
        I("MonsterRange", "Ranges", "Monster Range", 1, 200),
        I("MonsterDisengageRange", "Ranges", "Monster Disengage Range", 0, 200),
        I("RingRange", "Ranges", "Ring Range", 1, 50),
        I("ApproachRange", "Ranges", "Approach Range", 1, 50),
        F("CorpseApproachRangeMax", "Ranges", "Corpse Max (yd)", 0.5, 50, 0.5),
        F("CorpseApproachRangeMin", "Ranges", "Corpse Min (yd)", 0.5, 20, 0.5),
        // Navigation
        B("BoostNavPriority", "Navigation", "Boost Nav Priority"),
        F("FollowNavMin", "Navigation", "Follow / Nav Min (yd)", 0.5, 20, 0.5),
        F("NavRingThickness", "Navigation", "Ring Thickness", 1, 16, 1),
        F("NavLineThickness", "Navigation", "Line Thickness", 1, 16, 1),
        F("NavHeightOffset", "Navigation", "Height Offset", -5, 5, 0.5),
        F("NavSlopeSink", "Navigation", "Slope Sink", 0, 8, 0.5),
        B("ShowTerrainPassability", "Navigation", "Show Terrain Passability"),
        B("OpenDoors", "Navigation", "Open Doors While Navigating"),
        F("OpenDoorRange", "Navigation", "Door Detection Range (yd)", 0.1, 70, 1),
        B("AutoUnlockDoors", "Navigation", "Auto-Unlock Doors"),
        E("MovementMode", "Navigation", "Movement Mode", MoveModes),
        F("NavStopTurnAngle", "Navigation", "Stop & Turn Angle", 1, 90, 1),
        F("NavResumeTurnAngle", "Navigation", "Resume Run Angle", 1, 45, 1),
        F("NavDeadZone", "Navigation", "Dead Zone", 0.5, 20, 0.5),
        F("NavSweepMult", "Navigation", "Sweep Detect Mult", 0.5, 10, 0.5),
        F("NavLookaheadYards", "Navigation", "Lookahead (yd)", 0, 30, 1),
        F("NavTurnRateDegPerSec", "Navigation", "Turn Rate (deg/s)", 30, 720, 10),
        F("NavTier1TurnSpeed", "Navigation", "Tier 1 Turn Speed", 0.5, 15, 0.5),
        F("PostPortalDelaySec", "Navigation", "Post-Portal Delay (s)", 0, 30, 0.5),
        F("T2Speed", "Navigation", "T2 Speed", 0.1, 5, 0.1),
        F("T2WalkWithinYd", "Navigation", "T2 Walk Within (yd)", 1, 50, 1),
        F("T2DistanceTo", "Navigation", "T2 Stop Distance (yd)", 0.1, 10, 0.1),
        F("T2ReissueMs", "Navigation", "T2 Reissue (ms)", 100, 10000, 100),
        F("T2MaxRangeYd", "Navigation", "T2 Max Range (yd)", 50, 2000, 50),
        I("T2MaxLandblocks", "Navigation", "T2 Max Landblocks", 1, 20),
        // Buffing
        B("EnableBuffing", "Buffing", "Enable Buffing"),
        B("RebuffWhenIdle", "Buffing", "Rebuff When Idle"),
        I("RebuffSecondsRemaining", "Buffing", "Rebuff With (s left)", 30, 1800),
        I("BuffMinSkillLevelTier1", "Buffing", "Buff Tier 1 Skill", 1, 500),
        I("BuffMinSkillLevelTier2", "Buffing", "Buff Tier 2 Skill", 1, 500),
        I("BuffMinSkillLevelTier3", "Buffing", "Buff Tier 3 Skill", 1, 500),
        I("BuffMinSkillLevelTier4", "Buffing", "Buff Tier 4 Skill", 1, 500),
        I("BuffMinSkillLevelTier5", "Buffing", "Buff Tier 5 Skill", 1, 500),
        I("BuffMinSkillLevelTier6", "Buffing", "Buff Tier 6 Skill", 1, 500),
        I("BuffMinSkillLevelTier7", "Buffing", "Buff Tier 7 Skill", 1, 500),
        I("BuffMinSkillLevelTier8", "Buffing", "Buff Tier 8 Skill", 1, 500),
        // Crafting
        B("EnableMissileCrafting", "Crafting", "Enable Missile Crafting"),
        RO("MissileCraftingState", "Crafting", "Crafting State"),
        RO("MissileCraftingActive", "Crafting", "Crafting Active"),
        RO("MissileCraftingStatus", "Crafting", "Crafting Status"),
        // Looting
        B("EnableLooting", "Looting", "Enable Looting"),
        B("BoostLootPriority", "Looting", "Boost Loot Priority"),
        B("LootOnlyRareCorpses", "Looting", "Loot Only Rare Corpses"),
        B("LootJumpEnabled", "Looting", "Jump When Looting"),
        I("LootJumpHeight", "Looting", "Jump Height", 1, 100),
        E("LootOwnership", "Looting", "Loot From", LootFrom),
        B("EnableAutostack", "Looting", "Enable Autostack"),
        B("EnableCombineSalvage", "Looting", "Combine Salvage Bags"),
        B("CombineBagsDuringSalvage", "Looting", "Combine Bags During Salvage"),
        I("LootInterItemDelayMs", "Looting", "Inter-Item Delay (ms)", 0, 5000),
        I("LootContentSettleMs", "Looting", "Content Settle (ms)", 0, 5000),
        I("LootEmptyCorpseMs", "Looting", "Empty Corpse Wait (ms)", 0, 5000),
        I("LootClosingDelayMs", "Looting", "Closing Delay (ms)", 0, 5000),
        I("LootAssessWindowMs", "Looting", "Assess Window (ms)", 0, 5000),
        I("LootRetryTimeoutMs", "Looting", "Loot Retry Timeout (ms)", 0, 10000),
        I("LootOpenRetryMs", "Looting", "Corpse Open Retry (ms)", 0, 10000),
        I("LootCorpseTimeoutMs", "Looting", "Corpse Timeout (ms)", 0, 60000),
        I("SalvageOpenDelayFirstMs", "Looting", "Salvage Open (First, ms)", 0, 5000),
        I("SalvageOpenDelayFastMs", "Looting", "Salvage Open (Fast, ms)", 0, 2000),
        I("SalvageAddDelayFirstMs", "Looting", "Salvage Add (First, ms)", 0, 5000),
        I("SalvageAddDelayFastMs", "Looting", "Salvage Add (Fast, ms)", 0, 2000),
        I("SalvageSalvageDelayMs", "Looting", "Salvage Click (ms)", 0, 2000),
        I("SalvageResultDelayFirstMs", "Looting", "Salvage Result (First, ms)", 0, 5000),
        I("SalvageResultDelayFastMs", "Looting", "Salvage Result (Fast, ms)", 0, 2000),
    };
}
