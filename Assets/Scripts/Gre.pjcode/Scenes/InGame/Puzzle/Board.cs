using UnityEngine;

namespace Gre.pjcode.Scenes.InGame
{
    public sealed class Board : MonoBehaviour
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private float _cellSize = 100f;

        Vector2Int _boardSize;

        public RectTransform Rect => _rectTransform == null ? transform as RectTransform : _rectTransform;
        public float CellSize => _cellSize;

        public void Setup(Vector2Int boardSize, float cellSize)
        {
            _boardSize = boardSize;
            _cellSize = cellSize;
            if (_rectTransform == null) _rectTransform = transform as RectTransform;
        }

        public Vector2 GetPositionOnBoard(Vector2Int cellPos)
        {
            Vector2 boardSize = _boardSize;
            Vector2 cell = cellPos;
            Vector2 leftBottomPos = -_cellSize * boardSize / 2f + Vector2.one * (_cellSize * 0.5f);
            return leftBottomPos + cell * _cellSize;
        }
    }
}
