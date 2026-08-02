using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gre.pjcode.Scenes.InGame
{
    [CreateAssetMenu(fileName = "dat_part", menuName = "Grenge/DataAsset/PartDataAsset")]
    public sealed class PartDataAsset : ScriptableObject
    {
        [SerializeField] private List<PartData> _partDataList = new();

        public IReadOnlyList<PartData> PartDataList => _partDataList;

        public bool TryGetPartData(int partId, out PartData partData)
        {
            if (partId >= 0 && partId < _partDataList.Count)
            {
                partData = _partDataList[partId];
                return true;
            }

            partData = null;
            return false;
        }

        public PerformanceData GetPerformanceData(int partId)
        {
            return TryGetPartData(partId, out PartData partData) ? partData.PerformanceData : null;
        }

        public int GetId(PartData partData)
        {
            var index = _partDataList.IndexOf(partData);
            if (index < 0)
            {
                throw new ArgumentException("PartData is not found in PartDataAsset.", nameof(partData));
            }

            return index;
        }
    }

    [Serializable]
    public sealed class PartData
    {
        [SerializeField] private MinoShapeType _shapeType;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Sprite _minoSprite;
        [SerializeField] private Sprite _blockSprite;
        [SerializeField] [Range(0, 3)] private int _rotate;
        [SerializeField] private PerformanceData _performanceData;
        [SerializeField] private List<PartViewData> _levelUpViews = new();

        public MinoShapeType ShapeType => _shapeType;
        public int Rotate => _rotate;
        public PerformanceData PerformanceData => _performanceData;
        public IReadOnlyList<PartViewData> LevelUpViews => _levelUpViews;
        public PartView Prefab => _prefab == null ? null : _prefab.GetComponent<PartView>();

        public PartView GetPrefab(int level)
        {
            var index = level - 2;
            if (index < 0 || _levelUpViews.Count == 0) return Prefab;
            return index >= _levelUpViews.Count ? _levelUpViews[^1].Prefab : _levelUpViews[index].Prefab;
        }

        public Sprite GetMinoSprite(int level)
        {
            var index = level - 2;
            if (index < 0 || _levelUpViews.Count == 0) return _minoSprite;
            return index >= _levelUpViews.Count ? _levelUpViews[^1].MinoSprite : _levelUpViews[index].MinoSprite;
        }

        public Sprite GetBlockSprite(int level)
        {
            var index = level - 2;
            if (index < 0 || _levelUpViews.Count == 0) return _blockSprite;
            var sprite = index >= _levelUpViews.Count ? _levelUpViews[^1].BlockSprite : _levelUpViews[index].BlockSprite;
            return sprite != null ? sprite : _blockSprite;
        }

        public bool IsLevelUpViewCountStop(int level)
        {
            return level - 2 >= _levelUpViews.Count;
        }
    }

    [Serializable]
    public sealed class PerformanceData
    {
        [SerializeField] private TerrainType _terrainType;
        [SerializeField] private float _value;

        public TerrainType TerrainType => _terrainType;
        public float Value => _value;
    }

    [Serializable]
    public sealed class PartViewData
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Sprite _minoSprite;
        [SerializeField] private Sprite _blockSprite;

        public PartView Prefab => _prefab == null ? null : _prefab.GetComponent<PartView>();
        public Sprite MinoSprite => _minoSprite;
        public Sprite BlockSprite => _blockSprite;
    }

}
