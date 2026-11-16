using UnityEngine;

namespace RidahFPS.Controllers
{
    public interface ISurfControllable
    {
        MoveType MoveType { get; }
        MoveData MoveData { get; }
        CharacterController CC { get; }
        CapsuleCollider Collider { get; }
        Vector3 Forward { get; }
        Vector3 Right { get; }
        Vector3 Up { get; }
        Vector3 BaseVelocity { get; }
    }
}
