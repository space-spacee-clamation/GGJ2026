public interface IMonsterSpawnLogic
{
    /// <summary>
    /// 返回 null 表示本逻辑不处理，交给链路后续逻辑。
    /// roundIndex：第几场战斗（从 0 开始）
    /// </summary>
    CharacterConfig TrySpawn(int roundIndex, FightContext context);
}


