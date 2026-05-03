using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ChessGameManager : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private BoardUIManager boardUIManager;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject promotionPanel;

    [Header("Timers")]
    [SerializeField] private TMP_Text whiteTimerText;
    [SerializeField] private TMP_Text blackTimerText;

    [Header("Player Names")]
    [SerializeField] private TMP_Text whitePlayerNameText;
    [SerializeField] private TMP_Text blackPlayerNameText;

    [Header("Move History")]
    [SerializeField] private TMP_Text moveHistoryText;

    private ChessBoardState boardState;
    private PieceColor currentTurn = PieceColor.White;

    private bool hasSelection;
    private int selectedX = -1;
    private int selectedY = -1;
    private readonly List<MoveData> currentLegalMoves = new List<MoveData>();

    private bool gameEnded;

    private bool waitingForPromotion;
    private int promotionX;
    private int promotionY;
    private PieceColor promotionColor;

    private MoveData lastMovePlayed;
    private MoveData pendingPromotionMove;

    private bool useTimer;
    private float baseTimeSeconds;
    private float incrementSeconds;
    private float whiteTimeRemaining;
    private float blackTimeRemaining;

    private readonly List<string> moveHistoryLines = new List<string>();
    private readonly List<MoveData> playedMoves = new List<MoveData>();
    private int fullMoveNumber = 1;

    private string loadedStatusText;

    private void Awake()
    {
        if (boardUIManager == null)
            boardUIManager = FindFirstObjectByType<BoardUIManager>();
    }

    private void Start()
    {
        if (boardUIManager == null)
        {
            Debug.LogError("BoardUIManager not found in scene.");
            return;
        }

        boardUIManager.GenerateBoard();
        InitializeGameState();

        boardUIManager.RenderPieces(boardState);
        boardUIManager.ClearHighlights();
        RefreshMoveHistoryUI();

        if (promotionPanel != null)
            promotionPanel.SetActive(waitingForPromotion);

        UpdatePlayerNameTexts();
        UpdateTimerTexts();
        RefreshStatusAfterInitialization();
    }

    private void Update()
    {
        TickGameClock();
    }

    private void InitializeGameState()
    {
        string pendingFileName = PendingGameLoad.Consume();

        if (!string.IsNullOrWhiteSpace(pendingFileName))
        {
            SavedGameData loadedGame = SaveSystem.LoadGame(pendingFileName);
            if (loadedGame != null)
            {
                ApplyLoadedGame(loadedGame);
                return;
            }

            Debug.LogWarning("Failed to load saved game. Starting a new game instead.");
        }

        StartNewGame();
    }

    private void StartNewGame()
    {
        boardState = new ChessBoardState();
        boardState.InitializeStartingPosition();

        currentTurn = PieceColor.White;
        gameEnded = false;

        waitingForPromotion = false;
        promotionX = 0;
        promotionY = 0;
        promotionColor = PieceColor.White;

        lastMovePlayed = null;
        pendingPromotionMove = null;

        ResetSelection();

        moveHistoryLines.Clear();
        playedMoves.Clear();
        fullMoveNumber = 1;

        useTimer = GameSettings.UseTimer;
        baseTimeSeconds = Mathf.Max(1f, GameSettings.BaseMinutes * 60f);
        incrementSeconds = Mathf.Max(0f, GameSettings.IncrementSeconds);
        whiteTimeRemaining = baseTimeSeconds;
        blackTimeRemaining = baseTimeSeconds;

        loadedStatusText = $"Turn: {currentTurn}";
    }

    private void RefreshStatusAfterInitialization()
    {
        if (waitingForPromotion)
        {
            UpdateStatusText($"{promotionColor} promotion: choose a piece");
            return;
        }

        if (!string.IsNullOrWhiteSpace(loadedStatusText))
        {
            UpdateStatusText(loadedStatusText);
            return;
        }

        EvaluateGameState();
    }

    public void SaveGameToFile()
    {
        if (boardState == null)
        {
            Debug.LogWarning("Cannot save game: boardState is null.");
            return;
        }

        SavedGameData saveData = BuildSaveData();
        string fileName = SaveSystem.SaveGame(saveData);

        if (!string.IsNullOrWhiteSpace(fileName))
            Debug.Log($"Game saved successfully: {fileName}");
    }

    public void HandleCellClick(int x, int y)
    {
        if (boardState == null || gameEnded || waitingForPromotion)
            return;

        PieceData clickedPiece = boardState.GetPiece(x, y);

        if (!hasSelection)
        {
            if (clickedPiece != null && clickedPiece.Color == currentTurn)
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
            ExecuteMove(selectedMove);
            return;
        }

        if (clickedPiece != null && clickedPiece.Color == currentTurn)
        {
            SelectPiece(x, y);
            return;
        }

        ClearSelection();
    }

    public void PromoteToQueen()
    {
        CompletePromotion(PieceType.Queen);
    }

    public void PromoteToRook()
    {
        CompletePromotion(PieceType.Rook);
    }

    public void PromoteToBishop()
    {
        CompletePromotion(PieceType.Bishop);
    }

    public void PromoteToKnight()
    {
        CompletePromotion(PieceType.Knight);
    }

    private SavedGameData BuildSaveData()
    {
        return new SavedGameData
        {
            WhitePlayerName = GetWhitePlayerName(),
            BlackPlayerName = GetBlackPlayerName(),

            UseTimer = useTimer,
            BaseTimeSeconds = baseTimeSeconds,
            IncrementSeconds = incrementSeconds,
            WhiteTimeRemaining = whiteTimeRemaining,
            BlackTimeRemaining = blackTimeRemaining,

            CurrentTurn = currentTurn,
            GameEnded = gameEnded,

            WaitingForPromotion = waitingForPromotion,
            PromotionX = promotionX,
            PromotionY = promotionY,
            PromotionColor = promotionColor,

            FullMoveNumber = fullMoveNumber,
            StatusText = statusText != null ? statusText.text : string.Empty,

            MoveHistoryLines = new List<string>(moveHistoryLines),
            Pieces = CollectSavedPieces(),
            PlayedMoves = CreateSavedMoveList(playedMoves),

            LastMovePlayed = CreateSavedMoveData(lastMovePlayed),
            PendingPromotionMove = CreateSavedMoveData(pendingPromotionMove)
        };
    }

    private List<SavedPieceData> CollectSavedPieces()
    {
        List<SavedPieceData> pieces = new List<SavedPieceData>();

        for (int x = 0; x < ChessBoardState.Size; x++)
        {
            for (int y = 0; y < ChessBoardState.Size; y++)
            {
                PieceData piece = boardState.GetPiece(x, y);
                if (piece == null)
                    continue;

                pieces.Add(new SavedPieceData
                {
                    X = x,
                    Y = y,
                    Type = piece.Type,
                    Color = piece.Color,
                    HasMoved = piece.HasMoved
                });
            }
        }

        return pieces;
    }

    private void ApplyLoadedGame(SavedGameData saveData)
    {
        boardState = new ChessBoardState();
        boardState.ClearBoard();

        RestorePieces(saveData.Pieces);

        currentTurn = saveData.CurrentTurn;
        gameEnded = saveData.GameEnded;

        waitingForPromotion = saveData.WaitingForPromotion;
        promotionX = saveData.PromotionX;
        promotionY = saveData.PromotionY;
        promotionColor = saveData.PromotionColor;

        useTimer = saveData.UseTimer;
        baseTimeSeconds = Mathf.Max(1f, saveData.BaseTimeSeconds);
        incrementSeconds = Mathf.Max(0f, saveData.IncrementSeconds);
        whiteTimeRemaining = Mathf.Max(0f, saveData.WhiteTimeRemaining);
        blackTimeRemaining = Mathf.Max(0f, saveData.BlackTimeRemaining);

        lastMovePlayed = RestoreMoveData(saveData.LastMovePlayed);
        pendingPromotionMove = RestoreMoveData(saveData.PendingPromotionMove);

        moveHistoryLines.Clear();
        if (saveData.MoveHistoryLines != null)
            moveHistoryLines.AddRange(saveData.MoveHistoryLines);

        playedMoves.Clear();
        if (saveData.PlayedMoves != null)
        {
            for (int i = 0; i < saveData.PlayedMoves.Count; i++)
            {
                MoveData restoredMove = RestoreMoveData(saveData.PlayedMoves[i]);
                if (restoredMove != null)
                    playedMoves.Add(restoredMove);
            }
        }

        fullMoveNumber = Mathf.Max(1, saveData.FullMoveNumber);
        loadedStatusText = saveData.StatusText;

        ResetSelection();
        ApplyLoadedSettingsToGameSettings(saveData);
    }

    private void RestorePieces(List<SavedPieceData> savedPieces)
    {
        if (savedPieces == null)
            return;

        for (int i = 0; i < savedPieces.Count; i++)
        {
            SavedPieceData savedPiece = savedPieces[i];

            PieceData piece = new PieceData(savedPiece.Type, savedPiece.Color)
            {
                HasMoved = savedPiece.HasMoved
            };

            boardState.SetPiece(savedPiece.X, savedPiece.Y, piece);
        }
    }

    private void ApplyLoadedSettingsToGameSettings(SavedGameData saveData)
    {
        GameSettings.WhitePlayerName = string.IsNullOrWhiteSpace(saveData.WhitePlayerName)
            ? "WhitePlayer"
            : saveData.WhitePlayerName;

        GameSettings.BlackPlayerName = string.IsNullOrWhiteSpace(saveData.BlackPlayerName)
            ? "BlackPlayer"
            : saveData.BlackPlayerName;

        GameSettings.UseTimer = useTimer;
        GameSettings.BaseMinutes = baseTimeSeconds / 60f;
        GameSettings.IncrementSeconds = incrementSeconds;
    }

    private List<SavedMoveData> CreateSavedMoveList(List<MoveData> moves)
    {
        List<SavedMoveData> result = new List<SavedMoveData>();

        if (moves == null)
            return result;

        for (int i = 0; i < moves.Count; i++)
        {
            SavedMoveData savedMove = CreateSavedMoveData(moves[i]);
            if (savedMove != null)
                result.Add(savedMove);
        }

        return result;
    }

    private SavedMoveData CreateSavedMoveData(MoveData move)
    {
        if (move == null)
            return null;

        SavedMoveData savedMove = new SavedMoveData
        {
            FromX = move.FromX,
            FromY = move.FromY,
            ToX = move.ToX,
            ToY = move.ToY,
            MoveType = move.MoveType,

            CapturedX = move.CapturedX,
            CapturedY = move.CapturedY,

            RookFromX = move.RookFromX,
            RookFromY = move.RookFromY,
            RookToX = move.RookToX,
            RookToY = move.RookToY
        };

        if (move.CapturedPieceType.HasValue)
        {
            savedMove.HasCapturedPiece = true;
            savedMove.CapturedPieceType = move.CapturedPieceType.Value;
        }

        if (move.PromotionPieceType.HasValue)
        {
            savedMove.HasPromotionPiece = true;
            savedMove.PromotionPieceType = move.PromotionPieceType.Value;
        }

        return savedMove;
    }

    private MoveData RestoreMoveData(SavedMoveData savedMove)
    {
        if (savedMove == null)
            return null;

        MoveData move = new MoveData(
            savedMove.FromX,
            savedMove.FromY,
            savedMove.ToX,
            savedMove.ToY,
            savedMove.MoveType
        );

        if (savedMove.HasCapturedPiece)
        {
            move.SetCapturedPiece(
                savedMove.CapturedPieceType,
                savedMove.CapturedX,
                savedMove.CapturedY
            );
        }

        if (savedMove.HasPromotionPiece)
            move.SetPromotion(savedMove.PromotionPieceType);

        if (savedMove.RookFromX >= 0 && savedMove.RookToX >= 0)
        {
            move.SetCastlingRookMove(
                savedMove.RookFromX,
                savedMove.RookFromY,
                savedMove.RookToX,
                savedMove.RookToY
            );
        }

        return move;
    }

    private void RegisterPlayedMove(MoveData move)
    {
        if (move == null)
            return;

        MoveData clonedMove = RestoreMoveData(CreateSavedMoveData(move));
        if (clonedMove != null)
            playedMoves.Add(clonedMove);
    }

    private void TickGameClock()
    {
        if (!useTimer || gameEnded || boardState == null)
            return;

        ref float currentTime = ref GetCurrentPlayerTimeRef();
        currentTime -= Time.deltaTime;

        if (currentTime <= 0f)
        {
            currentTime = 0f;
            HandleTimeout(currentTurn);
        }

        UpdateTimerTexts();
    }

    private ref float GetCurrentPlayerTimeRef()
    {
        if (currentTurn == PieceColor.White)
            return ref whiteTimeRemaining;

        return ref blackTimeRemaining;
    }

    private void HandleTimeout(PieceColor loser)
    {
        if (gameEnded)
            return;

        gameEnded = true;
        ClearSelection();

        PieceColor winner = GetOpponentColor(loser);
        UpdateTimerTexts();
        UpdateStatusText($"Time out! {winner} wins.");
        Debug.Log($"Time out! {winner} wins.");
    }

    private void UpdatePlayerNameTexts()
    {
        if (whitePlayerNameText != null)
            whitePlayerNameText.text = GetWhitePlayerName();

        if (blackPlayerNameText != null)
            blackPlayerNameText.text = GetBlackPlayerName();
    }

    private string GetWhitePlayerName()
    {
        return string.IsNullOrWhiteSpace(GameSettings.WhitePlayerName) ? "WhitePlayer" : GameSettings.WhitePlayerName;
    }

    private string GetBlackPlayerName()
    {
        return string.IsNullOrWhiteSpace(GameSettings.BlackPlayerName) ? "BlackPlayer" : GameSettings.BlackPlayerName;
    }

    private void SelectPiece(int x, int y)
    {
        hasSelection = true;
        selectedX = x;
        selectedY = y;

        currentLegalMoves.Clear();
        currentLegalMoves.AddRange(
            ChessRulesUtility.GetLegalMovesForPiece(boardState, x, y, lastMovePlayed)
        );

        boardUIManager.ClearHighlights();
        boardUIManager.HighlightSelectedCell(x, y);
        boardUIManager.HighlightMoveCells(currentLegalMoves);
    }

    private void ClearSelection()
    {
        ResetSelection();
        boardUIManager.ClearHighlights();
    }

    private void ResetSelection()
    {
        hasSelection = false;
        selectedX = -1;
        selectedY = -1;
        currentLegalMoves.Clear();
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

    private void ExecuteMove(MoveData move)
    {
        PieceData movingPiece = boardState.GetPiece(move.FromX, move.FromY);
        if (movingPiece == null)
            return;

        PieceType movingPieceType = movingPiece.Type;

        ChessRulesUtility.ApplyMoveToState(boardState, move);
        lastMovePlayed = move;

        boardUIManager.RenderPieces(boardState);
        ClearSelection();

        if (ShouldStartPromotion(move.ToX, move.ToY))
        {
            pendingPromotionMove = move;
            BeginPromotion(move.ToX, move.ToY);
            return;
        }

        FinalizeTurnAfterMove(movingPiece.Color, move, movingPieceType, null);
    }

    private void FinalizeTurnAfterMove(PieceColor movedColor, MoveData move, PieceType movingPieceType, PieceType? promotionResultType)
    {
        AddIncrement(movedColor);
        RegisterPlayedMove(move);

        currentTurn = GetOpponentColor(currentTurn);

        UpdateTimerTexts();
        AppendMoveToHistory(move, movedColor, movingPieceType, promotionResultType);
        EvaluateGameState();
    }

    private void AddIncrement(PieceColor movedColor)
    {
        if (!useTimer)
            return;

        if (movedColor == PieceColor.White)
            whiteTimeRemaining += incrementSeconds;
        else
            blackTimeRemaining += incrementSeconds;
    }

    private bool ShouldStartPromotion(int x, int y)
    {
        PieceData piece = boardState.GetPiece(x, y);
        if (piece == null || piece.Type != PieceType.Pawn)
            return false;

        return (piece.Color == PieceColor.White && y == 7) ||
               (piece.Color == PieceColor.Black && y == 0);
    }

    private void BeginPromotion(int x, int y)
    {
        PieceData piece = boardState.GetPiece(x, y);
        if (piece == null)
            return;

        waitingForPromotion = true;
        promotionX = x;
        promotionY = y;
        promotionColor = piece.Color;

        if (promotionPanel != null)
            promotionPanel.SetActive(true);

        UpdateStatusText($"{promotionColor} promotion: choose a piece");
    }

    private void CompletePromotion(PieceType newType)
    {
        if (!waitingForPromotion)
            return;

        PieceData piece = boardState.GetPiece(promotionX, promotionY);
        if (piece == null || piece.Type != PieceType.Pawn || piece.Color != promotionColor)
            return;

        piece.PromoteTo(newType);
        waitingForPromotion = false;

        if (promotionPanel != null)
            promotionPanel.SetActive(false);

        boardUIManager.RenderPieces(boardState);

        MoveData finishedPromotionMove = pendingPromotionMove;
        pendingPromotionMove = null;

        if (finishedPromotionMove != null)
        {
            finishedPromotionMove.SetPromotion(newType);
            lastMovePlayed = finishedPromotionMove;
        }

        FinalizeTurnAfterMove(promotionColor, finishedPromotionMove, PieceType.Pawn, newType);
    }

    private void AppendMoveToHistory(MoveData move, PieceColor movedColor, PieceType movingPieceType, PieceType? promotionResultType)
    {
        if (move == null)
            return;

        string notation = BuildMoveNotation(move, movingPieceType, promotionResultType) + GetCheckSuffixForCurrentPosition();

        if (movedColor == PieceColor.White)
        {
            moveHistoryLines.Add($"{fullMoveNumber}. {notation}");
        }
        else
        {
            if (moveHistoryLines.Count == 0)
                moveHistoryLines.Add($"{fullMoveNumber}... {notation}");
            else
                moveHistoryLines[moveHistoryLines.Count - 1] += $" {notation}";

            fullMoveNumber++;
        }

        RefreshMoveHistoryUI();
    }

    private string BuildMoveNotation(MoveData move, PieceType movingPieceType, PieceType? promotionResultType)
    {
        if (move.IsCastling)
        {
            if (move.MoveType == MoveType.CastleKingSide)
                return "O-O";

            if (move.MoveType == MoveType.CastleQueenSide)
                return "O-O-O";
        }

        string targetSquare = GetSquareName(move.ToX, move.ToY);

        if (movingPieceType == PieceType.Pawn)
        {
            string notation;

            if (move.MoveType == MoveType.EnPassant)
            {
                notation = $"{GetFileLetter(move.FromX)}x{targetSquare} e.p.";
            }
            else if (move.IsCapture)
            {
                notation = $"{GetFileLetter(move.FromX)}x{targetSquare}";
            }
            else
            {
                notation = targetSquare;
            }

            if (promotionResultType.HasValue)
                notation += "=" + GetPieceLetter(promotionResultType.Value);

            return notation;
        }

        string pieceLetter = GetPieceLetter(movingPieceType);
        string captureMark = move.IsCapture ? "x" : string.Empty;

        return $"{pieceLetter}{captureMark}{targetSquare}";
    }

    private string GetCheckSuffixForCurrentPosition()
    {
        bool inCheck = ChessRulesUtility.IsKingInCheck(boardState, currentTurn);
        bool hasAnyMove = ChessRulesUtility.HasAnyLegalMove(boardState, currentTurn, lastMovePlayed);

        if (inCheck && !hasAnyMove)
            return "#";

        if (inCheck)
            return "+";

        return string.Empty;
    }

    private void RefreshMoveHistoryUI()
    {
        if (moveHistoryText != null)
            moveHistoryText.text = string.Join("\n", moveHistoryLines);
    }

    private string GetSquareName(int x, int y)
    {
        return $"{GetFileLetter(x)}{y + 1}";
    }

    private string GetFileLetter(int x)
    {
        return ((char)('a' + x)).ToString();
    }

    private string GetPieceLetter(PieceType pieceType)
    {
        switch (pieceType)
        {
            case PieceType.King: return "K";
            case PieceType.Queen: return "Q";
            case PieceType.Rook: return "R";
            case PieceType.Bishop: return "B";
            case PieceType.Knight: return "N";
            default: return string.Empty;
        }
    }

    private void EvaluateGameState()
    {
        bool inCheck = ChessRulesUtility.IsKingInCheck(boardState, currentTurn);
        bool hasAnyMove = ChessRulesUtility.HasAnyLegalMove(boardState, currentTurn, lastMovePlayed);

        if (inCheck && !hasAnyMove)
        {
            gameEnded = true;
            PieceColor winner = GetOpponentColor(currentTurn);
            UpdateStatusText($"Checkmate! {winner} wins.");
            Debug.Log($"Checkmate! {winner} wins.");
            return;
        }

        if (!inCheck && !hasAnyMove)
        {
            gameEnded = true;
            UpdateStatusText("Stalemate! Draw.");
            Debug.Log("Stalemate! Draw.");
            return;
        }

        if (inCheck)
        {
            UpdateStatusText($"Check! Turn: {currentTurn}");
            Debug.Log($"Check! {currentTurn} is in check.");
            return;
        }

        UpdateStatusText($"Turn: {currentTurn}");
    }

    private void UpdateStatusText(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }

    private void UpdateTimerTexts()
    {
        if (!useTimer)
        {
            if (whiteTimerText != null)
                whiteTimerText.text = "∞";

            if (blackTimerText != null)
                blackTimerText.text = "∞";

            return;
        }

        if (whiteTimerText != null)
            whiteTimerText.text = FormatTime(whiteTimeRemaining);

        if (blackTimerText != null)
            blackTimerText.text = FormatTime(blackTimeRemaining);
    }

    private string FormatTime(float timeSeconds)
    {
        timeSeconds = Mathf.Max(0f, timeSeconds);

        int totalSeconds = Mathf.CeilToInt(timeSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return $"{minutes:00}:{seconds:00}";
    }

    private PieceColor GetOpponentColor(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
}