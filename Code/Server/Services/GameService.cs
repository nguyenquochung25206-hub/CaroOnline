// TODO TV5: Điều phối lượt đi, kiểm tra nước đi và kết thúc trận.
using System;
using CaroGame.Shared.Game;
using Caro.Server.Validation;

namespace Caro.Server.Services
{
    public sealed class GameService
    {
        private readonly string playerXId;
        private readonly string playerOId;

        private Player currentPlayer;

        public BoardState Board { get; private set; }

        public bool IsGameOver
        {
            get { return Result != GameResult.Ongoing; }
        }

        public GameResult Result { get; private set; }

        public Player CurrentPlayer
        {
            get { return currentPlayer; }
        }

        public string CurrentPlayerId
        {
            get
            {
                return currentPlayer == Player.X
                    ? playerXId
                    : playerOId;
            }
        }

        // Khởi tạo trận đấu với hai người chơi
        public GameService(string playerXId, string playerOId)
        {
            if (string.IsNullOrWhiteSpace(playerXId))
                throw new ArgumentException("Player X khong hop le.");

            if (string.IsNullOrWhiteSpace(playerOId))
                throw new ArgumentException("Player O khong hop le.");

            if (playerXId == playerOId)
                throw new ArgumentException(
                    "Hai nguoi choi phai khac nhau.");

            this.playerXId = playerXId;
            this.playerOId = playerOId;

            Board = new BoardState(15, 15);

            currentPlayer = Player.X;
            Result = GameResult.Ongoing;
        }

        // Xử lý nước đi của người chơi
        public bool MakeMove(
            string playerId,
            int row,
            int col,
            out string error)
        {
            error = string.Empty;

            // 1. Kiểm tra trận đấu đã kết thúc chưa
            if (IsGameOver)
            {
                error = "Tran dau da ket thuc.";
                return false;
            }

            // 2. Xác định người chơi
            Player movePlayer;

            if (playerId == playerXId)
            {
                movePlayer = Player.X;
            }
            else if (playerId == playerOId)
            {
                movePlayer = Player.O;
            }
            else
            {
                error = "Nguoi choi khong thuoc tran dau.";
                return false;
            }

            // 3. Kiểm tra lượt chơi
            if (movePlayer != currentPlayer)
            {
                error = "Chua den luot cua ban.";
                return false;
            }

            // 4. Tạo nước đi
            Move move = new Move(row, col, movePlayer);

            // 5. Kiểm tra nước đi hợp lệ
            if (!MoveValidator.IsValid(Board, move, out error))
            {
                return false;
            }

            // 6. Đặt quân lên bàn cờ
            if (!Board.TryPlaceMove(move))
            {
                error = "Khong the dat quan co.";
                return false;
            }

            // 7. Kiểm tra kết quả trận đấu
            Result = WinChecker.Check(Board, move);

            // 8. Kiểm tra trận đấu đã kết thúc chưa
            if (Result == GameResult.XWins)
            {
                error = "Nguoi choi X chien thang.";
                return true;
            }

            if (Result == GameResult.OWins)
            {
                error = "Nguoi choi O chien thang.";
                return true;
            }

            if (Result == GameResult.Draw)
            {
                error = "Tran dau hoa.";
                return true;
            }

            // 9. Chuyển lượt nếu trận đấu chưa kết thúc
            currentPlayer = currentPlayer == Player.X
                ? Player.O
                : Player.X;

            return true;
        }

        // Lấy trạng thái bàn cờ
        public BoardState GetBoard()
        {
            return Board;
        }

        // Lấy mã người chơi theo ký hiệu X hoặc O
        public string? GetPlayerId(Player player)
        {
            if (player == Player.X)
                return playerXId;

            if (player == Player.O)
                return playerOId;

            return null;
        }
    }
}


