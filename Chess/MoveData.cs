public enum MoveType
{
    Normal,
    Capture,
    EnPassant,
    CastleKingSide,
    CastleQueenSide,
    Promotion,
    PromotionCapture
}

public class MoveData
{
    public int FromX { get; }
    public int FromY { get; }
    public int ToX { get; }
    public int ToY { get; }

    public MoveType MoveType { get; }

    public PieceType? CapturedPieceType { get; private set; }
    public PieceType? PromotionPieceType { get; private set; }

    public int CapturedX { get; private set; }
    public int CapturedY { get; private set; }

    public int RookFromX { get; private set; }
    public int RookFromY { get; private set; }
    public int RookToX { get; private set; }
    public int RookToY { get; private set; }

    public bool IsCapture =>
        MoveType == MoveType.Capture ||
        MoveType == MoveType.EnPassant ||
        MoveType == MoveType.PromotionCapture;

    public bool IsPromotion =>
        MoveType == MoveType.Promotion ||
        MoveType == MoveType.PromotionCapture;

    public bool IsCastling =>
        MoveType == MoveType.CastleKingSide ||
        MoveType == MoveType.CastleQueenSide;

    public bool HasCapturedPiece => CapturedPieceType.HasValue;
    public bool HasPromotionPiece => PromotionPieceType.HasValue;

    public MoveData(int fromX, int fromY, int toX, int toY, MoveType moveType = MoveType.Normal)
    {
        FromX = fromX;
        FromY = fromY;
        ToX = toX;
        ToY = toY;
        MoveType = moveType;

        CapturedX = toX;
        CapturedY = toY;

        RookFromX = -1;
        RookFromY = -1;
        RookToX = -1;
        RookToY = -1;
    }

    public MoveData SetCapturedPiece(PieceType capturedPieceType, int capturedX, int capturedY)
    {
        CapturedPieceType = capturedPieceType;
        CapturedX = capturedX;
        CapturedY = capturedY;
        return this;
    }

    public MoveData SetPromotion(PieceType promotionPieceType)
    {
        PromotionPieceType = promotionPieceType;
        return this;
    }

    public MoveData SetCastlingRookMove(int rookFromX, int rookFromY, int rookToX, int rookToY)
    {
        RookFromX = rookFromX;
        RookFromY = rookFromY;
        RookToX = rookToX;
        RookToY = rookToY;
        return this;
    }
}