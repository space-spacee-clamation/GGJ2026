using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Jam 测试工具：拖入一个 MaterialObj prefab，点击按钮即可实例化并加入材料库存。
/// </summary>
public sealed class JamSpawnMaterialToInventoryTool : MonoBehaviour
{
    [Title("Jam：材料入库测试工具")]
    [InfoBox("拖入一个 MaterialObj prefab，点击“生成并入库”。会走 GameManager.DebugSpawnMaterialToInventory。")]

    [AssetsOnly]
    [InlineEditor(InlineEditorObjectFieldModes.Boxed)]
    [LabelText("材料 Prefab")]
    public MaterialObj MaterialPrefab;

    [Button(ButtonSizes.Large)]
    [LabelText("生成并入库")]
    private void Spawn()
    {
        if (GameManager.I == null)
        {
            Debug.LogError("[JamSpawnMaterialToInventoryTool] GameManager.I 为空，无法入库。", this);
            return;
        }
        if (MaterialPrefab == null)
        {
            Debug.LogWarning("[JamSpawnMaterialToInventoryTool] MaterialPrefab 为空。", this);
            return;
        }

        GameManager.I.DebugSpawnMaterialToInventory(MaterialPrefab);
    }
}


