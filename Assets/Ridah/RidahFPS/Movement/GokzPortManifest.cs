using System;
using System.Collections.Generic;

namespace RidahFPS.Controllers
{
    public enum GokzPortStatus
    {
        UnityEquivalent,
        UnityAdapter,
        SourceModOnly
    }

    [Serializable]
    public struct GokzPortModule
    {
        public string sourceModule;
        public string unityModule;
        public GokzPortStatus status;
        public string note;
    }

    /// <summary>
    /// Audit list for the complete GOKZ top-level module surface. It is kept
    /// in the project so a future feature cannot be mistaken for silently
    /// migrated SourceMod code.
    /// </summary>
    public static class GokzPortManifest
    {
        private static readonly IReadOnlyList<GokzPortModule> Modules =
            new List<GokzPortModule>
            {
                Unity("gokz-core", "GokzRuntime/GokzZone/GokzCommandRouter", "timer, courses, checkpoints, teleports, mode and zone state"),
                Adapter("gokz-mode-kztimer", "SourceMovementCore", "KZTimer constants, prestrafe, AirAccelerate hook, perf cap, crouch-jump bind, duck slowdown, water boost and displacement-stuck handling"),
                Adapter("gokz-mode-simplekz", "SourceMovementCore + GokzModeSettings", "SimpleKZ mode-specific prestrafe and jump behavior share the Source call path"),
                Adapter("gokz-mode-vanilla", "SourceMovementCore + GokzModeSettings", "Vanilla mode-specific convars share the Source call path"),
                Adapter("MovementAPI::PlayerMove/FullWalkMove", "SourceMovementCore.Process", "fixed 128 Hz call boundary"),
                Adapter("CCSGameMovement::ProcessMovement/PlayerMove/CheckParameters", "SourceMovementCore.Process", "CS:GO override order, raw/effective duck buttons, command speed crop and old ground state"),
                Adapter("CGameMovement::StartGravity/FinishGravity", "SourceMovementCore", "half-step gravity around movement"),
                Adapter("CGameMovement::CheckJumpButton/OnJump", "SourceMovementCore", "Source old-button guard and jump impulse; KZTimer HitPerf still depends on unavailable MovementAPI runtime state"),
                Adapter("CGameMovement::Friction/WalkMove/AirMove/AirAccelerate", "SourceMovementCore + GokzMovementRules", "Source formulas in Hammer units, converted only at the Unity boundary"),
                Adapter("CGameMovement::TryPlayerMove/StepMove/StayOnGround + gokz-momsurffix", "SourceMovementCore", "axis-aligned box sweep, five-plane clip solver, Source sv_bounce overbounce and step solver"),
                Adapter("CGameMovement::CategorizePosition/CheckVelocity", "SourceMovementCore", "walkable-normal ground trace and component velocity bounds"),
                Adapter("CGameMovement::CheckStuck + GetCheckInterval", "SourceMovementCore", "Source stuck offset sequence and retry cadence; Unity phase cannot include Source entity index"),
                Adapter("CGameMovement::Duck/CanUnduck", "SourceMovementCore + SurfCharacter", "54/72 HU hulls, Source eye offsets and landing-tick unduck rule"),
                Adapter("CGameMovement::LadderMove/FullLadderMove", "SourceMovementCore", "ladder-normal projection, 200 HU climb and 270 HU jump-away"),
                Adapter("CGameMovement::WaterMove/CheckWater", "SourceMovementCore + MoveData.underwater", "Source water friction, 0.8 wish-speed and swim branch; Unity water volumes supply the state"),
                Adapter("trigger_gravity + trigger_push", "GokzMovementVolume + SourceMovementCore", "One Unity authoring component; Source gravity scale, pushdir/pushspeed and SF_TRIG_PUSH_ONCE are selected by effects"),
                Unity("gokz-jumpstats", "GokzJumpStats", "jump tracking, types, tiers and events"),
                Unity("gokz-jumpbeam", "GokzJumpBeam", "jump event beam"),
                Unity("gokz-hud", "GokzHud", "timer, speed, keys and perf/deadstrafe display"),
                Unity("gokz-saveloc", "GokzRuntime + GokzCommandRouter", "position, angles, velocity and checkpoint restore"),
                Unity("gokz-measure", "GokzMeasureTool", "Hammer-unit distance measurement"),
                Unity("gokz-localdb/localranks", "GokzLocalRecords", "local PB/top records in PlayerPrefs JSON"),
                Unity("gokz-replays", "GokzReplayRecorder", "tick clip recording, playback and persistent JSON"),
                Unity("gokz-racing", "GokzRaceSession", "local race session/events"),
                Unity("gokz-goto", "GokzGoto", "configured local transform targets"),
                Unity("gokz-chat", "GokzChat", "local message/event bus"),
                Unity("gokz-profile", "GokzProfile", "persistent options/profile"),
                Unity("gokz-quiet", "GokzQuiet", "player/audio visibility controls"),
                Unity("gokz-tips", "GokzTips", "periodic local tips"),
                Unity("gokz-paint", "GokzPaint", "world paint markers"),
                Unity("gokz-pistol", "GokzPistol", "optional pistol prefab selection"),
                Unity("gokz-playermodels", "GokzPlayerModels", "optional model set selection"),
                Unity("gokz-tpanglefix", "GokzTpAngleFix", "explicit view synchronization after teleport"),
                Unity("gokz-slayonend", "GokzSlayOnEnd", "optional end-of-run freeze"),
                Unity("gokz-anticheat", "GokzMovementDiagnostics", "local diagnostic adapter; no server trust claim"),
                Adapter("gokz-global", "none", "Steam/web API, global ranks and server ban endpoints require an external service"),
                Adapter("gokz-localdb SQL", "GokzLocalRecords", "MySQL/SourceMod async SQL replaced by local JSON; network DB needs a Unity backend"),
                Adapter("gokz-errorboxfixer", "none", "Source engine memory/box patch has no Unity equivalent"),
                Adapter("gokz-momsurffix", "SourceMovementCore", "Source detour is replaced by the Unity box sweep/multi-plane solver"),
                Adapter("gokz-spec", "none", "Source spectator/team API is not present in this single-player project"),
                Adapter("gokz-quiet sound hooks", "GokzQuiet", "Source sound-hook granularity depends on Unity AudioMixer routing"),
            };

        public static IReadOnlyList<GokzPortModule> All => Modules;

        private static GokzPortModule Unity(string source, string unity, string note)
        {
            return new GokzPortModule
            {
                sourceModule = source,
                unityModule = unity,
                status = GokzPortStatus.UnityAdapter,
                note = note
            };
        }

        private static GokzPortModule Adapter(string source, string unity, string note)
        {
            return new GokzPortModule
            {
                sourceModule = source,
                unityModule = unity,
                status = unity == "none" ? GokzPortStatus.SourceModOnly : GokzPortStatus.UnityAdapter,
                note = note
            };
        }
    }
}
