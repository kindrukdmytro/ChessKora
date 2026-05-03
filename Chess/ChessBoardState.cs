public class ChessBoardState
{
    public const int Size = 8;

    private static readonly PieceType[] StartingBackRow =
    {
        PieceType.Rook,
        PieceType.Knight,
        PieceType.Bishop,
        PieceType.Queen,
        PieceType.King,
        PieceType.Bishop,
        PieceType.Knight,
        PieceType.Rook
    };

    private readonly PieceData[,] board = new PieceData[Size, Size];

    public void InitializeStartingPosition()
    {
        ClearBoard();

        for (int x = 0; x < Size; x++)
        {
            board[x, 0] = new PieceData(StartingBackRow[x], PieceColor.White);
            board[x, 1] = new PieceData(PieceType.Pawn, PieceColor.White);

            board[x, 6] = new PieceData(PieceType.Pawn, PieceColor.Black);
            board[x, 7] = new PieceData(StartingBackRow[x], PieceColor.Black);
        }
    }

    public void ClearBoard()
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                board[x, y] = null;
            }
        }
    }

    public bool IsInsideBoard(int x, int y)
    {
        return x >= 0 && x < Size && y >= 0 && y < Size;
    }

    public PieceData GetPiece(int x, int y)
    {
        return IsInsideBoard(x, y) ? board[x, y] : null;
    }

    public void SetPiece(int x, int y, PieceData piece)
    {
        if (!IsInsideBoard(x, y))
            return;

        board[x, y] = piece;
    }

    public void MovePiece(int fromX, int fromY, int toX, int toY)
    {
        if (!IsInsideBoard(fromX, fromY) || !IsInsideBoard(toX, toY))
            return;

        PieceData piece = board[fromX, fromY];
        if (piece == null)
            return;

        board[toX, toY] = piece;
        board[fromX, fromY] = null;
        piece.HasMoved = true;
    }

    public ChessBoardState Clone()
    {
        ChessBoardState clone = new ChessBoardState();

        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                PieceData piece = board[x, y];
                if (piece != null)
                    clone.board[x, y] = piece.Clone();
            }
        }

        return clone;
    }

    public bool TryFindKing(PieceColor color, out int kingX, out int kingY)
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                PieceData piece = board[x, y];
                if (piece != null && piece.Color == color && piece.Type == PieceType.King)
                {
                    kingX = x;
                    kingY = y;
                    return true;
                }
            }
        }

        kingX = -1;
        kingY = -1;
        return false;
    }
}