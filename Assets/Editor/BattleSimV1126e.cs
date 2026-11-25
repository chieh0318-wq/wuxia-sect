namespace Wuxia.EditorTools
{
    /// <summary>
    /// v1126e #1 before/after battle simulation (batch):
    /// m2_unity.ps1 -Mode setup -Project C:\Dev\wuxia-v1126e -Method Wuxia.EditorTools.BattleSimV1126e.Run
    /// The simulation lives in <see cref="Wuxia.BattleSimV1126eCore"/> (runtime assembly, editor-only) so it can build
    /// fights with the internal BattleSetup / BattleModel.
    /// </summary>
    public static class BattleSimV1126e
    {
        public static void Run() => Wuxia.BattleSimV1126eCore.RunAll();
    }
}