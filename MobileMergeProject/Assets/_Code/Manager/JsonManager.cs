using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace _Code.Manager
{
    public sealed class JsonManager : MonoBehaviour
    {
        [SerializeField] private string _fileName = "score-data.json";

        private const int DailyGoldRewardAmount = 1000;
        public const int CatTypeCount = 4;
        private static readonly TimeSpan KoreaDailyRewardTime = new TimeSpan(18, 0, 0);

        private ScoreSaveData _saveData = new ScoreSaveData();

        public int MaxScore => _saveData.MaxScore;
        public int Gold => _saveData.Gold;
        public string LastDailyGoldRewardDate => _saveData.LastDailyGoldRewardDate;
        public string SavePath => Path.Combine(Application.persistentDataPath, _fileName);

        private void Awake()
        {
            Load();
        }

        public void Load()
        {
            if (!File.Exists(SavePath))
            {
                _saveData = new ScoreSaveData();
                Save();
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                ScoreSaveData loadedData = JsonUtility.FromJson<ScoreSaveData>(json);
                _saveData = loadedData ?? new ScoreSaveData();
                NormalizeSaveData();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load score data. A new save file will be created. {exception.Message}");
                _saveData = new ScoreSaveData();
                Save();
            }
        }

        public bool TrySaveMaxScore(int score)
        {
            if (score <= _saveData.MaxScore)
                return false;

            _saveData.MaxScore = score;
            Save();
            return true;
        }

        public void SetMaxScore(int score)
        {
            _saveData.MaxScore = Mathf.Max(0, score);
            Save();
        }

        public void SetGold(int gold)
        {
            _saveData.Gold = Mathf.Max(0, gold);
            Save();
        }

        public void ResetSaveData()
        {
            _saveData = new ScoreSaveData();
            Save();
        }

        public void ResetCatGrowthLines()
        {
            bool changed = false;
            for (int i = 0; i < CatTypeCount; i++)
            {
                if (_saveData.CatGrowthLines[i] == 0)
                    continue;

                _saveData.CatGrowthLines[i] = 0;
                changed = true;
            }

            if (changed)
                Save();
        }

        public void ResetCatGrowthLine(int catIndex)
        {
            if (catIndex < 0 || catIndex >= CatTypeCount)
                throw new ArgumentOutOfRangeException(nameof(catIndex));

            if (_saveData.CatGrowthLines[catIndex] == 0)
                return;

            _saveData.CatGrowthLines[catIndex] = 0;
            Save();
        }

        public int GetCatGrowthLines(int catIndex)
        {
            if (catIndex < 0 || catIndex >= CatTypeCount)
                throw new ArgumentOutOfRangeException(nameof(catIndex));

            return _saveData.CatGrowthLines[catIndex];
        }

        public void AddCatGrowthLines(IReadOnlyList<int> lineCounts)
        {
            if (lineCounts == null || lineCounts.Count != CatTypeCount)
                throw new ArgumentException("Expected one line count for each of the four cats.", nameof(lineCounts));

            bool changed = false;
            for (int i = 0; i < CatTypeCount; i++)
            {
                if (lineCounts[i] <= 0)
                    continue;

                _saveData.CatGrowthLines[i] = (int)Math.Min(int.MaxValue, (long)_saveData.CatGrowthLines[i] + lineCounts[i]);
                changed = true;
            }

            if (changed)
                Save();
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
                return;

            _saveData.Gold = Mathf.Max(0, _saveData.Gold + amount);
            Save();
        }

        public bool ApplyDailyGoldRewardIfAvailable()
        {
            if (!TryGetAvailableKoreaRewardDate(out string rewardDate))
                return false;

            if (string.Equals(_saveData.LastDailyGoldRewardDate, rewardDate, StringComparison.Ordinal))
                return false;

            _saveData.Gold = Mathf.Max(0, _saveData.Gold + DailyGoldRewardAmount);
            _saveData.LastDailyGoldRewardDate = rewardDate;
            Save();
            return true;
        }

        public void MergePlayerData(int maxScore, int gold, string lastDailyGoldRewardDate)
        {
            bool changed = false;
            int safeMaxScore = Mathf.Max(0, maxScore);
            int safeGold = Mathf.Max(0, gold);

            if (safeMaxScore > _saveData.MaxScore)
            {
                _saveData.MaxScore = safeMaxScore;
                changed = true;
            }

            if (safeGold > _saveData.Gold)
            {
                _saveData.Gold = safeGold;
                changed = true;
            }

            if (IsLaterRewardDate(lastDailyGoldRewardDate, _saveData.LastDailyGoldRewardDate))
            {
                _saveData.LastDailyGoldRewardDate = lastDailyGoldRewardDate;
                changed = true;
            }

            if (changed)
                Save();
        }

        public void Save()
        {
            string directory = Path.GetDirectoryName(SavePath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(_saveData, true);
            File.WriteAllText(SavePath, json);
        }

        private void NormalizeSaveData()
        {
            _saveData.MaxScore = Mathf.Max(0, _saveData.MaxScore);
            _saveData.Gold = Mathf.Max(0, _saveData.Gold);

            // Older score files do not contain cat growth yet.
            if (_saveData.CatGrowthLines == null)
                _saveData.CatGrowthLines = new int[CatTypeCount];
            else if (_saveData.CatGrowthLines.Length != CatTypeCount)
                Array.Resize(ref _saveData.CatGrowthLines, CatTypeCount);

            for (int i = 0; i < CatTypeCount; i++)
                _saveData.CatGrowthLines[i] = Mathf.Max(0, _saveData.CatGrowthLines[i]);

            if (string.IsNullOrWhiteSpace(_saveData.LastDailyGoldRewardDate))
                _saveData.LastDailyGoldRewardDate = string.Empty;
        }

        private static bool TryGetAvailableKoreaRewardDate(out string rewardDate)
        {
            DateTime koreaNow = DateTime.UtcNow.AddHours(9);
            rewardDate = string.Empty;

            if (koreaNow.TimeOfDay < KoreaDailyRewardTime)
                return false;

            rewardDate = koreaNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        private static bool IsLaterRewardDate(string candidate, string current)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            if (string.IsNullOrWhiteSpace(current))
                return true;

            return string.CompareOrdinal(candidate, current) > 0;
        }

        [Serializable]
        private sealed class ScoreSaveData
        {
            public int MaxScore;
            public int Gold;
            public int[] CatGrowthLines = new int[CatTypeCount];
            public string LastDailyGoldRewardDate = string.Empty;
        }
    }
}
