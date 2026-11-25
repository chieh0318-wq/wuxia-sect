namespace Wuxia.EditorTools
{
    /// <summary>
    /// v1126e #6 talent audit (batch): every talent's described effect against the applied effect.
    /// m2_unity.ps1 -Mode setup -Project C:\Dev\wuxia-v1126e -Method Wuxia.EditorTools.TalentAuditV1126e.Run
    /// </summary>
    public static class TalentAuditV1126e
    {
        public static void Run() => Wuxia.TalentAuditV1126eCore.RunAll();

        /// <summary>Both v1126e checks in one editor session (audit first, then the battle simulation).</summary>
        public static void RunBoth()
        {
            Wuxia.TalentAuditV1126eCore.RunAll();
            Wuxia.BattleSimV1126eCore.RunAll();
        }
    }
}