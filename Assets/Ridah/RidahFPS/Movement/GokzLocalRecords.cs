using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RidahFPS.Controllers
{
    [Serializable]
    public struct GokzRecord
    {
        public string map;
        public int course;
        public float time;
        public int teleportsUsed;
        public bool valid;
        public GokzMode mode;
        public string timestamp;
    }

    [Serializable]
    internal sealed class GokzRecordList
    {
        public List<GokzRecord> records = new List<GokzRecord>();
    }

    /// <summary>
    /// Local Unity equivalent of gokz-localdb and gokz-localranks. It keeps
    /// the same useful local PB/top-record behaviour without pretending that
    /// Steam, MySQL or the GOKZ web API exist inside a standalone Unity build.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Local Records")]
    public sealed class GokzLocalRecords : MonoBehaviour
    {
        [SerializeField] private int maximumRecords = 100;
        [SerializeField] private List<GokzRecord> records = new List<GokzRecord>();

        public IReadOnlyList<GokzRecord> Records => records;
        public event Action<GokzRecord> RecordAdded;

        private GokzRuntime _runtime;

        private string StorageKey => "GOKZ.records." + SceneManager.GetActiveScene().path;

        private void Awake()
        {
            Load();
            Subscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            _runtime = GokzRuntime.Instance;
            if (_runtime != null)
                _runtime.RunFinished += OnRunFinished;
        }

        private void Unsubscribe()
        {
            if (_runtime != null)
                _runtime.RunFinished -= OnRunFinished;
        }

        private void Update()
        {
            if (_runtime == null && GokzRuntime.Instance != null)
                Subscribe();
        }

        public bool TryGetPersonalBest(int course, out GokzRecord personalBest)
        {
            return TryGetPersonalBest(course, false, out personalBest);
        }

        public bool TryGetPersonalBest(int course, bool validOnly, out GokzRecord personalBest)
        {
            personalBest = default;
            bool found = false;
            for (int i = 0; i < records.Count; i++)
            {
                GokzRecord record = records[i];
                if (record.course != course || validOnly && !record.valid ||
                    (found && record.time >= personalBest.time))
                    continue;
                personalBest = record;
                found = true;
            }
            return found;
        }

        public int GetRank(GokzRecord record)
        {
            int rank = 1;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].course == record.course && records[i].time < record.time)
                    rank++;
            }
            return rank;
        }

        public void ClearRecords()
        {
            records.Clear();
            PlayerPrefs.DeleteKey(StorageKey);
            PlayerPrefs.Save();
        }

        private void OnRunFinished(GokzRunResult result)
        {
            GokzRecord record = new GokzRecord
            {
                map = SceneManager.GetActiveScene().name,
                course = result.course,
                time = result.time,
                teleportsUsed = result.teleportsUsed,
                valid = result.valid,
                mode = result.mode,
                timestamp = DateTime.UtcNow.ToString("O")
            };
            records.Add(record);
            records.Sort((a, b) => a.time.CompareTo(b.time));
            if (records.Count > Mathf.Max(1, maximumRecords))
                records.RemoveRange(Mathf.Max(1, maximumRecords), records.Count - Mathf.Max(1, maximumRecords));
            Save();
            RecordAdded?.Invoke(record);
        }

        private void Load()
        {
            string json = PlayerPrefs.GetString(StorageKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;

            GokzRecordList loaded = JsonUtility.FromJson<GokzRecordList>(json);
            if (loaded != null && loaded.records != null)
                records = loaded.records;
        }

        private void Save()
        {
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(new GokzRecordList { records = records }));
            PlayerPrefs.Save();
        }
    }
}
