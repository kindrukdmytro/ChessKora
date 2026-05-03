using System.Collections.Generic;
using UnityEngine;

public static class ChessRulesUtility
{
    private static readonly int[,] KnightOffsets =
    {
        { 1, 2 }, { 2, 1 }, { 2, -1 }, { 1, -2 },
        { -1, -2 }, { -2, -1 }, { -2, 1 }, { -1, 2 }
    };

    private static readonly Vector2Int[] RookDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1)
    };

    private static readonly Vector2Int[] BishopDirections =
    {
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };

    private static readonly Vector2Int[] QueenDirections =
    {
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1, -1)
    };

    public static List<MoveData> GetLegalMovesForPiece(ChessBoardState state, int x, int y, MoveData lastMovePlayed)
    {
        List<MoveData> legalMoves = new List<MoveData>();

        PieceData piece = state.GetPiece(x, y);
        if (piece == null)
            return legalMoves;

        List<MoveData> pseudoMoves = GetPseudoLegalMovesForPiece(state, x, y, piece, lastMovePlayed);

        for (int i = 0; i < pseudoMoves.Count; i++)
        {
            MoveData move = pseudoMoves[i];
            ChessBoardState simulatedState = state.Clone();

            ApplyMoveToState(simulatedState, move);

            if (!IsKingInCheck(simulatedState, piece.Color))
                legalMoves.Add(move);
        }

        return legalMoves;
    }

    public static bool HasAnyLegalMove(ChessBoardState state, PieceColor color, MoveData lastMovePlayed)
    {
        for (int x = 0; x < ChessBoardState.Size; x++)
        {
            for (int y = 0; y < ChessBoardState.Size; y++)
            {
                PieceData piece = state.GetPiece(x, y);
                if (piece == null || piece.Color != color)
                    continue;

                if (GetLegalMovesForPiece(state, x, y, lastMovePlayed).Count > 0)
                    return true;
            }
        }

        return false;
    }

    public static bool IsKingInCheck(ChessBoardState state, PieceColor kingColor)
    {
        if (!state.TryFindKing(kingColor, out int kingX, out int kingY))
            return false;

        return IsSquareAttacked(state, kingX, kingY, GetOpponentColor(kingColor));
    }

    public static void ApplyMoveToState(ChessBoardState state, MoveData move)
    {
        if (state == null || move == null)
            return;

        switch (move.MoveType)
        {
            case MoveType.EnPassant:
                state.SetPiece(move.CapturedX, move.CapturedY, null);
                state.MovePiece(move.FromX, move.FromY, move.ToX, move.ToY);
                return;

            case MoveType.CastleKingSide:
            case MoveType.CastleQueenSide:
                state.MovePiece(move.FromX, move.FromY, move.ToX, move.ToY);
                state.MovePiece(move.RookFromX, move.RookFromY, move.RookToX, move.RookToY);
                return;

            default:
                state.MovePiece(move.FromX, move.FromY, move.ToX, move.ToY);
                return;
        }
    }

    private static List<MoveData> GetPseudoLegalMovesForPiece(ChessBoardState state, int x, int y, PieceData piece, MoveData lastMovePlayed)
    {
        List<MoveData> moves = new List<MoveData>();

        switch (piece.Type)
        {
            case PieceType.Pawn:
                AddPawnMoves(state, moves, x, y, piece, lastMovePlayed);
                break;

            case PieceType.Rook:
                AddDirectionalMoves(state, moves, x, y, RookDirections);
                break;

            case PieceType.Bishop:
                AddDirectionalMoves(state, moves, x, y, BishopDirections);
                break;

            case PieceType.Queen:
                AddDirectionalMoves(state, moves, x, y, QueenDirections);
                break;

            case PieceType.Knight:
                AddKnightMoves(state, moves, x, y, piece.Color);
                break;

            case PieceType.King:
                AddKingMoves(state, moves, x, y, piece.Color);
                AddCastlingMoves(state, moves, x, y, piece);
                break;
        }

        return moves;
    }

    private static void AddPawnMoves(ChessBoardState state, List<MoveData> moves, int x, int y, PieceData piece, MoveData lastMovePlayed)
    {
        int direction = piece.Color == PieceColor.White ? 1 : -1;
        int startRow = piece.Color == PieceColor.White ? 1 : 6;
        int promotionRow = piece.Color == PieceColor.White ? 7 : 0;

        int oneStepY = y + direction;
        if (state.IsInsideBoard(x, oneStepY) && state.GetPiece(x, oneStepY) == null)
        {
            MoveType moveType = oneStepY == promotionRow ? MoveType.Promotion : MoveType.Normal;
            moves.Add(new MoveData(x, y, x, oneStepY, moveType));

            int twoStepY = y + (2 * direction);
            if (y == startRow && state.IsInsideBoard(x, twoStepY) && state.GetPiece(x, twoStepY) == null)
                moves.Add(new MoveData(x, y, x, twoStepY, MoveType.Normal));
        }

        AddPawnCaptureMove(state, moves, x, y, x - 1, y + direction, piece.Color, promotionRow);
        AddPawnCaptureMove(state, moves, x, y, x + 1, y + direction, piece.Color, promotionRow);
        AddEnPassantMoves(state, moves, x, y, piece, lastMovePlayed);
    }

    private static void AddPawnCaptureMove(ChessBoardState state, List<MoveData> moves, int fromX, int fromY, int toX, int toY, PieceColor movingColor, int promotionRow)
    {
        if (!state.IsInsideBoard(toX, toY))
            return;

        PieceData targetPiece = state.GetPiece(toX, toY);
        if (targetPiece == null || targetPiece.Color == movingColor || targetPiece.Type == PieceType.King)
            return;

        MoveType captureType = toY == promotionRow ? MoveType.PromotionCapture : MoveType.Capture;

        MoveData captureMove = new MoveData(fromX, fromY, toX, toY, captureType)
            .SetCapturedPiece(targetPiece.Type, toX, toY);

        moves.Add(captureMove);
    }

    private static void AddEnPassantMoves(ChessBoardState state, List<MoveData> moves, int x, int y, PieceData piece, MoveData lastMovePlayed)
    {
        if (lastMovePlayed == null)
            return;

        PieceData lastMovedPiece = state.GetPiece(lastMovePlayed.ToX, lastMovePlayed.ToY);
        if (lastMovedPiece == null ||
            lastMovedPiece.Type != PieceType.Pawn ||
            lastMovedPiece.Color == piece.Color)
        {
            return;
        }

        if (Mathf.Abs(lastMovePlayed.ToY - lastMovePlayed.FromY) != 2)
            return;

        if (lastMovePlayed.ToY != y)
            return;

        if (Mathf.Abs(lastMovePlayed.ToX - x) != 1)
            return;

        int direction = piece.Color == PieceColor.White ? 1 : -1;
        int targetX = lastMovePlayed.ToX;
        int targetY = y + direction;

        if (!state.IsInsideBoard(targetX, targetY))
            return;

        if (state.GetPiece(targetX, targetY) != null)
            return;

        if (state.GetPiece(targetX, y) == null)
            return;

        moves.Add(
            new MoveData(x, y, targetX, targetY, MoveType.EnPassant)
                .SetCapturedPiece(PieceType.Pawn, targetX, y)
        );
    }

    private static void AddKnightMoves(ChessBoardState state, List<MoveData> moves, int x, int y, PieceColor movingColor)
    {
        for (int i = 0; i < KnightOffsets.GetLength(0); i++)
        {
            int targetX = x + KnightOffsets[i, 0];
            int targetY = y + KnightOffsets[i, 1];
            TryAddSingleMove(state, moves, x, y, targetX, targetY, movingColor);
        }
    }

    private static void AddKingMoves(ChessBoardState state, List<MoveData> moves, int x, int y, PieceColor movingColor)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                TryAddSingleMove(state, moves, x, y, x + dx, y + dy, movingColor);
            }
        }
    }

    private static void AddCastlingMoves(ChessBoardState state, List<MoveData> moves, int kingX, int kingY, PieceData king)
    {
        if (king == null || king.Type != PieceType.King || king.HasMoved)
            return;

        PieceColor enemyColor = GetOpponentColor(king.Color);
        if (IsKingInCheck(state, king.Color))
            return;

        TryAddCastle(state, moves, kingX, kingY, king, enemyColor, isKingSide: true);
        TryAddCastle(state, moves, kingX, kingY, king, enemyColor, isKingSide: false);
    }

    private static void TryAddCastle(ChessBoardState state, List<MoveData> moves, int kingX, int kingY, PieceData king, PieceColor enemyColor, bool isKingSide)
    {
        int rookX = isKingSide ? 7 : 0;
        int step = isKingSide ? 1 : -1;

        PieceData rook = state.GetPiece(rookX, kingY);
        if (rook == null || rook.Color != king.Color || rook.Type != PieceType.Rook || rook.HasMoved)
            return;

        int startX = isKingSide ? kingX + 1 : rookX + 1;
        int endX = isKingSide ? rookX - 1 : kingX - 1;

        for (int x = startX; x <= endX; x++)
        {
            if (state.GetPiece(x, kingY) != null)
                return;
        }

        if (IsSquareAttacked(state, kingX + step, kingY, enemyColor))
            return;

        if (IsSquareAttacked(state, kingX + (2 * step), kingY, enemyColor))
            return;

        MoveData castleMove = new MoveData(
                kingX,
                kingY,
                kingX + (2 * step),
                kingY,
                isKingSide ? MoveType.CastleKingSide : MoveType.CastleQueenSide)
            .SetCastlingRookMove(
                rookX,
                kingY,
                kingX + step,
                kingY);

        moves.Add(castleMove);
    }

    private static void AddDirectionalMoves(ChessBoardState state, List<MoveData> moves, int startX, int startY, Vector2Int[] directions)
    {
        PieceData piece = state.GetPiece(startX, startY);
        if (piece == null)
            return;

        for (int i = 0; i < directions.Length; i++)
            AddDirectionalMoves(state, moves, startX, startY, directions[i].x, directions[i].y, piece.Color);
    }

    private static void AddDirectionalMoves(ChessBoardState state, List<MoveData> moves, int startX, int startY, int dx, int dy, PieceColor movingColor)
    {
        int x = startX + dx;
        int y = startY + dy;

        while (state.IsInsideBoard(x, y))
        {
            PieceData targetPiece = state.GetPiece(x, y);

            if (targetPiece == null)
            {
                moves.Add(new MoveData(startX, startY, x, y, MoveType.Normal));
            }
            else
            {
                if (targetPiece.Color != movingColor && targetPiece.Type != PieceType.King)
                {
                    moves.Add(
                        new MoveData(startX, startY, x, y, MoveType.Capture)
                            .SetCapturedPiece(targetPiece.Type, x, y)
                    );
                }

                break;
            }

            x += dx;
            y += dy;
        }
    }

    private static void TryAddSingleMove(ChessBoardState state, List<MoveData> moves, int fromX, int fromY, int toX, int toY, PieceColor movingColor)
    {
        if (!state.IsInsideBoard(toX, toY))
            return;

        PieceData targetPiece = state.GetPiece(toX, toY);

        if (targetPiece == null)
        {
            moves.Add(new MoveData(fromX, fromY, toX, toY, MoveType.Normal));
            return;
        }

        if (targetPiece.Color != movingColor && targetPiece.Type != PieceType.King)
        {
            moves.Add(
                new MoveData(fromX, fromY, toX, toY, MoveType.Capture)
                    .SetCapturedPiece(targetPiece.Type, toX, toY)
            );
        }
    }

    private static bool IsSquareAttacked(ChessBoardState state, int targetX, int targetY, PieceColor attackerColor)
    {
        return IsAttackedByPawn(state, targetX, targetY, attackerColor) ||
               IsAttackedByKnight(state, targetX, targetY, attackerColor) ||
               IsAttackedByKing(state, targetX, targetY, attackerColor) ||
               IsAttackedBySlidingPieces(state, targetX, targetY, attackerColor);
    }

    private static bool IsAttackedByPawn(ChessBoardState state, int targetX, int targetY, PieceColor attackerColor)
    {
        int sourceY = attackerColor == PieceColor.White ? targetY - 1 : targetY + 1;

        return IsSpecificAttackerAt(state, targetX - 1, sourceY, attackerColor, PieceType.Pawn) ||
               IsSpecificAttackerAt(state, targetX + 1, sourceY, attackerColor, PieceType.Pawn);
    }

    private static bool IsAttackedByKnight(ChessBoardState state, int targetX, int targetY, PieceColor attackerColor)
    {
        for (int i = 0; i < KnightOffsets.GetLength(0); i++)
        {
            if (IsSpecificAttackerAt(
                    state,
                    targetX + KnightOffsets[i, 0],
                    targetY + KnightOffsets[i, 1],
                    attackerColor,
                    PieceType.Knight))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAttackedByKing(ChessBoardState state, int targetX, int targetY, PieceColor attackerColor)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                if (IsSpecificAttackerAt(state, targetX + dx, targetY + dy, attackerColor, PieceType.King))
                    return true;
            }
        }

        return false;
    }

    private static bool IsAttackedBySlidingPieces(ChessBoardState state, int targetX, int targetY, PieceColor attackerColor)
    {
        return IsAttackedInDirection(state, targetX, targetY, 1, 0, attackerColor, PieceType.Rook, PieceType.Queen) ||
               IsAttackedInDirection(state, targetX, targetY, -1, 0, attackerColor, PieceType.Rook, PieceType.Queen) ||
               IsAttackedInDirection(state, targetX, targetY, 0, 1, attackerColor, PieceType.Rook, PieceType.Queen) ||
               IsAttackedInDirection(state, targetX, targetY, 0, -1, attackerColor, PieceType.Rook, PieceType.Queen) ||
               IsAttackedInDirection(state, targetX, targetY, 1, 1, attackerColor, PieceType.Bishop, PieceType.Queen) ||
               IsAttackedInDirection(state, targetX, targetY, 1, -1, attackerColor, PieceType.Bishop, PieceType.Queen) ||
               IsAttackedInDirection(state, targetX, targetY, -1, 1, attackerColor, PieceType.Bishop, PieceType.Queen) ||
               IsAttackedInDirection(state, targetX, targetY, -1, -1, attackerColor, PieceType.Bishop, PieceType.Queen);
    }

    private static bool IsSpecificAttackerAt(ChessBoardState state, int x, int y, PieceColor attackerColor, PieceType pieceType)
    {
        if (!state.IsInsideBoard(x, y))
            return false;

        PieceData piece = state.GetPiece(x, y);
        return piece != null && piece.Color == attackerColor && piece.Type == pieceType;
    }

    private static bool IsAttackedInDirection(ChessBoardState state, int startX, int startY, int dx, int dy, PieceColor attackerColor, PieceType primaryType, PieceType secondaryType)
    {
        int x = startX + dx;
        int y = startY + dy;

        while (state.IsInsideBoard(x, y))
        {
            PieceData piece = state.GetPiece(x, y);

            if (piece == null)
            {
                x += dx;
                y += dy;
                continue;
            }

            if (piece.Color != attackerColor)
                return false;

            return piece.Type == primaryType || piece.Type == secondaryType;
        }

        return false;
    }

    private static PieceColor GetOpponentColor(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
}