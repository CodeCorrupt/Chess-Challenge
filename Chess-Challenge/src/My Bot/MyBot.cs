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
        bool playerIsWhite = board.IsWhiteToMove;
        Move[] moves = board.GetLegalMoves();
        Move bestMove = moves[0];
        float bestScore = float.MinValue;
        for (int i = 0; i < moves.Length; i++)
        {
            board.MakeMove(moves[i]);
            float val = AlphaBetaHard(board, 3, float.MinValue, float.MaxValue, playerIsWhite);
            board.UndoMove(moves[i]);
            if (val > bestScore)
            {
                bestScore = val;
                bestMove = moves[i];
            }
        }
        Console.WriteLine("Best score: " + bestScore + " for " + bestMove.ToString());
        return bestMove;
    }

    private float AlphaBetaHard(Board board, int depth, float a, float b, bool playerIsWhite)
    {
        if (depth == 0)
        {
            return GetBoardScore(board, playerIsWhite);
        }
        Move[] moves = board.GetLegalMoves();
        if (playerIsWhite == board.IsWhiteToMove)
        {
            float val = float.MinValue;
            for (int i = 0; i < moves.Length; i++)
            {
                board.MakeMove(moves[i]);
                val = Math.Max(val, AlphaBetaHard(board, depth - 1, a, b, playerIsWhite));
                board.UndoMove(moves[i]);
                if (val > b)
                {
                    break;
                }
                a = Math.Max(a, val);
            }
            return val;
        }
        else
        {
            float val = float.MaxValue;
            for (int i = 0; i < moves.Length; i++)
            {
                board.MakeMove(moves[i]);
                val = Math.Min(val, AlphaBetaHard(board, depth - 1, a, b, playerIsWhite));
                board.UndoMove(moves[i]);
                if (val < a)
                {
                    break;
                }
                b = Math.Min(b, val);
            }
            return val;
        }
    }

    private float GetBoardScore(Board board, bool playerIsWhite)
    {
        if (boardScores.ContainsKey(board.ZobristKey))
        {
            return boardScores[board.ZobristKey];
        }
        float score = 0;
        PieceList[] pieceLists = board.GetAllPieceLists();
        for (int i = 0; i < pieceLists.Length; i++)
        {
            int factor = -1;
            bool pieceIsWhite = i < 6;
            if (pieceIsWhite == playerIsWhite)
            {
                factor = 1;
            }
            float s = factor * GetPieceScore(pieceLists[i].TypeOfPieceInList) * pieceLists[i].Count;
            score += s;
        }
        boardScores[board.ZobristKey] = score;
        return score;
    }

    private float GetPieceScore(PieceType pieceType)
    {
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues = { 0, 100, 300, 300, 500, 900, 10000 };
        return pieceValues[(int)pieceType];
    }
}