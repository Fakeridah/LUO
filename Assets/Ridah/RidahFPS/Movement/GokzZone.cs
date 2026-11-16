using UnityEngine;

namespace RidahFPS.Controllers
{
    public enum GokzZoneType
    {
        Start,
        End,
        Stop,
        Checkpoint,
        Reset,
        StartButton,
        EndButton,
        Teleport,
        AntiBhop,
        AntiCheckpoint,
        AntiPause,
        AntiJumpstats,
        BhopReset
    }

    public enum GokzTeleportType
    {
        Normal,
        MultiBhop,
        SingleBhop,
        SequentialBhop
    }

    /// <summary>
    /// Unity trigger equivalent of the map start/end/checkpoint events used by
    /// gokz-core. The trigger itself contains no timer logic; it forwards the
    /// event to the active GokzRuntime so map objects stay reusable.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Zone")]
    [RequireComponent(typeof(Collider))]
    public sealed class GokzZone : MonoBehaviour
    {
        [SerializeField] private GokzZoneType zoneType = GokzZoneType.Start;
        [SerializeField] private int course = 0;
        [SerializeField] private int checkpoint = 0;
        [Header("Teleport trigger")]
        [SerializeField] private Transform teleportDestination;
        [SerializeField] private GokzTeleportType teleportType = GokzTeleportType.Normal;
        [SerializeField] private float teleportDelay;
        [SerializeField] private bool useDestinationAngles = true;
        [SerializeField] private bool resetVelocity = true;
        [SerializeField] private bool relativeDestination;
        [SerializeField] private bool reorientPlayer;

        public GokzZoneType ZoneType => zoneType;
        public int Course => course;
        public int Checkpoint => checkpoint;
        public Transform TeleportDestination => teleportDestination;
        public GokzTeleportType TeleportType => teleportType;
        public float TeleportDelay => teleportDelay;
        public bool UseDestinationAngles => useDestinationAngles;
        public bool ResetVelocity => resetVelocity;
        public bool RelativeDestination => relativeDestination;
        public bool ReorientPlayer => reorientPlayer;

        private void Awake()
        {
            Collider zoneCollider = GetComponent<Collider>();
            zoneCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            LZM player = other.GetComponentInParent<LZM>();
            if (player == null || GokzRuntime.Instance == null)
                return;

            GokzRuntime.Instance.HandleZone(this, player);
        }

        private void OnTriggerExit(Collider other)
        {
            LZM player = other.GetComponentInParent<LZM>();
            if (player != null && GokzRuntime.Instance != null)
                GokzRuntime.Instance.HandleZoneExit(this, player);
        }
    }
}
