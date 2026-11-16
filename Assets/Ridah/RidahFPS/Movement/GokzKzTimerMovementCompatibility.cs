using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Compatibility facade for existing HUD, replay and diagnostics code.
    /// The actual movement implementation is SourceMovementCore; this type
    /// intentionally contains no movement rules of its own.
    /// </summary>
    internal sealed class GokzKzTimerMovement
    {
        public const int TickRate = SourceMovementCore.TickRate;
        public const float TickInterval = SourceMovementCore.TickInterval;
        public const float HammerUnitsPerUnityUnit =
            SourceMovementCore.HammerUnitsPerUnityUnit;

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
        public void EnableSourceCollision(ISurfControllable surfer) =>
            _core.EnableSourceCollision(surfer);
        public void DisableSourceCollision(ISurfControllable surfer) =>
            _core.DisableSourceCollision(surfer);
        public void Move(ISurfControllable surfer) => _core.Move(surfer);
        public void Process(ISurfControllable surfer) => _core.Process(surfer);
        public void Finish(ISurfControllable surfer) => _core.Finish(surfer);
        public void ResetState() => _core.ResetState();
    }
}
