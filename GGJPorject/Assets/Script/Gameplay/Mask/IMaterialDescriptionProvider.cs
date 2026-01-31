using System.Text;

/// <summary>
/// 材质描述生成接口：按组件顺序依次写入 StringBuilder。
/// </summary>
public interface IMaterialDescriptionProvider
{
    void AppendDescription(StringBuilder sb);
}



