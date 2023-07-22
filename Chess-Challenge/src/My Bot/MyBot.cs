using ChessChallenge.API;
using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

public class MyBot : IChessBot
{
    Dictionary<ulong, float> boardScores = new();
    public Move Think(Board board, Timer timer)
    {
        Move bestMove = SearchRoot(board, 5);
        return bestMove;
    }

    private Move SearchRoot(Board board, int depth)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Move[] moves = board.GetLegalMoves();
        Move bestMove = moves[0];
        float bestScore = float.MinValue;
        for (int i = 0; i < moves.Length; i++)
        {
            board.MakeMove(moves[i]);
            float val = -AlphaBetaNegamax(board, depth - 1, float.MinValue, -bestScore);
            board.UndoMove(moves[i]);
            if (val > bestScore)
            {
                bestScore = val;
                bestMove = moves[i];
            }
        }
        watch.Stop();
        Console.WriteLine($"Took {watch.ElapsedMilliseconds,5} ms for depth {depth,2} - Best score: {bestScore}");
        return bestMove;
    }
    private float AlphaBetaNegamax(Board board, int depth, float a, float b)
    {
        if (depth == 0)
        {
            return GetBoardScore(board);
        }
        Move[] allMoves = board.GetLegalMoves();
        if (allMoves.Length == 0)
        {
            if (board.IsInCheck())
            {
                return float.MinValue;
            }
            return 0;
        }
        Move[] orderedMoves = allMoves.OrderByDescending(move => GetMoveScore(board, move)).ToArray();
        for (int i = 0; i < orderedMoves.Length; i++)
        {
            board.MakeMove(orderedMoves[i]);
            float val = -AlphaBetaNegamax(board, depth - 1, -b, -a);
            board.UndoMove(orderedMoves[i]);
            if (val >= b)
            {
                return b;
            }
            a = Math.Max(val, a);
        }
        return a;
    }

    private float GetMoveScore(Board board, Move move)
    {
        float score = 0;
        if (move.IsCapture)
        {
            // Prioritize capturing higher value pieces with lower value pieces
            score += 10 * GetPieceScore(move.CapturePieceType) - GetPieceScore(move.MovePieceType);
        }
        if (move.IsPromotion)
        {
            score += GetPieceScore(move.PromotionPieceType);
        }
        return score;
    }
    // Score from the perspective of the player who's turn it is
    private float GetBoardScore(Board board)
    {
        if (boardScores.ContainsKey(board.ZobristKey))
        {
            return boardScores[board.ZobristKey];
        }
        float score = 0;
        float whiteScore = GetBoardScore(board, true);
        float blackScore = GetBoardScore(board, false);
        float eval = whiteScore - blackScore;
        int perspective = board.IsWhiteToMove ? 1 : -1;
        score += perspective * eval;
        boardScores[board.ZobristKey] = score;
        return score;
    }

    private float GetBoardScore(Board board, Boolean isWhite)
    {
        float score = 0;
        // Ger piece scores
        PieceList[] pieceLists = isWhite ? board.GetAllPieceLists().Take(6).ToArray() : board.GetAllPieceLists().Skip(6).ToArray();
        score += pieceLists.Sum(pieceList => GetPieceScore(pieceList.TypeOfPieceInList) * pieceList.Count);

        // Enemy King cornered score
        Square enemyKing = board.GetKingSquare(!isWhite);
        score += 2 * (float)getDist(enemyKing, new Square(3, 3));

        return score;
    }

    private double getDist(Square s1, Square s2)
    {
        return Math.Sqrt(Math.Pow(s1.File - s2.File, 2) + Math.Pow(s1.Rank - s2.Rank, 2));
    }

    private float GetPieceScore(PieceType pieceType)
    {
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };
        return pieceValues[(int)pieceType];
    }
}