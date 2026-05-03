using UnityEngine;

public class PieceSpriteLibrary : MonoBehaviour
{
    [Header("White Pieces")]
    [SerializeField] private Sprite whiteKing;
    [SerializeField] private Sprite whiteQueen;
    [SerializeField] private Sprite whiteRook;
    [SerializeField] private Sprite whiteBishop;
    [SerializeField] private Sprite whiteKnight;
    [SerializeField] private Sprite whitePawn;

    [Header("Black Pieces")]
    [SerializeField] private Sprite blackKing;
    [SerializeField] private Sprite blackQueen;
    [SerializeField] private Sprite blackRook;
    [SerializeField] private Sprite blackBishop;
    [SerializeField] private Sprite blackKnight;
    [SerializeField] private Sprite blackPawn;

    public Sprite GetSprite(PieceColor color, PieceType type)
    {
        if (color == PieceColor.White)
        {
            switch (type)
            {
                case PieceType.King: return whiteKing;
                case PieceType.Queen: return whiteQueen;
                case PieceType.Rook: return whiteRook;
                case PieceType.Bishop: return whiteBishop;
                case PieceType.Knight: return whiteKnight;
                case PieceType.Pawn: return whitePawn;
            }
        }
        else
        {
            switch (type)
            {
                case PieceType.King: return blackKing;
                case PieceType.Queen: return blackQueen;
                case PieceType.Rook: return blackRook;
                case PieceType.Bishop: return blackBishop;
                case PieceType.Knight: return blackKnight;
                case PieceType.Pawn: return blackPawn;
            }
        }

        return null;
    }
}