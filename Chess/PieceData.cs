public enum PieceType
{
    King,
    Queen,
    Rook,
    Bishop,
    Knight,
    Pawn
}

public enum PieceColor
{
    White,
    Black
}

public class PieceData
{
    public PieceType Type { get; private set; }
    public PieceColor Color { get; }
    public bool HasMoved { get; set; }

    public bool IsPawn => Type == PieceType.Pawn;

    public PieceData(PieceType type, PieceColor color)
    {
        Type = type;
        Color = color;
        HasMoved = false;
    }

    public PieceData Clone()
    {
        return new PieceData(Type, Color)
        {
            HasMoved = HasMoved
        };
    }

    public void PromoteTo(PieceType newType)
    {
        if (!IsPawn || !IsValidPromotionType(newType))
            return;

        Type = newType;
        HasMoved = true;
    }

    private static bool IsValidPromotionType(PieceType pieceType)
    {
        return pieceType == PieceType.Queen ||
               pieceType == PieceType.Rook ||
               pieceType == PieceType.Bishop ||
               pieceType == PieceType.Knight;
    }
}