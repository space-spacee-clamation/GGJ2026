using System.Collections.Generic;

public interface IMaterialDropMethod
{
    /// <summary>
    /// 掉落入口：根据材料池与幸运值抽取本次掉落。
    /// - luck: 0~100（超出会被夹紧）
    /// - dropCount: 本次掉落数量（<=0 返回空；>100 将被夹紧到 100）
    /// </summary>
    IReadOnlyList<MaterialDropEntry> Roll(MaterialPool pool, int luck, int dropCount);
}



