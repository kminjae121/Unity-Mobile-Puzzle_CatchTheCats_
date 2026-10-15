using _Code.Server;
using TMPro;
using UnityEngine;

namespace _Code.Manager
{
    public sealed class PlayerProgressController : MonoBehaviour
    {
        [SerializeField] private JsonManager jsonManager;
        [SerializeField] private ServerScoreClient serverScoreClient;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI bestScoreText;

        private const string ScoreSuffix = "\uC810";
        private const string BestScoreLabel = "\uCD5C\uACE0\uC810\uC218 : ";
        private const int GoldScoreInterval = 100;

        private int _score;
        private int _maxScore;
        private int _gold;
        private bool _isStageMode;
        private int _stageTargetScore;

        public int Score => _score;
        public int MaxScore => _maxScore;
        public int Gold => _gold;

        public void Configure(
            JsonManager jsonManager,
            ServerScoreClient serverScoreClient,
            TextMeshProUGUI scoreText,
            TextMeshProUGUI bestScoreText)
        {
            if (this.jsonManager == null)
                this.jsonManager = jsonManager;

            if (this.serverScoreClient == null)
                this.serverScoreClient = serverScoreClient;

            if (this.scoreText == null)
                this.scoreText = scoreText;

            if (this.bestScoreText == null)
                this.bestScoreText = bestScoreText;
        }

        public void Initialize(bool isStageMode, int stageTargetScore)
        {
            _score = 0;
            _stageTargetScore = Mathf.Max(0, stageTargetScore);
            _isStageMode = isStageMode && _stageTargetScore > 0;

            LoadPlayerData();
            UpdateScoreText();
            UpdateBestScoreText();
        }

        public void AddScore(int value, bool syncToServer)
        {
            if (value <= 0)
                return;

            int previousScore = _score;
            _score += value;
            int earnedGold = Mathf.Max(0, _score / GoldScoreInterval - previousScore / GoldScoreInterval);

            if (earnedGold > 0)
                AddGold(earnedGold);

            UpdateScoreText();
            bool updatedMaxScore = TryUpdateMaxScore(syncToServer);

            if (earnedGold > 0 && !updatedMaxScore && syncToServer)
                SyncToServer();
        }

        public void ResetCurrentScore()
        {
            _score = 0;
            UpdateScoreText();
        }

        public bool TryUpdateMaxScore(bool syncToServer)
        {
            if (_score <= _maxScore)
                return false;

            _maxScore = _score;

            if (jsonManager != null)
                jsonManager.SetMaxScore(_maxScore);

            UpdateBestScoreText();

            if (syncToServer)
                SyncToServer();

            return true;
        }

        public void ApplyServerData(ServerScoreClient.PlayerData serverData)
        {
            int localScore = _maxScore;
            int localGold = _gold;

            if (jsonManager != null)
            {
                jsonManager.MergePlayerData(serverData.MaxScore, serverData.Gold, serverData.LastDailyGoldRewardDate);
                _maxScore = jsonManager.MaxScore;
                _gold = jsonManager.Gold;
            }
            else
            {
                _maxScore = Mathf.Max(_maxScore, serverData.MaxScore);
                _gold = Mathf.Max(_gold, serverData.Gold);
            }

            UpdateBestScoreText();

            if (localScore > serverData.MaxScore || localGold > serverData.Gold)
                SyncToServer();
        }

        public void SyncToServer()
        {
            if (serverScoreClient != null)
                serverScoreClient.SubmitPlayerData(_maxScore, _gold);
        }

        public void ResetLocalData()
        {
            _score = 0;
            _maxScore = 0;
            _gold = 0;

            if (jsonManager != null)
                jsonManager.ResetSaveData();

            UpdateScoreText();
            UpdateBestScoreText();
        }

        private void LoadPlayerData()
        {
            if (jsonManager != null)
            {
                jsonManager.Load();
                jsonManager.ApplyDailyGoldRewardIfAvailable();
                _maxScore = jsonManager.MaxScore;
                _gold = jsonManager.Gold;
            }

            if (serverScoreClient != null)
                serverScoreClient.FetchPlayerData(ApplyServerData, UpdateBestScoreText);
        }

        private void AddGold(int amount)
        {
            if (amount <= 0)
                return;

            _gold = Mathf.Max(0, _gold + amount);

            if (jsonManager != null)
                jsonManager.SetGold(_gold);
        }

        private void UpdateScoreText()
        {
            if (scoreText == null)
                return;

            scoreText.text = _isStageMode
                ? $"{_score}/{_stageTargetScore}{ScoreSuffix}"
                : $"{_score}{ScoreSuffix}";
        }

        private void UpdateBestScoreText()
        {
            if (bestScoreText != null)
                bestScoreText.text = $"{BestScoreLabel}{_maxScore}";
        }
    }
}
