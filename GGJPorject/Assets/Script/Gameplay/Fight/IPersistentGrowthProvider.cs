public interface IPersistentGrowthProvider
{
    /// <summary>
    /// 战后收集持久增值：把增长写入 delta，最后由上层一次性写回到 Player.ActualStats。
    /// </summary>
    void OnCollectPersistentGrowth(Player player, PlayerGrowthDelta delta, FightContext battleContext);
}



