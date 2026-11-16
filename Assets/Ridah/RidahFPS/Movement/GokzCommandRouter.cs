using System;
using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Local command surface corresponding to the user-facing gokz-core,
    /// saveloc, measure and replay commands. A UI, console bridge or input
    /// layer can call Execute without coupling those systems to the timer.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Command Router")]
    public sealed class GokzCommandRouter : MonoBehaviour
    {
        [SerializeField] private GokzRuntime runtime;
        [SerializeField] private GokzMeasureTool measure;
        [SerializeField] private GokzReplayRecorder replay;

        public string LastResult { get; private set; }
        public event Action<string> CommandCompleted;

        private void Awake()
        {
            ResolveDependencies();
        }

        public bool Execute(string commandLine)
        {
            ResolveDependencies();
            if (string.IsNullOrWhiteSpace(commandLine))
                return Fail("empty command");

            string[] args = commandLine.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string command = args[0].ToLowerInvariant().TrimStart('/');
            switch (command)
            {
                case "start": runtime.StartRun(); return Succeed("timer started");
                case "stop": runtime.StopRun(); return Succeed("timer stopped");
                case "pause": runtime.PauseRun(); return Succeed("timer paused");
                case "resume": runtime.ResumeRun(); return Succeed("timer resumed");
                case "cp":
                case "checkpoint": return runtime.MakeCheckpoint()
                    ? Succeed("checkpoint saved") : Fail("checkpoint unavailable");
                case "tp":
                case "teleport": return runtime.TeleportToCheckpoint()
                    ? Succeed("teleported to checkpoint") : Fail("checkpoint unavailable");
                case "prev": return runtime.PreviousCheckpoint()
                    ? Succeed("teleported to previous checkpoint") : Fail("no previous checkpoint");
                case "next": return runtime.NextCheckpoint()
                    ? Succeed("teleported to next checkpoint") : Fail("no next checkpoint");
                case "undo": return runtime.UndoTeleport()
                    ? Succeed("teleport undone") : Fail("nothing to undo");
                case "saveloc": runtime.SaveCurrentLocation(); return Succeed("location saved");
                case "loadloc": return runtime.TeleportToSavedLocation()
                    ? Succeed("location loaded") : Fail("no saved location");
                case "measure1": measure.SetFirstPointFromPlayer(); return Succeed("measure point 1 set");
                case "measure2": measure.SetSecondPointFromPlayer(); return Succeed("measure point 2 set");
                case "measure": return Succeed(measure.DistanceHu.ToString("0.00") + " HU");
                case "mode": return SetMode(args.Length > 1 ? args[1] : string.Empty);
                case "replay": return ExecuteReplay(args);
                default: return Fail("unknown command: " + command);
            }
        }

        private bool SetMode(string modeName)
        {
            if (string.Equals(modeName, "kztimer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(modeName, "kzt", StringComparison.OrdinalIgnoreCase))
                runtime.SetMode(GokzMode.KzTimer);
            else if (string.Equals(modeName, "simplekz", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(modeName, "skz", StringComparison.OrdinalIgnoreCase))
                runtime.SetMode(GokzMode.SimpleKz);
            else if (string.Equals(modeName, "vanilla", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(modeName, "vnl", StringComparison.OrdinalIgnoreCase))
                runtime.SetMode(GokzMode.Vanilla);
            else
                return Fail("mode must be vanilla, simplekz or kztimer");
            return Succeed("mode changed to " + runtime.Mode);
        }

        private bool ExecuteReplay(string[] args)
        {
            if (replay == null || args.Length < 2)
                return Fail("replay start|stop|save");
            switch (args[1].ToLowerInvariant())
            {
                case "start": replay.StartRecording(); return Succeed("replay recording started");
                case "stop": replay.StopRecording(); return Succeed("replay recording stopped");
                case "save": return replay.SaveCurrentClip()
                    ? Succeed("replay saved") : Fail("no replay to save");
                default: return Fail("replay start|stop|save");
            }
        }

        private bool Succeed(string result)
        {
            LastResult = result;
            CommandCompleted?.Invoke(result);
            return true;
        }

        private bool Fail(string result)
        {
            LastResult = result;
            CommandCompleted?.Invoke(result);
            return false;
        }

        private void ResolveDependencies()
        {
            if (runtime == null) runtime = GokzRuntime.Instance;
            if (measure == null) measure = FindFirstObjectByType<GokzMeasureTool>();
            if (replay == null) replay = FindFirstObjectByType<GokzReplayRecorder>();
        }
    }
}
