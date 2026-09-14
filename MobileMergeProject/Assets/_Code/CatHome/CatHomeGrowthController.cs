using System.Collections.Generic;
using _Code.Block;
using _Code.Field;
using _Code.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace _Code.CatHome
{
    public sealed class CatHomeGrowthController : MonoBehaviour
    {
        [SerializeField] private CatGrowthCatalog catalog;
        [SerializeField] private RectTransform residentsPrefab;
        [SerializeField] private Image background;

        private readonly int[] _earnedLines = new int[JsonManager.CatTypeCount];
        private BlockField _field;
        private JsonManager _save;
        private RectTransform _residents;
        private CatResidentView[] _views;

        public void Initialize(BlockField field, JsonManager save)
        {
            Unsubscribe();
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

            if (background == null)
            {
                foreach (Image candidate in FindObjectsByType<Image>(FindObjectsSortMode.None))
                {
                    if (candidate.gameObject.scene == gameObject.scene && candidate.isActiveAndEnabled &&
                        candidate.canvas != null && candidate.canvas.isActiveAndEnabled &&
                        candidate.sprite == BlockBlastSpriteLibrary.NightCatHomeBackgroundSprite &&
                        (background == null || candidate.canvas.sortingOrder < background.canvas.sortingOrder))
                    {
                        background = candidate;
                    }
                }
            }

            if (background == null)
            {
                Debug.LogError("Cat home could not find the active room background.", this);
                return;
            }

            if (_residents == null)
                _residents = Instantiate(residentsPrefab, background.rectTransform, false);
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

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            if (_residents != null)
                Destroy(_residents.gameObject);
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
            if (!isActiveAndEnabled || _save == null)
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
                if (_earnedLines[(int)cat.type] > 0)
                    FindView(cat.type)?.SetProgress(catalog, cat, _save.GetCatGrowthLines((int)cat.type), true);
            }
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
