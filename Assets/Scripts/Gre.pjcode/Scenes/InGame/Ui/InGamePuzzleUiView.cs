using Gre.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gre.pjcode.Scenes.InGame
{
    public sealed class InGamePuzzleUiView : MonoBehaviour
    {
        enum BuyPriceMode
        {
            PriceList,
            Exponential
        }

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Board _board;
        [SerializeField] private Object _minoPrefab;
        [SerializeField] private RectTransform _minoListRoot;
        [SerializeField] private Object _minoListItemPrefab;
        [SerializeField] private RectTransform _gridRoot;
        [SerializeField] private CustomImage _boardGridPrefab;
        [SerializeField] private RectTransform _minoDragLayer;
        [SerializeField] private CustomButton _buyButton;
        [SerializeField] private CustomButton _playButton;
        [SerializeField] private CustomButton _autoMergeButton;
        [SerializeField] private RectTransform _guideRoot;
        [SerializeField] private CustomImage _boardGuidePrefab;
        [SerializeField] private CustomText[] _performanceValueTexts;
        [SerializeField] private RectTransform _performanceDiffRoot;
        [SerializeField] private CustomText _performanceDiffUpText;
        [SerializeField] private CustomText _performanceDiffDownText;
        [SerializeField] private CustomButton _boostEvolveButton;
        [SerializeField] private GameObject _boostEvolveRoot;
        [SerializeField] private CustomText _boostEvolvePriceText;
        [SerializeField] private CustomText _boostLevelText;
        [SerializeField] private GameObject _boostEvolveButtonMax;
        [SerializeField] private CustomImage _boostIcon;
        [SerializeField] private GameObject _boostEvolveButonTapGuide;
        [SerializeField] private RectTransform _attachmentRoot;
        [SerializeField] private CustomButton _attachmentButtonPrefab;
        [SerializeField] private Sprite _attachmentIconSprite;
        [SerializeField] private GameObject _bonusBoxViewRoot;
        [SerializeField] private CustomButton _bonusBoxOpenButton;
        [SerializeField] private GameObject _bonusBoxOpenButtonDefaultLayer;
        [SerializeField] private GameObject _bonusBoxOpenButtonFreeLayer;
        [SerializeField] private GameObject _bonusBoxOpenButtonTapGuide;
        [SerializeField] private TMP_Text _currentGoldText;
        [SerializeField] private TMP_Text _buyPartPriceText;
        [SerializeField] private BuyPriceMode _buyPriceMode = BuyPriceMode.PriceList;
        [SerializeField] private int[] _buyPriceList = new[] { 30, 130, 150, 200, 280, 400, 500, 980, 1120, 1250, 1500 };
        [SerializeField] private int _buyPriceBase = 30;
        [SerializeField] private float _buyPriceMultiplier = 1.5f;
        [SerializeField] private PartDataAsset _partDataAsset;
        [SerializeField] private int _startingGold = 10000;
        [SerializeField] private Vector2Int _runtimeGridSize = new Vector2Int(4, 4);
        [SerializeField] private float _runtimeCellSize = 96f;
        [SerializeField] private Color _runtimeGridColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color _runtimeBlockColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color _commonPartColor = Color.white;
        [SerializeField] private Color _rarePartColor = new Color(0.2f, 0.55f, 1f, 1f);
        [SerializeField] private Color _epicPartColor = new Color(0.68f, 0.25f, 1f, 1f);
        [SerializeField] private Color _legendPartColor = new Color(1f, 0.82f, 0.12f, 1f);
        [SerializeField] private GameObject _carAttachEffectPrefab;
        [SerializeField] private float _carAttachEffectLifetime = 2f;

        static readonly int[] RuntimePartIds = { 9, 0, 2, 1 };
        readonly List<RectTransform> _runtimeCells = new List<RectTransform>();
        readonly List<RuntimePuzzlePartIcon> _runtimeParts = new List<RuntimePuzzlePartIcon>();
        readonly Dictionary<int, RuntimePuzzlePartIcon> _occupiedCells = new Dictionary<int, RuntimePuzzlePartIcon>();
        readonly List<RuntimePuzzlePartIcon> _placedParts = new List<RuntimePuzzlePartIcon>();
        readonly float[] _runTerrainPerformances = new float[(int)TerrainType.Max];
        CarView _carView;
        int _gold;
        int _buyPrice = 30;
        int _buyCursor;
        int _buyCount;

        public Board Board => _board;
        public Sprite BoostIconSprite => _boostIcon == null ? null : _boostIcon.sprite;
        public Sprite AttachmentIconSprite => _attachmentIconSprite;
        public CustomButton PlayButton => _playButton;
        public CustomButton BuyButton => _buyButton;
        public float RunDistanceMultiplier
        {
            get
            {
                float addForceWeight = 1f;
                addForceWeight += _runTerrainPerformances[(int)TerrainType.Default] * 0.075f;
                addForceWeight += _runTerrainPerformances[(int)TerrainType.Dirt] * 0.065f;
                addForceWeight += _runTerrainPerformances[(int)TerrainType.Water] * 0.065f;
                addForceWeight += _runTerrainPerformances[(int)TerrainType.Air] * 0.065f;
                addForceWeight *= GetRunPerformanceTotal() == 0f ? 0.5f : 0.8f;
                return Mathf.Clamp(addForceWeight / 0.5f + GetRunPerformanceTotal() * 2f, 1f, 3f);
            }
        }

        void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_buyButton == null) _buyButton = FindButton("BuyButton");
            if (_playButton == null) _playButton = FindButton("StartButton");
            if (_autoMergeButton == null) _autoMergeButton = FindButton("AutoMergeButton");
            if (_boostEvolveButton == null) _boostEvolveButton = FindButton("UpgradeButton");
            if (_bonusBoxOpenButton == null) _bonusBoxOpenButton = FindButton("OpenButton");
            _carView = FindObjectOfType<CarView>();
            if (_performanceValueTexts == null || _performanceValueTexts.Length == 0)
            {
                _performanceValueTexts = new[]
                {
                    FindTextIn("Spec_Default"),
                    FindTextIn("Spec_Dirt"),
                    FindTextIn("Spec_Water"),
                    FindTextIn("Spec_Air"),
                };
            }

            if (_playButton != null) _playButton.onClick.AddListener(() => SetOpen(false));
            if (_buyButton != null) _buyButton.onClick.AddListener(BuyRuntimePart);
            if (_bonusBoxOpenButton != null) _bonusBoxOpenButton.onClick.AddListener(PlayworksBridge.InstallFullGame);

            SetGold(_startingGold);
            SetBoostLevel(0, false);
            BuildRuntimePuzzle();
            UpdatePerformanceFromPlacedParts();
            RefreshAttachmentButtons();
        }

        public void Setup(Vector2Int boardSize)
        {
            if (_board != null) _board.name = $"Board {boardSize.x}x{boardSize.y}";
            _runtimeGridSize = boardSize;
            BuildRuntimePuzzle();
            UpdatePerformanceFromPlacedParts();
        }

        public void SetOpen(bool isOpen, bool immediate = false)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = isOpen ? 1f : 0f;
                _canvasGroup.interactable = isOpen;
                _canvasGroup.blocksRaycasts = isOpen;
            }

            gameObject.SetActive(isOpen);
        }

        public void SetBuyPrice(int price)
        {
            if (_buyPartPriceText != null) _buyPartPriceText.text = price.ToString();
            if (_buyButton == null) return;
            _buyButton.SetText(price.ToString());
            _buyButton.SetState(_gold >= price ? ButtonState.Enable : ButtonState.Disable);
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            SetGold(_gold + amount);
            UpdateBuyPrice();
        }

        public void SetPerformance(int[] performances)
        {
            if (_performanceValueTexts == null) return;
            for (int i = 0; i < _performanceValueTexts.Length; i++)
            {
                CustomText text = _performanceValueTexts[i];
                if (text == null) continue;
                int value = performances != null && i < performances.Length ? performances[i] : 0;
                text.text = value.ToString();
                if (text.transform.parent != null) text.transform.parent.gameObject.SetActive(value > 0);
            }
        }

        public void SetBoostLevel(int level, bool unlockFlag)
        {
            bool show = level > 0 || unlockFlag;
            if (_boostEvolveRoot != null) _boostEvolveRoot.SetActive(show);
            if (_boostLevelText != null) _boostLevelText.text = level <= 0 ? string.Empty : level >= 6 ? "MAX" : $"Lv.{level}";
            if (_boostEvolveButtonMax != null) _boostEvolveButtonMax.SetActive(level >= 6);
        }

        public void SetBoostEvolvePrice(int price)
        {
            if (_boostEvolvePriceText != null) _boostEvolvePriceText.text = price > 0 ? price.ToString() : "FREE";
            if (_boostEvolveButton != null) _boostEvolveButton.SetState(ButtonState.Enable);
        }

        public void RefreshGridVisible()
        {
            SetChildrenActive(_gridRoot, true);
        }

        public void RefreshAttachmentButtons()
        {
            SetChildrenActive(_attachmentRoot, false);
            if (_autoMergeButton != null) _autoMergeButton.SetActive(false);
            if (_bonusBoxViewRoot != null) _bonusBoxViewRoot.SetActive(false);
        }

        public CustomButton GetAttachmentButton(int attachmentId)
        {
            if (_attachmentRoot == null || attachmentId < 0 || attachmentId >= _attachmentRoot.childCount) return null;
            return _attachmentRoot.GetChild(attachmentId).GetComponent<CustomButton>();
        }

        public void PlayFade(float alpha, float duration)
        {
            if (_canvasGroup != null) _canvasGroup.alpha = alpha;
        }

        public bool PlayAdvertisement()
        {
            PlayworksBridge.InstallFullGame();
            return true;
        }

        void UpdatePerformanceFromPlacedParts()
        {
            if (_partDataAsset == null)
            {
                SetPerformance(new int[(int)TerrainType.Max]);
                return;
            }

            int[] values = new int[(int)TerrainType.Max];
            for (int i = 0; i < _runTerrainPerformances.Length; i++) _runTerrainPerformances[i] = 0f;
            foreach (RuntimePuzzlePartIcon icon in _placedParts)
            {
                if (icon == null || !_partDataAsset.TryGetPartData(icon.PartId, out PartData partData)) continue;
                if (partData == null || partData.PerformanceData == null) continue;
                int index = (int)partData.PerformanceData.TerrainType;
                int count = (int)Mathf.Pow(2, icon.Level - 1);
                int levelBonus = (icon.Level - 1) * 5;
                int value = (int)(partData.PerformanceData.Value * count * 100f) + levelBonus;
                if (index < 0 || index >= values.Length) continue;
                values[index] += value;
                _runTerrainPerformances[index] += partData.PerformanceData.Value * count;
            }

            SetPerformance(values);
        }

        void BuildRuntimePuzzle()
        {
            if (_gridRoot == null || _minoListRoot == null || _partDataAsset == null) return;

            ClearChildren(_gridRoot);
            ClearChildren(_minoListRoot);
            _runtimeCells.Clear();
            _runtimeParts.Clear();
            _occupiedCells.Clear();
            _placedParts.Clear();
            _buyCursor = 0;
            _buyCount = 0;
            float cellSize = GetRuntimeCellSize();
            if (_board != null) _board.Setup(_runtimeGridSize, cellSize);

            for (int y = 0; y < _runtimeGridSize.y; y++)
            {
                for (int x = 0; x < _runtimeGridSize.x; x++)
                {
                    RectTransform cell = CreateBoardGrid(new Vector2Int(x, y), cellSize);
                    _runtimeCells.Add(cell);
                }
            }

            foreach (int partId in RuntimePartIds)
            {
                CreateTrayPart(partId, cellSize);
            }

            UpdateBuyPrice();
        }

        void BuyRuntimePart()
        {
            if (_partDataAsset == null || _minoListRoot == null) return;
            UpdateBuyPrice();
            if (_gold < _buyPrice)
            {
                PlayableSoundEffects.Play(PlayableSfx.Cancel);
                return;
            }

            _gold -= _buyPrice;
            SetGold(_gold);
            PlayableSoundEffects.Play(PlayableSfx.Buy);
            CreateTrayPart(RuntimePartIds[_buyCursor % RuntimePartIds.Length], GetRuntimeCellSize());
            _buyCursor++;
            _buyCount++;
            UpdateBuyPrice();
        }

        void CreateTrayPart(int partId, float cellSize)
        {
            if (!_partDataAsset.TryGetPartData(partId, out PartData partData)) return;
            if (partData == null) return;

            Vector2Int[] pattern = partData.ShapeType.GetBlockPattern(partData.Rotate);
            RectTransform listItem = CreateListItem($"PartSlot_{partId}", cellSize);
            RectTransform icon = CreateMinoIcon($"Part_{partId}", listItem, cellSize);
            RuntimePuzzlePartIcon drag = icon.gameObject.AddComponent<RuntimePuzzlePartIcon>();
            drag.Setup(this, partId, pattern, cellSize, partData.GetMinoSprite(1), partData.GetBlockSprite(1), _runtimeBlockColor, _minoDragLayer, _boardGuidePrefab, partData.Rotate, partData.MinoSpriteScale);
            _runtimeParts.Add(drag);
        }

        internal void DropPart(RuntimePuzzlePartIcon icon, Vector2 screenPosition)
        {
            RuntimePuzzlePartIcon mergeTarget = GetMergeTarget(icon, screenPosition);
            if (mergeTarget != null)
            {
                MergePart(icon, mergeTarget);
                return;
            }

            RuntimePuzzlePartIcon trayMergeTarget = GetTrayMergeTarget(icon, screenPosition);
            if (trayMergeTarget != null)
            {
                MergePart(icon, trayMergeTarget);
                return;
            }

            if (IsInTray(screenPosition))
            {
                RemovePlacedPart(icon);
                icon.PlaceInTray();
                PlayableSoundEffects.Play(PlayableSfx.PartSet);
                UpdatePerformanceFromPlacedParts();
                return;
            }

            int cellIndex = GetCellIndex(screenPosition);
            Vector2Int origin = GetCellPosition(cellIndex);
            if (cellIndex < 0 || !CanPlace(icon, origin))
            {
                if (icon.IsPlaced)
                {
                    RemovePlacedPart(icon);
                    icon.PlaceInTray();
                    PlayableSoundEffects.Play(PlayableSfx.PartSet);
                    UpdatePerformanceFromPlacedParts();
                }
                else
                {
                    PlayableSoundEffects.Play(PlayableSfx.Cancel);
                    icon.ReturnToStart();
                }

                return;
            }

            RemovePlacedPart(icon);
            foreach (Vector2Int offset in icon.Pattern)
            {
                _occupiedCells[GetCellIndex(origin + offset)] = icon;
            }

            if (!_placedParts.Contains(icon)) _placedParts.Add(icon);
            icon.PlaceOn(_gridRoot, _runtimeCells[cellIndex].anchoredPosition, cellIndex);
            PlayableSoundEffects.Play(PlayableSfx.PartSet);
            AttachCarPart(icon);
            UpdatePerformanceFromPlacedParts();
        }

        void MergePart(RuntimePuzzlePartIcon source, RuntimePuzzlePartIcon target)
        {
            if (source.PartId != target.PartId || source.Level != target.Level)
            {
                PlayableSoundEffects.Play(PlayableSfx.Cancel);
                source.ReturnToStart();
                return;
            }

            RemovePlacedPart(source);
            _runtimeParts.Remove(source);
            source.HideTraySlot();
            Destroy(source.gameObject);
            if (_carView != null && !string.IsNullOrEmpty(target.LinkedPartUniqueId)) _carView.DetachPart(target.LinkedPartUniqueId);

            target.SetLevel(target.Level + 1, GetPartSprite(target.PartId, target.Level + 1), GetBlockSprite(target.PartId, target.Level + 1), GetMinoSpriteScale(target.PartId));
            PlayableSoundEffects.Play(PlayableSfx.Merge);
            if (target.IsPlaced) AttachCarPart(target);
            UpdateBuyPrice();
            UpdatePerformanceFromPlacedParts();
        }

        bool CanPlace(RuntimePuzzlePartIcon icon, Vector2Int origin)
        {
            foreach (Vector2Int offset in icon.Pattern)
            {
                Vector2Int cell = origin + offset;
                int index = GetCellIndex(cell);
                if (index < 0) return false;
                if (_occupiedCells.TryGetValue(index, out RuntimePuzzlePartIcon other) && other != icon) return false;
            }

            return true;
        }

        void RemovePlacedPart(RuntimePuzzlePartIcon icon)
        {
            if (icon == null || !icon.IsPlaced) return;

            List<int> removeCells = new List<int>();
            foreach (KeyValuePair<int, RuntimePuzzlePartIcon> pair in _occupiedCells)
            {
                if (pair.Value == icon) removeCells.Add(pair.Key);
            }

            foreach (int cell in removeCells) _occupiedCells.Remove(cell);

            _placedParts.Remove(icon);
            icon.ClearPlaced();
            if (_carView != null && !string.IsNullOrEmpty(icon.LinkedPartUniqueId)) _carView.DetachPart(icon.LinkedPartUniqueId);
            icon.LinkedPartUniqueId = string.Empty;
        }

        void SetGold(int value)
        {
            _gold = value;
            if (_currentGoldText != null) _currentGoldText.text = _gold.ToString();
            Canvas canvas = GetComponentInParent<Canvas>();
            CustomText[] texts = canvas == null ? GetComponentsInChildren<CustomText>(true) : canvas.GetComponentsInChildren<CustomText>(true);
            foreach (CustomText text in texts)
            {
                if (text != null && text.name == "GoldText") text.text = _gold.ToString();
            }
        }

        void UpdateBuyPrice()
        {
            _buyPrice = GetBuyPrice(_buyCount);
            SetBuyPrice(_buyPrice);
        }

        int GetBuyPrice(int buyCount)
        {
            int index = Mathf.Max(0, buyCount);
            if (_buyPriceMode == BuyPriceMode.PriceList && _buyPriceList != null && _buyPriceList.Length > 0)
            {
                return Mathf.Max(0, _buyPriceList[Mathf.Min(index, _buyPriceList.Length - 1)]);
            }

            float multiplier = Mathf.Max(1f, _buyPriceMultiplier);
            return Mathf.Max(0, Mathf.RoundToInt(_buyPriceBase * Mathf.Pow(multiplier, index)));
        }

        float GetRunPerformanceTotal()
        {
            float total = 0f;
            foreach (float performance in _runTerrainPerformances) total += performance;
            return total;
        }

        internal Color GetPartLevelColor(int level)
        {
            if (level <= 1) return _commonPartColor;
            if (level == 2) return _rarePartColor;
            if (level == 3) return _epicPartColor;
            return _legendPartColor;
        }

        void AttachCarPart(RuntimePuzzlePartIcon icon)
        {
            if (_carView == null || !_partDataAsset.TryGetPartData(icon.PartId, out PartData partData)) return;
            icon.LinkedPartUniqueId = _carView.AttachPart(partData.GetPrefab(icon.Level), icon.PartId, icon.LinkedPartUniqueId);
            PlayCarAttachEffect(icon.LinkedPartUniqueId);
        }

        void PlayCarAttachEffect(string uniqueId)
        {
            if (_carView == null) return;

            List<PartView> parts = string.IsNullOrEmpty(uniqueId) ? null : _carView.GetPartViews(uniqueId);
            if (parts == null || parts.Count == 0)
            {
                SpawnCarAttachEffect(_carView.transform);
                return;
            }

            foreach (PartView part in parts)
            {
                if (part != null) SpawnCarAttachEffect(part.transform);
            }
        }

        void SpawnCarAttachEffect(Transform target)
        {
            if (target == null) return;

            GameObject effect = _carAttachEffectPrefab != null
                ? Instantiate(_carAttachEffectPrefab, target.position, target.rotation, target)
                : CreateDefaultCarAttachEffect(target);
            Destroy(effect, Mathf.Max(0.1f, _carAttachEffectLifetime));
        }

        GameObject CreateDefaultCarAttachEffect(Transform target)
        {
            GameObject effect = new GameObject("CarAttachEffect");
            effect.transform.SetParent(target, false);
            effect.transform.localPosition = Vector3.zero;
            effect.transform.localRotation = Quaternion.identity;

            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.45f;
            main.loop = false;
            main.startLifetime = 0.35f;
            main.startSpeed = 1.8f;
            main.startSize = 0.18f;
            main.startColor = new Color(1f, 0.85f, 0.25f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            particles.Play();
            return effect;
        }

        RuntimePuzzlePartIcon GetMergeTarget(RuntimePuzzlePartIcon icon, Vector2 screenPosition)
        {
            int cellIndex = GetCellIndex(screenPosition);
            if (cellIndex < 0) return null;
            if (!_occupiedCells.TryGetValue(cellIndex, out RuntimePuzzlePartIcon target)) return null;
            return target == icon ? null : target;
        }

        RuntimePuzzlePartIcon GetTrayMergeTarget(RuntimePuzzlePartIcon icon, Vector2 screenPosition)
        {
            if (!IsInTray(screenPosition)) return null;

            Camera camera = GetUiCamera();
            foreach (RuntimePuzzlePartIcon target in _runtimeParts)
            {
                if (target == null || target == icon || target.IsPlaced) continue;
                RectTransform rect = target.transform as RectTransform;
                if (rect == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, camera)) return target;
            }

            return null;
        }

        bool IsInTray(Vector2 screenPosition)
        {
            if (_minoListRoot == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(_minoListRoot, screenPosition, GetUiCamera());
        }

        Camera GetUiCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return canvas.worldCamera;
        }

        Sprite GetPartSprite(int partId, int level)
        {
            return _partDataAsset.TryGetPartData(partId, out PartData partData) ? partData.GetMinoSprite(level) : null;
        }

        Sprite GetBlockSprite(int partId, int level)
        {
            return _partDataAsset.TryGetPartData(partId, out PartData partData) ? partData.GetBlockSprite(level) : null;
        }

        float GetMinoSpriteScale(int partId)
        {
            PartData partData;
            return _partDataAsset.TryGetPartData(partId, out partData) ? partData.MinoSpriteScale : 1f;
        }

        int GetCellIndex(Vector2 screenPosition)
        {
            Camera camera = null;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) camera = canvas.worldCamera;

            for (int i = 0; i < _runtimeCells.Count; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(_runtimeCells[i], screenPosition, camera))
                {
                    return i;
                }
            }

            return -1;
        }

        int GetCellIndex(Vector2Int cell)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= _runtimeGridSize.x || cell.y >= _runtimeGridSize.y) return -1;
            return cell.y * _runtimeGridSize.x + cell.x;
        }

        Vector2Int GetCellPosition(int index)
        {
            if (index < 0) return new Vector2Int(-1, -1);
            return new Vector2Int(index % _runtimeGridSize.x, index / _runtimeGridSize.x);
        }

        float GetRuntimeCellSize()
        {
            if (_boardGridPrefab != null) return _boardGridPrefab.rectTransform.sizeDelta.x;
            return _runtimeCellSize;
        }

        RectTransform CreateBoardGrid(Vector2Int cellPos, float cellSize)
        {
            RectTransform cell;
            if (_boardGridPrefab != null)
            {
                CustomImage grid = Instantiate(_boardGridPrefab, _gridRoot);
                grid.name = $"{_boardGridPrefab.name} ({cellPos.x}, {cellPos.y})";
                cell = grid.rectTransform;
                grid.raycastTarget = true;
            }
            else
            {
                cell = CreateUiRect($"Cell_{cellPos.x}_{cellPos.y}", _gridRoot, cellSize, cellSize);
                Image image = cell.gameObject.AddComponent<Image>();
                image.color = _runtimeGridColor;
                image.raycastTarget = true;
            }

            cell.sizeDelta = Vector2.one * cellSize;
            cell.anchoredPosition = _board == null
                ? new Vector2(
                    (cellPos.x - (_runtimeGridSize.x - 1) * 0.5f) * cellSize,
                    (cellPos.y - (_runtimeGridSize.y - 1) * 0.5f) * cellSize
                )
                : _board.GetPositionOnBoard(cellPos);
            return cell;
        }

        RectTransform CreateListItem(string objectName, float cellSize)
        {
            RectTransform item = InstantiatePrefabRect(_minoListItemPrefab, _minoListRoot);
            if (item == null) item = CreateUiRect(objectName, _minoListRoot, cellSize * 2f, cellSize * 2f);
            item.name = objectName;
            item.sizeDelta = Vector2.one * cellSize * 2f;
            return item;
        }

        RectTransform CreateMinoIcon(string objectName, RectTransform parent, float cellSize)
        {
            RectTransform icon = InstantiatePrefabRect(_minoPrefab, parent);
            if (icon == null) icon = CreateUiRect(objectName, parent, cellSize, cellSize);
            icon.name = objectName;
            icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = Vector2.zero;
            return icon;
        }

        static RectTransform InstantiatePrefabRect(Object prefab, Transform parent)
        {
            if (prefab == null) return null;
            Object instance = Instantiate(prefab, parent);
            if (instance is GameObject go) return go.transform as RectTransform;
            if (instance is Component component) return component.transform as RectTransform;
            Destroy(instance);
            return null;
        }

        static RectTransform CreateUiRect(string objectName, Transform parent, float width, float height)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
            return rect;
        }

        CustomButton FindButton(string objectName)
        {
            foreach (CustomButton button in GetComponentsInChildren<CustomButton>(true))
            {
                if (button.name == objectName) return button;
            }

            return null;
        }

        CustomText FindTextIn(string objectName)
        {
            Transform root = FindChild(transform, objectName);
            return root == null ? null : root.GetComponentInChildren<CustomText>(true);
        }

        static void SetChildrenActive(Transform root, bool active)
        {
            if (root == null) return;
            for (int i = 0; i < root.childCount; i++)
            {
                root.GetChild(i).gameObject.SetActive(active);
            }
        }

        internal static void ClearChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }

        static Transform FindChild(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), objectName);
                if (found != null) return found;
            }

            return null;
        }
    }

    public sealed class RuntimePuzzlePartIcon : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        InGamePuzzleUiView _owner;
        RectTransform _rect;
        Canvas _canvas;
        RectTransform _dragLayer;
        RectTransform _homeParent;
        Vector2 _homePosition;
        Transform _startParent;
        Vector2 _startPosition;
        Vector2 _dragScreenOffset;
        float _cellSize;
        int _spriteRotate;
        float _spriteScale = 1f;
        Sprite _blockSprite;
        Color _color;
        CustomImage _blockPrefab;

        public int PartId { get; private set; }
        public int Level { get; private set; } = 1;
        public int CellIndex { get; private set; } = -1;
        public bool IsPlaced => CellIndex >= 0;
        public string LinkedPartUniqueId { get; set; } = string.Empty;
        public IReadOnlyList<Vector2Int> Pattern { get; private set; }

        public void Setup(
            InGamePuzzleUiView owner,
            int partId,
            Vector2Int[] pattern,
            float cellSize,
            Sprite sprite,
            Sprite blockSprite,
            Color color,
            RectTransform dragLayer,
            CustomImage blockPrefab,
            int spriteRotate,
            float spriteScale)
        {
            _owner = owner;
            PartId = partId;
            Pattern = pattern;
            _cellSize = cellSize;
            _blockSprite = blockSprite;
            _color = color;
            _blockPrefab = blockPrefab;
            _spriteRotate = spriteRotate;
            _spriteScale = spriteScale;
            _rect = transform as RectTransform;
            _canvas = GetComponentInParent<Canvas>();
            _dragLayer = dragLayer;
            _homeParent = transform.parent as RectTransform;
            _homePosition = _rect.anchoredPosition;
            BuildBlocks(pattern, cellSize, sprite, blockSprite, color, blockPrefab);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            PlayableSoundEffects.Play(PlayableSfx.PartPick);
            _startParent = transform.parent;
            _startPosition = _rect.anchoredPosition;
            if (_dragLayer != null) _rect.SetParent(_dragLayer, true);
            float scale = _canvas == null ? 1f : _canvas.scaleFactor;
            _dragScreenOffset = Vector2.up * _cellSize * scale;
            _rect.anchoredPosition += Vector2.up * _cellSize;
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            float scale = _canvas == null ? 1f : _canvas.scaleFactor;
            _rect.anchoredPosition += eventData.delta / Mathf.Max(0.01f, scale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _owner.DropPart(this, eventData.position + _dragScreenOffset);
        }

        public void PlaceOn(RectTransform parent, Vector2 anchoredPosition, int cellIndex)
        {
            _rect.SetParent(parent, false);
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.anchoredPosition = anchoredPosition;
            CellIndex = cellIndex;
            HideTraySlot();
        }

        public void PlaceInTray()
        {
            ClearPlaced();
            ShowTraySlot();
            _rect.SetParent(_homeParent, false);
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.anchoredPosition = _homePosition;
        }

        public void ClearPlaced()
        {
            CellIndex = -1;
        }

        public void ReturnToStart()
        {
            if (_startParent == _homeParent) ShowTraySlot();
            _rect.SetParent(_startParent, false);
            _rect.anchoredPosition = _startPosition;
        }

        public void HideTraySlot()
        {
            if (_homeParent != null) _homeParent.gameObject.SetActive(false);
        }

        void ShowTraySlot()
        {
            if (_homeParent != null) _homeParent.gameObject.SetActive(true);
        }

        public void SetLevel(int level, Sprite partSprite, Sprite blockSprite, float spriteScale)
        {
            Level = level;
            if (blockSprite != null) _blockSprite = blockSprite;
            _spriteScale = spriteScale;
            BuildBlocks(Pattern as Vector2Int[] ?? new List<Vector2Int>(Pattern).ToArray(), _cellSize, partSprite, _blockSprite, _color, _blockPrefab);
        }

        void BuildBlocks(Vector2Int[] pattern, float cellSize, Sprite sprite, Sprite blockSprite, Color color, CustomImage blockPrefab)
        {
            InGamePuzzleUiView.ClearChildren(transform);

            Vector2Int min = Vector2Int.zero;
            Vector2Int max = Vector2Int.zero;
            foreach (Vector2Int offset in pattern)
            {
                min = Vector2Int.Min(min, offset);
                max = Vector2Int.Max(max, offset);
            }

            _rect.sizeDelta = new Vector2(max.x - min.x + 1, max.y - min.y + 1) * cellSize;

            foreach (Vector2Int offset in pattern)
            {
                RectTransform block = CreateBlock(offset, cellSize, blockPrefab);
                Image image = block.GetComponent<Image>();
                if (image == null) image = block.gameObject.AddComponent<Image>();
                if (blockSprite != null) image.sprite = blockSprite;
                image.color = _owner.GetPartLevelColor(Level);
                image.raycastTarget = true;
            }

            RectTransform partRect = CreateBlock(Vector2Int.zero, cellSize * 1.25f * Mathf.Max(0.01f, _spriteScale), null);
            Image partImage = partRect.gameObject.AddComponent<Image>();
            partImage.name = "PartImage";
            partImage.sprite = sprite;
            partImage.preserveAspect = true;
            partImage.raycastTarget = false;
            partRect.anchoredPosition = new Vector2((min.x + max.x) * 0.5f * cellSize, (min.y + max.y) * 0.5f * cellSize);
            partRect.localEulerAngles = new Vector3(0f, 0f, _spriteRotate * 90f);
        }

        RectTransform CreateBlock(Vector2Int offset, float cellSize, CustomImage prefab)
        {
            RectTransform block;
            if (prefab != null)
            {
                CustomImage image = Instantiate(prefab, transform);
                block = image.rectTransform;
            }
            else
            {
                GameObject go = new GameObject("Block", typeof(RectTransform));
                block = go.GetComponent<RectTransform>();
                block.SetParent(transform, false);
            }

            block.name = "Block";
            block.anchorMin = block.anchorMax = block.pivot = new Vector2(0.5f, 0.5f);
            block.sizeDelta = Vector2.one * cellSize;
            block.anchoredPosition = new Vector2(offset.x * cellSize, offset.y * cellSize);
            return block;
        }

    }

    public enum PlayableSfx
    {
        Tap,
        Cancel,
        Buy,
        PartPick,
        PartSet,
        Merge,
        Pull,
        Launch,
        Coin,
        Dash,
        Collision,
        Finish,
        Claim,
        Count
    }

    public sealed class PlayableSoundEffects : MonoBehaviour
    {
        static PlayableSoundEffects _instance;
        static readonly AudioClip[] _clips = new AudioClip[(int)PlayableSfx.Count];

        [SerializeField] private float _volume = 1f;
        [SerializeField] private AudioSource _audioSource;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureAudioSource();
        }

        public static void Register(PlayableSfx cue, AudioClip clip)
        {
            int index = (int)cue;
            if (index < 0 || index >= _clips.Length) return;
            _clips[index] = clip;
        }

        public static void Play(PlayableSfx cue)
        {
            PlayableSoundEffects player = Instance();
            if (player == null) return;
            player.PlayInternal(cue);
        }

        static PlayableSoundEffects Instance()
        {
            if (_instance != null) return _instance;

            PlayableSoundEffects found = FindObjectOfType<PlayableSoundEffects>();
            if (found != null)
            {
                _instance = found;
                return _instance;
            }

            GameObject go = new GameObject("PlayableSoundEffects");
            _instance = go.AddComponent<PlayableSoundEffects>();
            return _instance;
        }

        void PlayInternal(PlayableSfx cue)
        {
            EnsureAudioSource();
            AudioClip clip = GetClip(cue);
            if (clip != null) _audioSource.PlayOneShot(clip, _volume);
        }

        void EnsureAudioSource()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }

        static AudioClip GetClip(PlayableSfx cue)
        {
            int index = (int)cue;
            if (index < 0 || index >= _clips.Length) return null;
            return _clips[index];
        }
    }
}
