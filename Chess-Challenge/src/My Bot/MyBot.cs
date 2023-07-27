using ChessChallenge.API;
using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

public class MyBot : IChessBot
{
    Dictionary<ulong, int> boardScores = new();
    Move bestRootMove;
    bool useQuiescence = true;
    int rootPositionsSearched;
    int quiescencePositionsSearched;

    private bool IsOutOfTime(Timer timer)
    {
        return timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30;
    }

    private int getManhattanDist(Square s1, Square s2)
    {
        return Math.Abs(s1.File - s2.File) + Math.Abs(s1.Rank - s2.Rank);
    }

    private int GetPieceScore(PieceType pieceType)
    {
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };
        return pieceValues[(int)pieceType];
    }

    private int GetMoveScore(Board board, Move move)
    {
        int score = 0;
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
    private int GetBoardScore(Board board, Boolean isWhite)
    {
        int score = 0;
        // Ger piece scores
        PieceList[] pieceLists = isWhite ? board.GetAllPieceLists().Take(6).ToArray() : board.GetAllPieceLists().Skip(6).ToArray();
        score += pieceLists.Sum(pieceList => GetPieceScore(pieceList.TypeOfPieceInList) * pieceList.Count);

        // Enemy King cornered score
        Square enemyKing = board.GetKingSquare(!isWhite);
        score += 2 * (int)getManhattanDist(enemyKing, new Square(3, 3));

        // Check if in check
        if (board.IsInCheck())
        {
            score += (board.IsWhiteToMove == isWhite) ? -10000 : 10000;
        }

        // Move pawns up
        PieceList pawnLists = board.GetPieceList(PieceType.Pawn, isWhite);
        score += pawnLists.Sum(pawn => isWhite ? pawn.Square.File : 7 - pawn.Square.File);

        return score;
    }

    private int GetBoardScore(Board board)
    {
        if (boardScores.ContainsKey(board.ZobristKey))
        {
            return boardScores[board.ZobristKey];
        }
        int score = 0;
        int whiteScore = GetBoardScore(board, true);
        int blackScore = GetBoardScore(board, false);
        int eval = whiteScore - blackScore;
        int perspective = board.IsWhiteToMove ? 1 : -1;
        score += perspective * eval;
        boardScores[board.ZobristKey] = score;
        return score;
    }

    // My understanding:
    // - Alpha: The best score I think I can possibly get from this position
    // - Beta: The best score my opponent will let me get.
    // - If Alpha is > Beta, then further up the tree the opponent will have stopped me from taking this path.
    private int Search(Board board, int a, int b, int depth, Timer timer, int ply)
    {
        bool quiescence = depth <= 0;
        if (quiescence)
        {
            int score = GetBoardScore(board);
            if (!useQuiescence) return score;
            if (score > b) return b;
            a = Math.Max(a, score);
        }
        Move[] orderedMoves = board.GetLegalMoves(quiescence).OrderByDescending(move => GetMoveScore(board, move)).ToArray();
        for (int i = 0; i < orderedMoves.Length; i++)
        {
            // If out of time, return the max score. Since the caller is negating this, it will
            // appear as the worst possible move and we will just take the best we found so far
            if (IsOutOfTime(timer)) return int.MaxValue - 1;
            if (quiescence)
            {
                quiescencePositionsSearched++;
            }
            else
            {
                rootPositionsSearched++;
            }
            board.MakeMove(orderedMoves[i]);
            int score = -Search(board, -b, -a, depth - 1, timer, ply + 1);
            board.UndoMove(orderedMoves[i]);
            if (score > a)
            {
                if (ply == 0)
                {
                    bestRootMove = orderedMoves[i];
                }
                a = score;
            }
            if (a >= b) return b;
        }
        return a;
    }

    public Move Think(Board board, Timer timer)
    {
        Move bestRootMoveFromCompletedSearch = Move.NullMove;
        for (int depth = 1; depth < 100; depth++)
        {
            bestRootMove = Move.NullMove;
            rootPositionsSearched = 0;
            quiescencePositionsSearched = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            // If you take the negative on int.MinValue it wraps around, so add one 
            int score = Search(board, int.MinValue + 1, int.MaxValue - 1, depth, timer, 0);
            watch.Stop();
            if (IsOutOfTime(timer) && bestRootMoveFromCompletedSearch != Move.NullMove)
            {
                break;
            }
            else
            {
                Console.WriteLine($"Depth {depth,2} - Took {watch.ElapsedMilliseconds,5} ms - Score: {score,11} - Best move: {bestRootMove} - Moves evaled: {rootPositionsSearched,8} - Quiescence evaled: {quiescencePositionsSearched,8}");
                bestRootMoveFromCompletedSearch = bestRootMove != Move.NullMove ? bestRootMove : board.GetLegalMoves().First();
            }
        }
        Console.WriteLine($"Final move: {bestRootMoveFromCompletedSearch} - ply {board.PlyCount}");
        return bestRootMoveFromCompletedSearch;
    }
}