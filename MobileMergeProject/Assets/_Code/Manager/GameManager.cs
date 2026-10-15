using System.Threading.Tasks;
using _Code.Block;
using _Code.CatHome;
using _Code.Effects;
using _Code.Field;
using _Code.Server;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using MouseView = _Code.Mouse.Mouse;

namespace _Code.Manager
{
    public class GameManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private BlockField blockField;
        [SerializeField] private RandomBlockManager randomBlockManager;
        [SerializeField] private BlockPiece[] pieces;
        [SerializeField] private MouseView mouse;
        [SerializeField] private JsonManager jsonManager;
        [SerializeField] private ServerScoreClient serverScoreClient;
        [SerializeField] private HapticFeedback hapticFeedback;
        [SerializeField] private BlockPlacementPreview placementPreview;
        [SerializeField] private GameOverView gameOverView;
        [SerializeField] private LineClearPawParticleEffect lineClearParticleEffect;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI bestScoreText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private string resetSceneName = "MainScene";
        [SerializeField, Min(20f)] private float swipeMinDistance = 75f;
        [SerializeField] private bool enableKeyboardInput = true;

        [Header("Responsibility Controllers")]
        [SerializeField] private PlayerProgressController playerProgressController;
        [SerializeField] private StageModeController stageModeController;
        [SerializeField] private BoardShiftController boardShiftController;
        [SerializeField] private LineClearEffectPlayer lineClearEffectPlayer;
        [SerializeField] private PlacementScoreGuard placementScoreGuard;
        [SerializeField] private TutorialController tutorialController;
        [SerializeField] private CatHomeGrowthController catHomeGrowthController;
        [SerializeField] private LineClearFeedbackController lineClearFeedbackController;
        [SerializeField] private GameSessionController gameSessionController;
        [SerializeField] private GameplayTurnController gameplayTurnController;

        private void Awake()
        {
            if (blockField == null)
            {
                enabled = false;
                return;
            }

            ResolveSceneReferences();
            PrepareLoadingScene();
            ResolveControllers();
            ConfigureControllers();
        }

        private async void Start()
        {

            await BlockBlastSpriteLibrary.InitializeAsync();
            if (!this)
                return;

            blockField.Rebuild();
            blockField.ClearAll();
            jsonManager?.ResetCatGrowthLines();
            await catHomeGrowthController.InitializeAsync(blockField, jsonManager);
            if (!this)
                return;

            stageModeController.Initialize(blockField, gameObject);

            randomBlockManager.Initialize(pieces);
            if (mouse != null)
                mouse.Initialize(blockField);

            playerProgressController.Initialize(stageModeController.HasScoreGoal, stageModeController.TargetScore);
            gameSessionController.BeginRun();
            gameplayTurnController.InitializePieces();

            bool tutorialStarted = tutorialController != null &&
                                   tutorialController.ShouldStartAutomatically &&
                                   tutorialController.TryBegin(stageModeController.IsStageMode);
            randomBlockManager.SetTutorialMode(tutorialStarted);

            if (!randomBlockManager.GiveNewSet(blockField))
            {
                gameSessionController.EndGame(GameOverReason.NoAvailablePlacement);
                return;
            }

            if (!tutorialStarted)
                gameSessionController.SetMessage(stageModeController.GetStartMessage());

        }
        
        private void OnDestroy()
        {
            gameplayTurnController?.UnsubscribePieces();
        }
        
     
        private void Update()
        {
            gameplayTurnController?.ProcessInput();
        }

        private void LateUpdate()
        {
            gameplayTurnController?.UpdatePlacementPreview();
        }


        public void StartTutorial()
        {
            if (gameSessionController == null || !gameSessionController.IsGameplayActive || tutorialController == null)
                return;

            randomBlockManager?.SetTutorialMode(true);
            randomBlockManager?.GiveNewSet(blockField);
            tutorialController.BeginManually();
        }

        public void ReturnToMainScene()
        {
            gameSessionController?.ReturnToMainScene();
        }

        public void ReturnToStageSelect()
        {
            gameSessionController?.ReturnToStageSelect();
        }

        private void ResolveSceneReferences()
        {
            if (randomBlockManager == null)
                randomBlockManager = GetComponent<RandomBlockManager>();

            if (jsonManager == null)
                jsonManager = GetComponent<JsonManager>();

            if (serverScoreClient == null)
                serverScoreClient = GetComponent<ServerScoreClient>();

            if (hapticFeedback == null)
                hapticFeedback = GetComponent<HapticFeedback>();

            if (gameOverView == null)
                gameOverView = GetComponentInChildren<GameOverView>(true);

            if (placementPreview == null)
                placementPreview = GetComponent<BlockPlacementPreview>();

            if (placementPreview == null)
                placementPreview = gameObject.AddComponent<BlockPlacementPreview>();
        }

        private void PrepareLoadingScene()
        {
           
        }

        private void ResolveControllers()
        {
            if (playerProgressController == null)
                playerProgressController = GetOrAdd<PlayerProgressController>();

            if (stageModeController == null)
                stageModeController = GetOrAdd<StageModeController>();

            if (boardShiftController == null)
                boardShiftController = GetOrAdd<BoardShiftController>();

            if (lineClearEffectPlayer == null)
                lineClearEffectPlayer = GetOrAdd<LineClearEffectPlayer>();

            if (placementScoreGuard == null)
                placementScoreGuard = GetOrAdd<PlacementScoreGuard>();

            if (tutorialController == null)
                tutorialController = GetOrAdd<TutorialController>();

            if (catHomeGrowthController == null)
                catHomeGrowthController = GetOrAdd<CatHomeGrowthController>();

            if (lineClearFeedbackController == null)
                lineClearFeedbackController = GetOrAdd<LineClearFeedbackController>();

            if (gameSessionController == null)
                gameSessionController = GetOrAdd<GameSessionController>();

            if (gameplayTurnController == null)
                gameplayTurnController = GetOrAdd<GameplayTurnController>();
        }

        private void ConfigureControllers()
        {
            playerProgressController.Configure(jsonManager, serverScoreClient, scoreText, bestScoreText);
            boardShiftController.Configure(blockField, randomBlockManager, mouse, swipeMinDistance, enableKeyboardInput);
            lineClearEffectPlayer.Configure(hapticFeedback, lineClearParticleEffect);
            placementScoreGuard.Configure(jsonManager, serverScoreClient, resetSceneName);
            tutorialController.Configure(
                messageText,
                blockField,
                mouse,
                pieces,
                playerProgressController,
                catHomeGrowthController);
            lineClearFeedbackController.Configure(lineClearEffectPlayer);
            gameSessionController.Configure(
                blockField,
                randomBlockManager,
                playerProgressController,
                stageModeController,
                catHomeGrowthController,
                placementPreview,
                gameOverView,
                messageText,
                resetSceneName);
            gameplayTurnController.Configure(
                blockField,
                randomBlockManager,
                pieces,
                placementPreview,
                playerProgressController,
                stageModeController,
                boardShiftController,
                placementScoreGuard,
                tutorialController,
                catHomeGrowthController,
                lineClearFeedbackController,
                gameSessionController);
        }

        private T GetOrAdd<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }
    }
}
