namespace Caro.Shared.Game;
// TODO TV4: Trạng thái bàn cờ; thống nhất kích thước và quy ước ô trống.
public sealed class BoardState
{
    public const int Size = 15;
    public string?[,] Cells { get; } = new string?[Size, Size];
}
