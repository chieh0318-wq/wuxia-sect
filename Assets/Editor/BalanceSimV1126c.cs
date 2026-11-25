namespace Wuxia.EditorTools
{
    /// <summary>
    /// v1126c PA3 balance simulation entry point (batch):
    /// m2_unity.ps1 -Mode setup -Method Wuxia.EditorTools.BalanceSimV1126c.Run
    /// The simulation itself lives in <see cref="Wuxia.BalanceSimCore"/> (runtime assembly, editor-only) so it can drive
    /// the internal GameState API directly.
    /// </summary>
    public static class BalanceSimV1126c
    {
        public static void Run() => Wuxia.BalanceSimCore.RunAll();
    }
}
