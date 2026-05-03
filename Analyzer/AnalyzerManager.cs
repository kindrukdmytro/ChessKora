using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

public class AnalyzerManager : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private BoardUIManager boardUIManager;
    [SerializeField] private TMP_Text statusText;

    [Header("Player Names")]
    [SerializeField] private TMP_Text whitePlayerNameText;
    [SerializeField] private TMP_Text blackPlayerNameText;

    [Header("Move History")]
    [SerializeField] private TMP_Text moveHistoryText;

    [Header("Replay Info")]
    [SerializeField] private TMP_Text replayCounterText;

    [Header("Engine UI")]
    [SerializeField] private TMP_Text engineStatusText;
    [SerializeField] private TMP_Text evaluationText;
    [SerializeField] private TMP_Text bestMoveText;
    [SerializeField] private TMP_Text depthText;

    [Header("Engine Settings")]
    [SerializeField] private string engineRelativePath = "Engines/stockfish/stockfish.exe";
    [SerializeField] private int analysisDepth = 12;
    [SerializeField] private bool autoAnalyzeOnReplayChange = false;

    private SavedGameData currentSaveData;
    private ChessBoardState currentBoardState;

    private int currentPlyIndex;
    private int totalPlyCount;
    private bool hasReplayData;
    private PieceColor currentReplayTurn = PieceColor.White;

    private UciEngineClient engineClient;
    private string currentFen = string.Empty;

    private bool hasSelection;
    private int selectedX = -1;
    private int selectedY = -1;
    private readonly List<MoveData> currentLegalMoves = new List<MoveData>();

    private readonly List<MoveData> variationMoves = new List<MoveData>();
    private int variationStartPlyIndex;
    private MoveData currentLastMove;

    private bool IsInVariationMode => variationMoves.Count > 0;
    private bool HasLoadedGame => currentSaveData != null;

    private void Awake()
    {
        if (boardUIManager == null)
            boardUIManager = FindFirstObjectByType<BoardUIManager>();
    }

    private void Start()
    {
        if (boardUIManager == null)
        {
            Debug.LogError("AnalyzerManager: BoardUIManager not found in scene.");
            return;
        }

        boardUIManager.GenerateBoard();
        ShowEmptyAnalyzerState();
        InitializeEngine();
    }

    private void Update()
    {
        engineClient?.PumpEvents();
    }

    private void OnDestroy()
    {
        if (engineClient == null)
            return;

        engineClient.Dispose();
        engineClient = null;
    }

    public void LoadSavedGameByFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            Debug.LogWarning("AnalyzerManager: fileName is empty.");
            return;
        }

        ResetAnalysisAndEditingState();

        SavedGameData loadedSave = SaveSystem.LoadGame(fileName);
        if (loadedSave == null)
        {
            UpdateStatusText("Failed to load saved game.");
            return;
        }

        currentSaveData = loadedSave;
        hasReplayData = currentSaveData.PlayedMoves != null && currentSaveData.PlayedMoves.Count > 0;
        totalPlyCount = hasReplayData ? currentSaveData.PlayedMoves.Count : 0;
        currentPlyIndex = totalPlyCount;

        UpdatePlayerTexts();
        RefreshMoveHistoryText();
        RebuildBoardForCurrentPly();
    }

    public void GoToStart()
    {
        NavigateReplay(0);
    }

    public void GoToPreviousMove()
    {
        NavigateReplay(Mathf.Max(0, currentPlyIndex - 1));
    }

    public void GoToNextMove()
    {
        NavigateReplay(Mathf.Min(totalPlyCount, currentPlyIndex + 1));
    }

    public void GoToEnd()
    {
        if (!HasLoadedGame)
            return;

        StopCurrentAnalysis();
        ClearSelection();
        ClearVariation();

        if (!hasReplayData)
        {
            BuildBoardFromSavedPieces();
            UpdateAnalyzerTexts();
            return;
        }

        currentPlyIndex = totalPlyCount;
        RebuildBoardForCurrentPly();
    }

    public void HandleAnalyzerCellClick(int x, int y)
    {
        if (!HasLoadedGame || currentBoardState == null)
            return;

        PieceData clickedPiece = currentBoardState.GetPiece(x, y);

        if (!hasSelection)
        {
            if (clickedPiece != null && clickedPiece.Color == currentReplayTurn)
                SelectPiece(x, y);

            return;
        }

        if (selectedX == x && selectedY == y)
        {
            ClearSelection();
            return;
        }

        MoveData selectedMove = FindMoveTo(x, y);
        if (selectedMove != null)
        {
            ExecuteVariationMove(selectedMove);
            return;
        }

        if (clickedPiece != null && clickedPiece.Color == currentReplayTurn)
        {
            SelectPiece(x, y);
            return;
        }

        ClearSelection();
    }

    public void AnalyzeCurrentPosition()
    {
        if (engineClient == null)
        {
            UpdateEngineStatus("Engine is not initialized.");
            return;
        }

        if (string.IsNullOrWhiteSpace(currentFen))
        {
            UpdateEngineStatus("No position to analyze.");
            return;
        }

        ClearAnalysisTexts();
        boardUIManager.ClearHighlights();
        engineClient.AnalyzeFen(currentFen, analysisDepth);
    }

    public void StopCurrentAnalysis()
    {
        engineClient?.StopAnalysis();

        boardUIManager.ClearHighlights();
        UpdateEngineStatus("Engine ready. Press Analyze Position.");
        ClearAnalysisTexts();
    }

    private void NavigateReplay(int targetPly)
    {
        if (!HasLoadedGame)
            return;

        if (!hasReplayData)
        {
            UpdateStatusText("Replay is unavailable for this save.");
            return;
        }

        StopCurrentAnalysis();
        ClearSelection();
        ClearVariation();

        currentPlyIndex = targetPly;
        RebuildBoardForCurrentPly();
    }

    private void ResetAnalysisAndEditingState()
    {
        StopCurrentAnalysis();
        ClearSelection();
        ClearVariation();
    }

    private void ShowEmptyAnalyzerState()
    {
        currentSaveData = null;
        currentBoardState = new ChessBoardState();
        currentBoardState.ClearBoard();

        boardUIManager.RenderPieces(currentBoardState);
        boardUIManager.ClearHighlights();

        SetPlayerTexts("White", "Black");
        SetMoveHistoryText(string.Empty);
        SetReplayCounterText("0 / 0");

        ClearAnalysisTexts();
        UpdateStatusText("Analyzer: load a saved game.");
        UpdateEngineStatus("Engine: waiting.");
    }

    private void InitializeEngine()
    {
        string resolvedPath = ResolveEnginePath(engineRelativePath);

        engineClient = new UciEngineClient();
        engineClient.OnEngineStatusChanged += HandleEngineStatusChanged;
        engineClient.OnAnalysisUpdated += HandleAnalysisUpdated;

        if (!engineClient.StartEngine(resolvedPath))
            UpdateEngineStatus($"Engine failed to start. Path: {resolvedPath}");
    }

    private void HandleEngineStatusChanged(string status)
    {
        UpdateEngineStatus(status);
    }

    private void HandleAnalysisUpdated(UciAnalysisResult result)
    {
        if (result == null)
            return;

        if (depthText != null)
            depthText.text = $"Depth: {result.Depth}";

        if (evaluationText != null)
        {
            if (result.HasMateScore)
            {
                evaluationText.text = $"Evaluation: Mate {result.MateIn}";
            }
            else if (result.HasCentipawnScore)
            {
                float pawns = result.CentipawnScore / 100f;
                evaluationText.text = $"Evaluation: {pawns:+0.00;-0.00;0.00}";
            }
            else
            {
                evaluationText.text = "Evaluation: -";
            }
        }

        string moveToShow = GetBestMoveToDisplay(result);

        if (bestMoveText != null)
            bestMoveText.text = $"Best Move: {(string.IsNullOrWhiteSpace(moveToShow) ? "-" : moveToShow)}";

        HighlightEngineMove(moveToShow);
    }

    private static string GetBestMoveToDisplay(UciAnalysisResult result)
    {
        if (result == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(result.BestMove) && result.BestMove != "(none)")
            return result.BestMove;

        if (string.IsNullOrWhiteSpace(result.PrincipalVariation))
            return string.Empty;

        string[] pvMoves = result.PrincipalVariation.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        return pvMoves.Length > 0 ? pvMoves[0] : string.Empty;
    }

    private void HighlightEngineMove(string uciMove)
    {
        boardUIManager.ClearHighlights();

        if (!TryParseUciMove(uciMove, out int fromX, out int fromY, out int toX, out int toY))
            return;

        boardUIManager.HighlightSelectedCell(fromX, fromY);

        List<MoveData> highlightMoves = new List<MoveData>(1)
        {
            new MoveData(fromX, fromY, toX, toY)
        };

        boardUIManager.HighlightMoveCells(highlightMoves);
    }

    private static bool TryParseUciMove(string uciMove, out int fromX, out int fromY, out int toX, out int toY)
    {
        fromX = -1;
        fromY = -1;
        toX = -1;
        toY = -1;

        if (string.IsNullOrWhiteSpace(uciMove))
            return false;

        uciMove = uciMove.Trim().ToLowerInvariant();
        if (uciMove.Length < 4)
            return false;

        char fromFile = uciMove[0];
        char fromRank = uciMove[1];
        char toFile = uciMove[2];
        char toRank = uciMove[3];

        bool filesValid = fromFile >= 'a' && fromFile <= 'h' && toFile >= 'a' && toFile <= 'h';
        bool ranksValid = fromRank >= '1' && fromRank <= '8' && toRank >= '1' && toRank <= '8';

        if (!filesValid || !ranksValid)
            return false;

        fromX = fromFile - 'a';
        fromY = fromRank - '1';
        toX = toFile - 'a';
        toY = toRank - '1';
        return true;
    }

    private void RebuildBoardForCurrentPly()
    {
        if (!HasLoadedGame)
        {
            ShowEmptyAnalyzerState();
            return;
        }

        currentLastMove = null;

        if (!hasReplayData)
        {
            BuildBoardFromSavedPieces();
            UpdateAnalyzerTexts();

            if (autoAnalyzeOnReplayChange)
                AnalyzeCurrentPosition();

            return;
        }

        if (currentPlyIndex == totalPlyCount && currentSaveData.WaitingForPromotion && !IsInVariationMode)
        {
            BuildBoardFromSavedPieces();
            currentReplayTurn = currentSaveData.CurrentTurn;
            currentLastMove = RestoreSavedMove(currentSaveData.LastMovePlayed);
            UpdateAnalyzerTexts();

            if (autoAnalyzeOnReplayChange)
                AnalyzeCurrentPosition();

            return;
        }

        BuildBoardFromReplay();

        if (autoAnalyzeOnReplayChange)
            AnalyzeCurrentPosition();
        else
            UpdateEngineStatus("Engine ready. Press Analyze Position.");
    }

    private void BuildBoardFromReplay()
    {
        currentBoardState = new ChessBoardState();
        currentBoardState.InitializeStartingPosition();
        currentReplayTurn = PieceColor.White;

        for (int i = 0; i < currentPlyIndex; i++)
        {
            SavedMoveData savedMove = currentSaveData.PlayedMoves[i];
            MoveData restoredMove = RestoreSavedMove(savedMove);

            ChessRulesUtility.ApplyMoveToState(currentBoardState, restoredMove);
            ApplyPromotionIfNeeded(currentBoardState, savedMove);

            currentLastMove = restoredMove;
            currentReplayTurn = GetOpponentColor(currentReplayTurn);
        }

        ApplyVariationMovesToCurrentBoard();
        RenderCurrentBoardAndUpdateTexts();
    }

    private void BuildBoardFromSavedPieces()
    {
        currentBoardState = new ChessBoardState();
        currentBoardState.ClearBoard();

        if (currentSaveData?.Pieces != null)
        {
            foreach (SavedPieceData savedPiece in currentSaveData.Pieces)
            {
                PieceData piece = new PieceData(savedPiece.Type, savedPiece.Color)
                {
                    HasMoved = savedPiece.HasMoved
                };

                currentBoardState.SetPiece(savedPiece.X, savedPiece.Y, piece);
            }
        }

        currentReplayTurn = currentSaveData != null ? currentSaveData.CurrentTurn : PieceColor.White;
        currentLastMove = RestoreSavedMove(currentSaveData?.LastMovePlayed);

        ApplyVariationMovesToCurrentBoard();
        RenderCurrentBoardAndUpdateTexts();
    }

    private void ApplyVariationMovesToCurrentBoard()
    {
        if (!IsInVariationMode)
            return;

        foreach (MoveData variationMove in variationMoves)
        {
            MoveData cloned = CloneMove(variationMove);
            ChessRulesUtility.ApplyMoveToState(currentBoardState, cloned);

            if (cloned.IsPromotion)
            {
                PieceData piece = currentBoardState.GetPiece(cloned.ToX, cloned.ToY);
                if (piece != null)
                    piece.PromoteTo(cloned.PromotionPieceType ?? PieceType.Queen);
            }

            currentLastMove = cloned;
            currentReplayTurn = GetOpponentColor(currentReplayTurn);
        }
    }

    private void RenderCurrentBoardAndUpdateTexts()
    {
        boardUIManager.RenderPieces(currentBoardState);
        boardUIManager.ClearHighlights();
        UpdateAnalyzerTexts();
    }

    private void SelectPiece(int x, int y)
    {
        hasSelection = true;
        selectedX = x;
        selectedY = y;

        currentLegalMoves.Clear();
        currentLegalMoves.AddRange(
            ChessRulesUtility.GetLegalMovesForPiece(currentBoardState, x, y, currentLastMove)
        );

        boardUIManager.ClearHighlights();
        boardUIManager.HighlightSelectedCell(x, y);
        boardUIManager.HighlightMoveCells(currentLegalMoves);
    }

    private void ClearSelection()
    {
        hasSelection = false;
        selectedX = -1;
        selectedY = -1;
        currentLegalMoves.Clear();
        boardUIManager.ClearHighlights();
    }

    private MoveData FindMoveTo(int x, int y)
    {
        for (int i = 0; i < currentLegalMoves.Count; i++)
        {
            MoveData move = currentLegalMoves[i];
            if (move.ToX == x && move.ToY == y)
                return move;
        }

        return null;
    }

    private void ExecuteVariationMove(MoveData move)
    {
        if (move == null || currentBoardState == null)
            return;

        StopCurrentAnalysis();

        PieceData movingPiece = currentBoardState.GetPiece(move.FromX, move.FromY);
        if (movingPiece == null)
            return;

        ChessRulesUtility.ApplyMoveToState(currentBoardState, move);

        if (move.IsPromotion)
        {
            PieceData promotedPiece = currentBoardState.GetPiece(move.ToX, move.ToY);
            if (promotedPiece != null)
            {
                promotedPiece.PromoteTo(PieceType.Queen);
                move.SetPromotion(PieceType.Queen);
            }
        }

        if (!IsInVariationMode)
            variationStartPlyIndex = currentPlyIndex;

        MoveData storedMove = CloneMove(move);
        variationMoves.Add(storedMove);
        currentLastMove = CloneMove(storedMove);

        currentReplayTurn = GetOpponentColor(currentReplayTurn);

        boardUIManager.RenderPieces(currentBoardState);
        ClearSelection();

        RefreshMoveHistoryText();
        UpdateAnalyzerTexts();
    }

    private void UpdateVariationStatusText()
    {
        if (currentBoardState == null)
            return;

        bool inCheck = ChessRulesUtility.IsKingInCheck(currentBoardState, currentReplayTurn);
        bool hasAnyMove = ChessRulesUtility.HasAnyLegalMove(currentBoardState, currentReplayTurn, currentLastMove);

        if (inCheck && !hasAnyMove)
        {
            PieceColor winner = GetOpponentColor(currentReplayTurn);
            UpdateStatusText($"Variation: checkmate. {winner} wins.");
            return;
        }

        if (!inCheck && !hasAnyMove)
        {
            UpdateStatusText("Variation: stalemate.");
            return;
        }

        if (inCheck)
        {
            UpdateStatusText($"Variation mode. Check! Turn: {currentReplayTurn}");
            return;
        }

        UpdateStatusText($"Variation mode. Turn: {currentReplayTurn}");
    }

    private void ClearVariation()
    {
        variationMoves.Clear();
        variationStartPlyIndex = 0;
        RefreshMoveHistoryText();
    }

    private static void ApplyPromotionIfNeeded(ChessBoardState state, SavedMoveData move)
    {
        if (state == null || move == null || !move.HasPromotionPiece)
            return;

        PieceData piece = state.GetPiece(move.ToX, move.ToY);
        if (piece == null)
            return;

        piece.PromoteTo(move.PromotionPieceType);
    }

    private void UpdatePlayerTexts()
    {
        if (currentSaveData == null)
            return;

        string whiteName = string.IsNullOrWhiteSpace(currentSaveData.WhitePlayerName) ? "White" : currentSaveData.WhitePlayerName;
        string blackName = string.IsNullOrWhiteSpace(currentSaveData.BlackPlayerName) ? "Black" : currentSaveData.BlackPlayerName;

        SetPlayerTexts(whiteName, blackName);
    }

    private void SetPlayerTexts(string whiteName, string blackName)
    {
        if (whitePlayerNameText != null)
            whitePlayerNameText.text = whiteName;

        if (blackPlayerNameText != null)
            blackPlayerNameText.text = blackName;
    }

    private void RefreshMoveHistoryText()
    {
        if (moveHistoryText == null)
            return;

        StringBuilder builder = new StringBuilder();

        if (currentSaveData?.MoveHistoryLines != null)
        {
            for (int i = 0; i < currentSaveData.MoveHistoryLines.Count; i++)
            {
                builder.AppendLine(currentSaveData.MoveHistoryLines[i]);
            }
        }

        if (IsInVariationMode)
        {
            if (builder.Length > 0)
                builder.AppendLine();

            builder.AppendLine($"[Variation from ply {variationStartPlyIndex}/{totalPlyCount}]");

            for (int i = 0; i < variationMoves.Count; i++)
            {
                builder.Append(i + 1);
                builder.Append(". ");
                builder.AppendLine(FormatVariationMove(variationMoves[i]));
            }
        }

        SetMoveHistoryText(builder.ToString().TrimEnd('\r', '\n'));
    }

    private void SetMoveHistoryText(string text)
    {
        if (moveHistoryText != null)
            moveHistoryText.text = text;
    }

    private string FormatVariationMove(MoveData move)
    {
        if (move == null)
            return "-";

        if (move.MoveType == MoveType.CastleKingSide)
            return "O-O";

        if (move.MoveType == MoveType.CastleQueenSide)
            return "O-O-O";

        string from = GetSquareName(move.FromX, move.FromY);
        string to = GetSquareName(move.ToX, move.ToY);
        string separator = move.IsCapture ? "x" : "-";

        string text = $"{from}{separator}{to}";

        if (move.IsPromotion)
            text += "=Q";

        if (move.MoveType == MoveType.EnPassant)
            text += " e.p.";

        return text;
    }

    private void UpdateAnalyzerTexts()
    {
        if (currentSaveData == null)
        {
            ShowEmptyAnalyzerState();
            return;
        }

        UpdateReplayCounterText();
        currentFen = BuildFenForCurrentPosition();

        if (IsInVariationMode)
        {
            UpdateVariationStatusText();
            return;
        }

        if (!hasReplayData)
        {
            UpdateStatusText($"Analyzer: final position loaded. Turn: {currentReplayTurn}");
            return;
        }

        UpdateStatusText($"Analyzer: replay {currentPlyIndex}/{totalPlyCount}. Turn: {currentReplayTurn}");
    }

    private void UpdateReplayCounterText()
    {
        if (replayCounterText == null)
            return;

        if (IsInVariationMode)
        {
            replayCounterText.text = $"{currentPlyIndex}+{variationMoves.Count} / {totalPlyCount} (var)";
        }
        else if (hasReplayData)
        {
            replayCounterText.text = $"{currentPlyIndex} / {totalPlyCount}";
        }
        else
        {
            replayCounterText.text = "Final only";
        }
    }

    private string BuildFenForCurrentPosition()
    {
        if (currentBoardState == null)
            return string.Empty;

        StringBuilder fen = new StringBuilder();

        for (int y = 7; y >= 0; y--)
        {
            int emptyCount = 0;

            for (int x = 0; x < ChessBoardState.Size; x++)
            {
                PieceData piece = currentBoardState.GetPiece(x, y);

                if (piece == null)
                {
                    emptyCount++;
                    continue;
                }

                if (emptyCount > 0)
                {
                    fen.Append(emptyCount);
                    emptyCount = 0;
                }

                fen.Append(GetFenPieceChar(piece));
            }

            if (emptyCount > 0)
                fen.Append(emptyCount);

            if (y > 0)
                fen.Append('/');
        }

        fen.Append(' ');
        fen.Append(currentReplayTurn == PieceColor.White ? 'w' : 'b');

        string castlingRights = BuildCastlingRights();
        fen.Append(' ');
        fen.Append(string.IsNullOrWhiteSpace(castlingRights) ? "-" : castlingRights);

        string enPassantSquare = BuildEnPassantSquare();
        fen.Append(' ');
        fen.Append(string.IsNullOrWhiteSpace(enPassantSquare) ? "-" : enPassantSquare);

        fen.Append(" 0 ");
        fen.Append(Mathf.Max(1, (currentPlyIndex + variationMoves.Count) / 2 + 1));

        return fen.ToString();
    }

    private string BuildCastlingRights()
    {
        StringBuilder rights = new StringBuilder();

        AppendCastlingRightsForColor(
            rights,
            kingX: 4,
            kingY: 0,
            rookQueenX: 0,
            rookKingX: 7,
            color: PieceColor.White,
            kingSideSymbol: 'K',
            queenSideSymbol: 'Q');

        AppendCastlingRightsForColor(
            rights,
            kingX: 4,
            kingY: 7,
            rookQueenX: 0,
            rookKingX: 7,
            color: PieceColor.Black,
            kingSideSymbol: 'k',
            queenSideSymbol: 'q');

        return rights.ToString();
    }

    private void AppendCastlingRightsForColor(
        StringBuilder rights,
        int kingX,
        int kingY,
        int rookQueenX,
        int rookKingX,
        PieceColor color,
        char kingSideSymbol,
        char queenSideSymbol)
    {
        PieceData king = currentBoardState.GetPiece(kingX, kingY);
        if (king == null || king.Type != PieceType.King || king.Color != color || king.HasMoved)
            return;

        PieceData kingSideRook = currentBoardState.GetPiece(rookKingX, kingY);
        if (kingSideRook != null && kingSideRook.Type == PieceType.Rook && kingSideRook.Color == color && !kingSideRook.HasMoved)
            rights.Append(kingSideSymbol);

        PieceData queenSideRook = currentBoardState.GetPiece(rookQueenX, kingY);
        if (queenSideRook != null && queenSideRook.Type == PieceType.Rook && queenSideRook.Color == color && !queenSideRook.HasMoved)
            rights.Append(queenSideSymbol);
    }

    private string BuildEnPassantSquare()
    {
        if (currentLastMove == null)
            return "-";

        if (Mathf.Abs(currentLastMove.ToY - currentLastMove.FromY) != 2)
            return "-";

        PieceData movedPiece = currentBoardState.GetPiece(currentLastMove.ToX, currentLastMove.ToY);
        if (movedPiece == null || movedPiece.Type != PieceType.Pawn)
            return "-";

        int middleY = (currentLastMove.FromY + currentLastMove.ToY) / 2;
        return $"{GetFileLetter(currentLastMove.ToX)}{middleY + 1}";
    }

    private static char GetFenPieceChar(PieceData piece)
    {
        char c = piece.Type switch
        {
            PieceType.King => 'k',
            PieceType.Queen => 'q',
            PieceType.Rook => 'r',
            PieceType.Bishop => 'b',
            PieceType.Knight => 'n',
            PieceType.Pawn => 'p',
            _ => ' '
        };

        return piece.Color == PieceColor.White ? char.ToUpper(c) : c;
    }

    private string GetSquareName(int x, int y)
    {
        return $"{GetFileLetter(x)}{y + 1}";
    }

    private static string GetFileLetter(int x)
    {
        return ((char)('a' + x)).ToString();
    }

    private string ResolveEnginePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return string.Empty;

        return Path.IsPathRooted(rawPath)
            ? rawPath
            : Path.Combine(Application.streamingAssetsPath, rawPath);
    }

    private void ClearAnalysisTexts()
    {
        if (evaluationText != null)
            evaluationText.text = "Evaluation: -";

        if (bestMoveText != null)
            bestMoveText.text = "Best Move: -";

        if (depthText != null)
            depthText.text = "Depth: -";
    }

    private static MoveData RestoreSavedMove(SavedMoveData savedMove)
    {
        if (savedMove == null)
            return null;

        MoveData move = new MoveData(savedMove.FromX, savedMove.FromY, savedMove.ToX, savedMove.ToY, savedMove.MoveType);

        if (savedMove.HasCapturedPiece)
            move.SetCapturedPiece(savedMove.CapturedPieceType, savedMove.CapturedX, savedMove.CapturedY);

        if (savedMove.HasPromotionPiece)
            move.SetPromotion(savedMove.PromotionPieceType);

        if (savedMove.RookFromX >= 0 && savedMove.RookToX >= 0)
        {
            move.SetCastlingRookMove(
                savedMove.RookFromX,
                savedMove.RookFromY,
                savedMove.RookToX,
                savedMove.RookToY);
        }

        return move;
    }

    private static MoveData CloneMove(MoveData move)
    {
        if (move == null)
            return null;

        MoveData cloned = new MoveData(move.FromX, move.FromY, move.ToX, move.ToY, move.MoveType);

        if (move.CapturedPieceType.HasValue)
            cloned.SetCapturedPiece(move.CapturedPieceType.Value, move.CapturedX, move.CapturedY);

        if (move.PromotionPieceType.HasValue)
            cloned.SetPromotion(move.PromotionPieceType.Value);

        if (move.RookFromX >= 0 && move.RookToX >= 0)
        {
            cloned.SetCastlingRookMove(
                move.RookFromX,
                move.RookFromY,
                move.RookToX,
                move.RookToY);
        }

        return cloned;
    }

    private void SetReplayCounterText(string text)
    {
        if (replayCounterText != null)
            replayCounterText.text = text;
    }

    private void UpdateStatusText(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }

    private void UpdateEngineStatus(string text)
    {
        if (engineStatusText != null)
            engineStatusText.text = $"Engine: {text}";
    }

    private static PieceColor GetOpponentColor(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
}