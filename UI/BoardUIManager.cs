using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BoardUIManager : MonoBehaviour
{
    [Header("Board References")]
    [SerializeField] private RectTransform boardPanel;
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private GameObject piecePrefab;
    [SerializeField] private PieceSpriteLibrary pieceSpriteLibrary;

    [Header("Board Settings")]
    [SerializeField] private int boardSize = 8;
    [SerializeField] private float cellSize = 100f;

    [Header("Cell Colors")]
    [SerializeField] private Color lightCellColor = new Color(0.95f, 0.90f, 0.80f, 1f);
    [SerializeField] private Color darkCellColor = new Color(0.45f, 0.30f, 0.18f, 1f);

    [Header("Highlight Colors")]
    [SerializeField] private Color selectedCellColor = new Color(0.30f, 0.70f, 0.95f, 1f);
    [SerializeField] private Color availableMoveColor = new Color(0.45f, 0.85f, 0.45f, 1f);

    [Header("Board Coordinates")]
    [SerializeField] private bool showCoordinates = true;
    [SerializeField] private TMP_FontAsset coordinateFont;
    [SerializeField] private float coordinateFontSize = 18f;
    [SerializeField] private float coordinateInset = 4f;
    [SerializeField] private Color coordinateOnLightCellColor = new Color(0.55f, 0.40f, 0.25f, 1f);
    [SerializeField] private Color coordinateOnDarkCellColor = new Color(0.92f, 0.87f, 0.78f, 1f);

    private BoardCellUI[,] cells;
    private ChessGameManager chessGameManager;
    private AnalyzerManager analyzerManager;

    private RectTransform coordinatesOverlay;
    private readonly List<GameObject> coordinateObjects = new List<GameObject>();

    private void Awake()
    {
        chessGameManager = FindFirstObjectByType<ChessGameManager>();
        analyzerManager = FindFirstObjectByType<AnalyzerManager>();
    }

    public void GenerateBoard()
    {
        if (boardPanel == null || cellPrefab == null)
        {
            Debug.LogError("BoardUIManager: BoardPanel or CellPrefab is not assigned.");
            return;
        }

        ClearCoordinates();
        ClearBoardChildren();

        ConfigureGridLayout();

        cells = new BoardCellUI[boardSize, boardSize];
        boardPanel.sizeDelta = new Vector2(boardSize * cellSize, boardSize * cellSize);

        for (int y = boardSize - 1; y >= 0; y--)
        {
            for (int x = 0; x < boardSize; x++)
            {
                CreateCell(x, y);
            }
        }

        CreateCoordinatesOverlay();
    }

    public void RenderPieces(ChessBoardState boardState)
    {
        if (cells == null || piecePrefab == null || pieceSpriteLibrary == null)
        {
            Debug.LogError("BoardUIManager: Board is not ready or piece references are missing.");
            return;
        }

        ClearAllPieces();

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                PieceData piece = boardState.GetPiece(x, y);
                if (piece != null)
                    SpawnPiece(x, y, piece);
            }
        }
    }

    public void OnCellClicked(int x, int y)
    {
        if (TryHandleGameCellClick(x, y))
            return;

        if (TryHandleAnalyzerCellClick(x, y))
            return;

        Debug.LogError("BoardUIManager: No ChessGameManager or AnalyzerManager found in scene.");
    }

    public void ClearHighlights()
    {
        if (cells == null)
            return;

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                cells[x, y]?.ResetColor();
            }
        }
    }

    public void HighlightSelectedCell(int x, int y)
    {
        if (!TryGetCell(x, y, out BoardCellUI cell))
            return;

        cell.Highlight(selectedCellColor);
    }

    public void HighlightMoveCells(List<MoveData> moves)
    {
        if (cells == null || moves == null)
            return;

        for (int i = 0; i < moves.Count; i++)
        {
            MoveData move = moves[i];
            if (TryGetCell(move.ToX, move.ToY, out BoardCellUI cell))
                cell.Highlight(availableMoveColor);
        }
    }

    private void ConfigureGridLayout()
    {
        GridLayoutGroup grid = boardPanel.GetComponent<GridLayoutGroup>();
        if (grid == null)
            grid = boardPanel.gameObject.AddComponent<GridLayoutGroup>();

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = boardSize;
        grid.cellSize = new Vector2(cellSize, cellSize);
        grid.spacing = Vector2.zero;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
    }

    private void CreateCell(int x, int y)
    {
        GameObject cellObject = Instantiate(cellPrefab, boardPanel);
        cellObject.name = $"Cell_{x}_{y}";

        if (!cellObject.TryGetComponent(out BoardCellUI cell))
            cell = cellObject.AddComponent<BoardCellUI>();

        cell.Initialize(x, y, GetCellBaseColor(x, y), this);
        cells[x, y] = cell;
    }

    private void SpawnPiece(int x, int y, PieceData piece)
    {
        if (!TryGetCell(x, y, out BoardCellUI cell))
            return;

        GameObject pieceObject = Instantiate(piecePrefab, cell.transform);
        pieceObject.name = $"{piece.Color}_{piece.Type}_{x}_{y}";

        if (pieceObject.TryGetComponent(out RectTransform rect))
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 10f);
            rect.offsetMax = new Vector2(-10f, -10f);
        }

        if (!pieceObject.TryGetComponent(out ChessPieceUI pieceUI))
            pieceUI = pieceObject.AddComponent<ChessPieceUI>();

        Sprite sprite = pieceSpriteLibrary.GetSprite(piece.Color, piece.Type);
        pieceUI.Initialize(piece.Type, piece.Color, sprite);
    }

    private void CreateCoordinatesOverlay()
    {
        if (!showCoordinates || boardPanel == null)
            return;

        if (!(boardPanel.parent is RectTransform parentRect))
        {
            Debug.LogWarning("BoardUIManager: boardPanel has no RectTransform parent for coordinates overlay.");
            return;
        }

        GameObject overlayObject = new GameObject("BoardCoordinatesOverlay", typeof(RectTransform));
        coordinatesOverlay = overlayObject.GetComponent<RectTransform>();
        coordinatesOverlay.SetParent(parentRect, false);

        CopyRectTransform(boardPanel, coordinatesOverlay);
        coordinatesOverlay.SetSiblingIndex(boardPanel.GetSiblingIndex() + 1);

        CreateEdgeLabels(isFiles: true);
        CreateEdgeLabels(isFiles: false);
    }

    private void CreateEdgeLabels(bool isFiles)
    {
        for (int index = 0; index < boardSize; index++)
        {
            string text = isFiles ? ((char)('a' + index)).ToString() : (index + 1).ToString();
            int squareX = isFiles ? index : boardSize - 1;
            int squareY = isFiles ? 0 : index;

            Vector2 anchor = isFiles
                ? new Vector2((float)index / boardSize, 0f)
                : new Vector2(1f, (float)(index + 1) / boardSize);

            Vector2 pivot = isFiles
                ? new Vector2(0f, 0f)
                : new Vector2(1f, 1f);

            Vector2 position = isFiles
                ? new Vector2(coordinateInset, coordinateInset)
                : new Vector2(-coordinateInset, -coordinateInset);

            TextAlignmentOptions alignment = isFiles
                ? TextAlignmentOptions.BottomLeft
                : TextAlignmentOptions.TopRight;

            CreateCoordinateLabel(
                text,
                anchor,
                pivot,
                position,
                alignment,
                GetCoordinateColorForSquare(squareX, squareY));
        }
    }

    private void CreateCoordinateLabel(
        string labelText,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPosition,
        TextAlignmentOptions alignment,
        Color color)
    {
        if (coordinatesOverlay == null)
            return;

        TMP_FontAsset resolvedFont = coordinateFont != null ? coordinateFont : TMP_Settings.defaultFontAsset;
        if (resolvedFont == null)
        {
            Debug.LogWarning("BoardUIManager: No TMP font assigned for board coordinates and no TMP default font found.");
            return;
        }

        GameObject labelObject = new GameObject($"Coord_{labelText}", typeof(RectTransform));
        labelObject.transform.SetParent(coordinatesOverlay, false);
        coordinateObjects.Add(labelObject);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(cellSize * 0.35f, cellSize * 0.25f);

        TextMeshProUGUI tmp = labelObject.AddComponent<TextMeshProUGUI>();
        tmp.font = resolvedFont;
        tmp.text = labelText;
        tmp.fontSize = coordinateFontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
    }

    private Color GetCellBaseColor(int x, int y)
    {
        bool isLightCell = (x + y) % 2 != 0;
        return isLightCell ? lightCellColor : darkCellColor;
    }

    private Color GetCoordinateColorForSquare(int x, int y)
    {
        bool isLightCell = (x + y) % 2 != 0;
        return isLightCell ? coordinateOnLightCellColor : coordinateOnDarkCellColor;
    }

    private bool TryGetCell(int x, int y, out BoardCellUI cell)
    {
        cell = null;

        if (cells == null)
            return false;

        if (x < 0 || x >= boardSize || y < 0 || y >= boardSize)
            return false;

        cell = cells[x, y];
        return cell != null;
    }

    private bool TryHandleGameCellClick(int x, int y)
    {
        if (chessGameManager == null)
            chessGameManager = FindFirstObjectByType<ChessGameManager>();

        if (chessGameManager == null)
            return false;

        chessGameManager.HandleCellClick(x, y);
        return true;
    }

    private bool TryHandleAnalyzerCellClick(int x, int y)
    {
        if (analyzerManager == null)
            analyzerManager = FindFirstObjectByType<AnalyzerManager>();

        if (analyzerManager == null)
            return false;

        analyzerManager.HandleAnalyzerCellClick(x, y);
        return true;
    }

    private void ClearBoardChildren()
    {
        if (boardPanel == null)
            return;

        for (int i = boardPanel.childCount - 1; i >= 0; i--)
        {
            Destroy(boardPanel.GetChild(i).gameObject);
        }
    }

    private void ClearCoordinates()
    {
        for (int i = coordinateObjects.Count - 1; i >= 0; i--)
        {
            if (coordinateObjects[i] != null)
                Destroy(coordinateObjects[i]);
        }

        coordinateObjects.Clear();

        if (coordinatesOverlay != null)
        {
            Destroy(coordinatesOverlay.gameObject);
            coordinatesOverlay = null;
        }
    }

    private void ClearAllPieces()
    {
        if (cells == null)
            return;

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                BoardCellUI cell = cells[x, y];
                if (cell == null)
                    continue;

                Transform cellTransform = cell.transform;
                for (int i = cellTransform.childCount - 1; i >= 0; i--)
                {
                    Destroy(cellTransform.GetChild(i).gameObject);
                }
            }
        }
    }

    private void CopyRectTransform(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
    }
}