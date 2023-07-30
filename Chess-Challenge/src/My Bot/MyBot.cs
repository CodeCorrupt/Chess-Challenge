using ChessChallenge.API;
using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

public class MyBot : IChessBot
{
    Move bestRootMove;
    bool useQuiescence = true;
    int rootPositionsSearched;
    int quiescencePositionsSearched;
    int totalMemoHits;

    enum memoType { Exact, Lower, Upper };
    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 0, 100, 310, 330, 500, 1000, 10000 };

    struct MemoEntry
    {
        public ulong key;
        public int depth, score;
        public Move move;
        public memoType type;
        public MemoEntry(ulong _key, Move _move, int _depth, int _score, memoType _type)
        {
            key = _key;
            move = _move;
            depth = _depth;
            score = _score;
            type = _type;
        }
    }
    const int numMemoEntries = (1 << 20);
    MemoEntry[] memoTable = new MemoEntry[numMemoEntries];
    private bool IsOutOfTime(Timer timer)
    {
        return timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30;
    }

    private int GetMoveScore(Move move, Board board, MemoEntry memo)
    {
        if (memo.move == move) return 1000000;
        if (move.IsCapture) return 100 * (int)move.CapturePieceType - (int)move.MovePieceType;
        return 0;
    }
    /////////////////////////////////////////////
    // COPIED EVAL
    ulong[] psts = { 657614902731556116, 420894446315227099, 384592972471695068, 312245244820264086, 364876803783607569, 366006824779723922, 366006826859316500, 786039115310605588, 421220596516513823, 366011295806342421, 366006826859316436, 366006896669578452, 162218943720801556, 440575073001255824, 657087419459913430, 402634039558223453, 347425219986941203, 365698755348489557, 311382605788951956, 147850316371514514, 329107007234708689, 402598430990222677, 402611905376114006, 329415149680141460, 257053881053295759, 291134268204721362, 492947507967247313, 367159395376767958, 384021229732455700, 384307098409076181, 402035762391246293, 328847661003244824, 365712019230110867, 366002427738801364, 384307168185238804, 347996828560606484, 329692156834174227, 365439338182165780, 386018218798040211, 456959123538409047, 347157285952386452, 365711880701965780, 365997890021704981, 221896035722130452, 384289231362147538, 384307167128540502, 366006826859320596, 366006826876093716, 366002360093332756, 366006824694793492, 347992428333053139, 457508666683233428, 329723156783776785, 329401687190893908, 366002356855326100, 366288301819245844, 329978030930875600, 420621693221156179, 422042614449657239, 384602117564867863, 419505151144195476, 366274972473194070, 329406075454444949, 275354286769374224, 366855645423297932, 329991151972070674, 311105941360174354, 256772197720318995, 365993560693875923, 258219435335676691, 383730812414424149, 384601907111998612, 401758895947998613, 420612834953622999, 402607438610388375, 329978099633296596, 67159620133902 };
    int[] piecePhase = { 0, 0, 1, 1, 2, 4, 0 };
    public int getPstVal(int psq)
    {
        return (int)(((psts[psq / 10] >> (6 * (psq % 10))) & 63) - 20) * 8;
    }

    public int Evaluate(Board board)
    {
        int mg = 0, eg = 0, phase = 0;

        foreach (bool stm in new[] { true, false })
        {
            for (var p = PieceType.Pawn; p <= PieceType.King; p++)
            {
                int piece = (int)p, ind;
                ulong mask = board.GetPieceBitboard(p, stm);
                while (mask != 0)
                {
                    phase += piecePhase[piece];
                    ind = 128 * (piece - 1) + BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (stm ? 56 : 0);
                    mg += getPstVal(ind) + pieceValues[piece];
                    eg += getPstVal(ind + 64) + pieceValues[piece];
                }
            }

            mg = -mg;
            eg = -eg;
        }

        return (mg * phase + eg * (24 - phase)) / 24 * (board.IsWhiteToMove ? 1 : -1);
    }
    // END COPIED EVAL
    /////////////////////////////////////////////

    // My understanding:
    // - Alpha: The best score I think I can possibly get from this position
    // - Beta: The best score my opponent will let me get.
    // - If Alpha is > Beta, then further up the tree the opponent will have stopped me from taking this path.
    private int Search(Board board, int a, int b, int depth, Timer timer, int ply)
    {
        ulong zob = board.ZobristKey;
        bool quiescence = depth <= 0;
        int bestScore = -30000;

        if (ply > 0 && board.IsRepeatedPosition())
            return 0;

        MemoEntry memo = memoTable[zob % numMemoEntries];
        if (ply > 0 && memo.key == zob && memo.depth >= depth)
        {
            totalMemoHits++;
            if (memo.type == memoType.Exact) return memo.score;
            if (memo.type == memoType.Lower && memo.score >= b) return memo.score;
            if (memo.type == memoType.Upper && memo.score <= a) return memo.score;
            totalMemoHits--;
        }

        if (quiescence)
        {
            // int score = GetBoardScore(board);
            int score = Evaluate(board);
            if (!useQuiescence) return score;
            bestScore = score;
            if (score >= b) return score;
            a = Math.Max(a, score);
        }
        Move[] orderedMoves = board.GetLegalMoves(quiescence).OrderByDescending(move => GetMoveScore(move, board, memo)).ToArray();

        // (Check/Stale)mate
        if (!quiescence && orderedMoves.Length == 0) return board.IsInCheck() ? -30000 + ply : 0;

        int origAlpha = a;
        Move bestMove = Move.NullMove;
        for (int i = 0; i < orderedMoves.Length; i++)
        {
            // If out of time, return the max score. Since the caller is negating this, it will
            // appear as the worst possible move and we will just take the best we found so far
            if (IsOutOfTime(timer)) return 30000;

            // Count moves for debugging
            if (quiescence) quiescencePositionsSearched++;
            else rootPositionsSearched++;

            board.MakeMove(orderedMoves[i]);
            int score = -Search(board, -b, -a, depth - 1, timer, ply + 1);
            board.UndoMove(orderedMoves[i]);
            if (score > bestScore)
            {
                // Set the root move if we're at the root level
                if (ply == 0) bestRootMove = orderedMoves[i];
                bestScore = score;
                bestMove = orderedMoves[i];
                a = Math.Max(a, score);
                if (a >= b) break;
            }
        }
        memoTable[zob % numMemoEntries] = new MemoEntry(
            zob,
            bestMove,
            depth,
            bestScore,
            bestScore >= b ? memoType.Lower : bestScore > origAlpha ? memoType.Exact : memoType.Upper);
        return bestScore;
    }

    public Move Think(Board board, Timer timer)
    {
        Move bestRootMoveFromCompletedSearch = Move.NullMove;
        totalMemoHits = 0;
        for (int depth = 1; depth < 50; depth++)
        {
            bestRootMove = Move.NullMove;
            rootPositionsSearched = 0;
            quiescencePositionsSearched = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            // If you take the negative on int.MinValue it wraps around, so add one 
            int score = Search(board, -30000, 30000, depth, timer, 0);
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
        Console.WriteLine($"Final move: {bestRootMoveFromCompletedSearch} - ply {board.PlyCount} - Memo hits {totalMemoHits}");
        return bestRootMoveFromCompletedSearch;
    }
}