using System;
using System.Collections.Generic;

[Serializable]
public class SavedGameData
{
    public string FileName;
    public long SavedAtTicks;
    public string SavedAtDisplay;

    public string WhitePlayerName;
    public string BlackPlayerName;

    public bool UseTimer;
    public float BaseTimeSeconds;
    public float IncrementSeconds;
    public float WhiteTimeRemaining;
    public float BlackTimeRemaining;

    public PieceColor CurrentTurn;
    public bool GameEnded;

    public bool WaitingForPromotion;
    public int PromotionX;
    public int PromotionY;
    public PieceColor PromotionColor;

    public int FullMoveNumber;
    public string StatusText;

    public List<string> MoveHistoryLines = new List<string>();
    public List<SavedPieceData> Pieces = new List<SavedPieceData>();
    public List<SavedMoveData> PlayedMoves = new List<SavedMoveData>();

    public SavedMoveData LastMovePlayed;
    public SavedMoveData PendingPromotionMove;

    public void EnsureCollections()
    {
        MoveHistoryLines ??= new List<string>();
        Pieces ??= new List<SavedPieceData>();
        PlayedMoves ??= new List<SavedMoveData>();
    }
}

[Serializable]
public class SavedPieceData
{
    public int X;
    public int Y;
    public PieceType Type;
    public PieceColor Color;
    public bool HasMoved;

    public SavedPieceData()
    {
    }

    public SavedPieceData(int x, int y, PieceType type, PieceColor color, bool hasMoved)
    {
        X = x;
        Y = y;
        Type = type;
        Color = color;
        HasMoved = hasMoved;
    }
}

[Serializable]
public class SavedMoveData
{
    public int FromX;
    public int FromY;
    public int ToX;
    public int ToY;

    public MoveType MoveType;

    public bool HasCapturedPiece;
    public PieceType CapturedPieceType;

    public bool HasPromotionPiece;
    public PieceType PromotionPieceType;

    public int CapturedX;
    public int CapturedY;

    public int RookFromX;
    public int RookFromY;
    public int RookToX;
    public int RookToY;

    public SavedMoveData()
    {
    }

    public SavedMoveData(int fromX, int fromY, int toX, int toY, MoveType moveType)
    {
        FromX = fromX;
        FromY = fromY;
        ToX = toX;
        ToY = toY;
        MoveType = moveType;
    }
}

[Serializable]
public class SaveFileInfo
{
    public string FileName;
    public string WhitePlayerName;
    public string BlackPlayerName;
    public string SavedAtDisplay;
    public long SavedAtTicks;
    public bool GameEnded;
}