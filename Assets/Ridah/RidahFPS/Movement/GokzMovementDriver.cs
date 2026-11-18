using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Runtime driver for the Source movement core. It exposes Source's tick
    /// stages and contains no movement rules of its own.
    /// </summary>
    internal sealed class GokzMovementDriver
    {
        public const int TickRate = SourceMovementCore.TickRate;
        public const float TickInterval = SourceMovementCore.TickInterval;
        public const float HammerUnitsPerUnityUnit = SourceMovementCore.HammerUnitsPerUnityUnit;

        private readonly SourceMovementCore _core = new SourceMovementCore();

        public bool Grounded => _core.Grounded;
        public bool Crouched => _core.Crouched;
        public float DuckAmount => _core.DuckAmount;
        public float EyeDuckAmount => _core.EyeDuckAmount;
        public bool LastJumpWasPerfect => _core.LastJumpWasPerfect;
        public bool LastJumpWasDuck => _core.LastJumpWasDuck;
        public int JumpCount => _core.JumpCount;
        public float LastTakeoffSpeed => _core.LastTakeoffSpeed;
        public float Speed => _core.Speed;
        public Vector3 MovementVelocity => _core.MovementVelocity;
        public GokzMode Mode => _core.Mode;

        public void SetMode(GokzMode mode) => _core.SetMode(mode);
        public void EnableSourceCollision(IGokzMovementPlayer player) => _core.EnableSourceCollision(player);
        public void DisableSourceCollision(IGokzMovementPlayer player) => _core.DisableSourceCollision(player);
        public void Move(IGokzMovementPlayer player) => _core.Move(player);
        public void Process(IGokzMovementPlayer player) => _core.Process(player);
        public void Finish(IGokzMovementPlayer player) => _core.Finish(player);
        public void ResetState() => _core.ResetState();
    }
}