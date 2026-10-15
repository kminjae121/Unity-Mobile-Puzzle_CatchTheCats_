using System.Collections.Generic;
using System.Threading.Tasks;
using _Code.Block;
using _Code.Field;
using _Code.Manager;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace _Code.CatHome
{
    public sealed class CatHomeGrowthController : MonoBehaviour
    {
        private const int TimedSkillTurnCount = 3;

        [SerializeField] private CatGrowthCatalog catalog;
        [SerializeField] private RectTransform residentsPrefab;
        [SerializeField] private Image background;

        private readonly int[] _earnedLines = new int[JsonManager.CatTypeCount];
        private readonly int[] _remainingSkillTurns = new int[JsonManager.CatTypeCount];
        private BlockField _field;
        private JsonManager _save;
        private RectTransform _residents;
        private CatResidentView[] _views;
        private AsyncOperationHandle<GameObject> _backGroundHandle;

        private bool _hasWhiteExtraLife;
        private bool _hasSphynxObjectSwipe;

        public int ScoreMultiplier => _remainingSkillTurns[(int)HomeCatType.Black] > 0 ? 2 : 1;
        public int CheeseMultiplier => _remainingSkillTurns[(int)HomeCatType.Orange] > 0 ? 2 : 1;
        public bool HasWhiteExtraLife => _hasWhiteExtraLife;
        public bool HasSphynxObjectSwipe => _hasSphynxObjectSwipe;

        public void Initialize(BlockField field, JsonManager save)
        {
            _ = InitializeAsync(field, save);
        }

        public async Task InitializeAsync(BlockField field, JsonManager save)
        {
            Unsubscribe();
            ResetSkillState();
            _field = field;
            _save = save;
            if (catalog == null)
                catalog = Resources.Load<CatGrowthCatalog>("BlockBlast/CatHome/CatGrowthCatalog");
            if (residentsPrefab == null)
                residentsPrefab = Resources.Load<RectTransform>("BlockBlast/CatHome/CatResidents");
            if (catalog == null || residentsPrefab == null || _save == null)
            {
                Debug.LogError("Cat home is missing its catalog, residents prefab or save manager.", this);
                return;
            }

            AddAddressable();
            
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            if (_residents != null)
                Destroy(_residents.gameObject);
            ClearAddressable();
        }

        private async void AddAddressable()
        {
            _backGroundHandle = Addressables.InstantiateAsync("BackGround/BasicBackGround");
            
            await _backGroundHandle.Task;

            background = _backGroundHandle.Result.GetComponentInChildren<Image>();
            
            if (background == null)
            {
                Debug.LogError("Cat home could not find the active room background.", this);
                return;
            }

            if (_residents == null)
            {
                _residents = Instantiate(residentsPrefab, background.rectTransform, false);
            }
            _views = _residents.GetComponentsInChildren<CatResidentView>(true);
            foreach (CatGrowthCatalog.CatDefinition cat in catalog.cats)
            {
                CatResidentView view = FindView(cat.type);
                if (view == null)
                    continue;
                RectTransform home = (RectTransform)view.transform;
                home.anchorMin = cat.homePosition - new Vector2(cat.adultSize.x * 0.5f, 0f);
                home.anchorMax = cat.homePosition + new Vector2(cat.adultSize.x * 0.5f, cat.adultSize.y);
                home.offsetMin = home.offsetMax = Vector2.zero;
                view.SetProgress(catalog, cat, _save.GetCatGrowthLines((int)cat.type), false);
            }
            Subscribe();
        }

        private void ClearAddressable()
        {
            if (_backGroundHandle.IsValid())
                Addressables.ReleaseInstance(_backGroundHandle);

            _backGroundHandle = default;
            background = null;
        }

      


        private void Subscribe()
        {
            if (_field == null || _views == null || !isActiveAndEnabled)
                return;
            _field.MatchingCatLinesCleared -= HandleMatchingLines;
            _field.MatchingCatLinesCleared += HandleMatchingLines;
        }

        private void Unsubscribe()
        {
            if (_field != null)
                _field.MatchingCatLinesCleared -= HandleMatchingLines;
        }

        private void HandleMatchingLines(IReadOnlyList<Sprite> lineSprites)
        {
            if (!isActiveAndEnabled || _save == null || catalog == null)
                return;
            System.Array.Clear(_earnedLines, 0, _earnedLines.Length);
            foreach (Sprite sprite in lineSprites)
            {
                CatGrowthCatalog.CatDefinition cat = catalog.Find(sprite);
                if (cat != null)
                    _earnedLines[(int)cat.type]++;
            }
            _save.AddCatGrowthLines(_earnedLines);
            foreach (CatGrowthCatalog.CatDefinition cat in catalog.cats)
            {
                if (cat == null || _earnedLines[(int)cat.type] <= 0)
                    continue;

                int lineCount = _save.GetCatGrowthLines((int)cat.type);
                CatResidentView view = FindView(cat.type);
                view?.SetProgress(catalog, cat, lineCount, true);
                if (!IsSkillActive(cat.type) && catalog.GetStage(lineCount) >= 2)
                    ActivateSkill(cat.type);
            }
        }

        public void CompleteTurn()
        {
            CompleteTimedSkill(HomeCatType.Black);
            CompleteTimedSkill(HomeCatType.Orange);
        }

        public bool ConsumeSphynxObjectSwipe()
        {
            if (!_hasSphynxObjectSwipe)
                return false;

            _hasSphynxObjectSwipe = false;
            ReturnToKitten(HomeCatType.Sphynx);
            return true;
        }

        public bool TryUseWhiteExtraLife()
        {
            if (!_hasWhiteExtraLife)
                return false;

            _hasWhiteExtraLife = false;
            ReturnToKitten(HomeCatType.White);
            return true;
        }

        public void ResetGrowthForTutorial()
        {
            ResetSkillState();
            _save?.ResetCatGrowthLines();

            if (catalog == null || _views == null)
                return;

            foreach (CatGrowthCatalog.CatDefinition cat in catalog.cats)
            {
                if (cat == null)
                    continue;

                CatResidentView view = FindView(cat.type);
                view?.SetProgress(catalog, cat, 0, false);
                view?.SetSkillActive(false);
            }
        }

        private void ActivateSkill(HomeCatType type)
        {
            FindView(type)?.SetSkillActive(true);

            switch (type)
            {
                case HomeCatType.Black:
                case HomeCatType.Orange:
                    _remainingSkillTurns[(int)type] = TimedSkillTurnCount;
                    break;
                case HomeCatType.White:
                    _hasWhiteExtraLife = true;
                    break;
                case HomeCatType.Sphynx:
                    _hasSphynxObjectSwipe = true;
                    break;
            }
        }

        private bool IsSkillActive(HomeCatType type)
        {
            switch (type)
            {
                case HomeCatType.Black:
                case HomeCatType.Orange:
                    return _remainingSkillTurns[(int)type] > 0;
                case HomeCatType.White:
                    return _hasWhiteExtraLife;
                case HomeCatType.Sphynx:
                    return _hasSphynxObjectSwipe;
                default:
                    return false;
            }
        }

        private void CompleteTimedSkill(HomeCatType type)
        {
            int typeIndex = (int)type;
            if (_remainingSkillTurns[typeIndex] <= 0)
                return;

            _remainingSkillTurns[typeIndex]--;
            if (_remainingSkillTurns[typeIndex] == 0)
                ReturnToKitten(type);
        }

        private void ReturnToKitten(HomeCatType type)
        {
            if (_save == null || catalog == null)
                return;

            _save.ResetCatGrowthLine((int)type);
            foreach (CatGrowthCatalog.CatDefinition cat in catalog.cats)
            {
                if (cat != null && cat.type == type)
                {
                    CatResidentView view = FindView(type);
                    view?.SetProgress(catalog, cat, 0, true);
                    view?.SetSkillActive(false);
                    return;
                }
            }
        }

        private void ResetSkillState()
        {
            System.Array.Clear(_remainingSkillTurns, 0, _remainingSkillTurns.Length);
            _hasWhiteExtraLife = false;
            _hasSphynxObjectSwipe = false;
        }

        private CatResidentView FindView(HomeCatType type)
        {
            foreach (CatResidentView view in _views)
                if (view != null && view.CatType == type)
                    return view;
            return null;
        }
    }
}
