using UnityEngine;

namespace RidahFPS.Controllers
{
    public interface IGokzMovementPlayer
    {
        MoveData MoveData { get; }
        CharacterController CC { get; }
        Vector3 Forward { get; }
        Vector3 Right { get; }
        Vector3 BaseVelocity { get; }
    }
}