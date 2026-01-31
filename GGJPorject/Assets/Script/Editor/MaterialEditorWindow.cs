#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public class MaterialEditorWindow : OdinEditorWindow
{
    [MenuItem("GGJ2026/材料编辑器")]
    private static void Open()
    {
        var w = GetWindow<MaterialEditorWindow>();
        w.titleContent = new GUIContent("材料编辑器");
        w.Show();
    }

    [TitleGroup("文件夹", Alignment = TitleAlignments.Left)]
    [InfoBox("选择一个文件夹（默认：Assets/Resources/Mat），左侧会列出其中所有带 MaterialObj 的 Prefab。")]
    [SerializeField] private DefaultAsset folderAsset;

    [TitleGroup("文件夹")]
    [ShowInInspector, ReadOnly]
    private string FolderPath => folderAsset != null ? AssetDatabase.GetAssetPath(folderAsset) : _defaultFolder;

    private const string _defaultFolder = "Assets/Resources/Mat";

    [TitleGroup("左侧列表")]
    [ShowInInspector, ReadOnly]
    private List<PrefabEntry> Prefabs => _prefabs;
    private readonly List<PrefabEntry> _prefabs = new();

    private PrefabEntry _selectedEntry;

    private GameObject _editingInstance;
    private MaterialObj _editingMaterial;
    private string _selectedPrefabPath;

    // Add Component UI
    private string _componentSearch = "";
    private Vector2 _leftScroll;
    private Vector2 _rightScroll;

    private readonly List<Type> _materialComponentTypes = new();

    protected override void OnEnable()
    {
        base.OnEnable();

        if (folderAsset == null)
        {
            folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_defaultFolder);
        }

        CacheMaterialComponentTypes();
        Refresh();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        CleanupEditingInstance();
    }

    [Button("刷新列表", ButtonSizes.Medium)]
    private void Refresh()
    {
        LoadPrefabsFromFolder(FolderPath);
        if (_selectedEntry != null)
        {
            SelectByPath(_selectedEntry.Path);
        }
    }

    [Button("创建新材质（Prefab）", ButtonSizes.Medium)]
    private void CreateNew()
    {
        var folder = FolderPath;
        if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogError("[材料编辑器] 文件夹无效，请先选择正确的文件夹。");
            return;
        }

        CleanupEditingInstance();

        var go = new GameObject("NewMaterial");
        // 注意：不能使用 HideAndDontSave，否则 PrefabUtility.SaveAsPrefabAsset 会报：
        // "No objects were found for saving into prefab. Have you marked all objects with DontSave?"
        // 我们只隐藏到 Hierarchy，不使用 DontSave。
        go.hideFlags = HideFlags.HideInHierarchy;
        var mat = go.AddComponent<MaterialObj>();

        mat.Id = GetNextIdOrDefault(folder);
        mat.DisplayName = "新材质";
        mat.ResetInventoryShelfLife();

        _editingInstance = go;
        _editingMaterial = mat;
        _selectedPrefabPath = null;
        _selectedEntry = null;
    }

    protected override void OnGUI()
    {
        if (folderAsset == null)
        {
            // ensure default
            folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(_defaultFolder);
        }

        EditorGUILayout.BeginHorizontal();
        DrawLeft();
        DrawRight();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLeft()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(320));

        EditorGUILayout.LabelField("文件夹", EditorStyles.boldLabel);
        folderAsset = (DefaultAsset)EditorGUILayout.ObjectField(folderAsset, typeof(DefaultAsset), false);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新", GUILayout.Height(24))) Refresh();
        if (GUILayout.Button("创建", GUILayout.Height(24))) CreateNew();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

        for (int i = 0; i < _prefabs.Count; i++)
        {
            var e = _prefabs[i];
            bool selected = _selectedEntry != null && _selectedEntry.Path == e.Path;

            using (new EditorGUILayout.HorizontalScope(selected ? EditorStyles.helpBox : GUIStyle.none))
            {
                var label = $"{e.Id}  {e.DisplayName}";
                if (GUILayout.Button(label, GUILayout.ExpandWidth(true)))
                {
                    SelectEntry(e);
                }
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawRight()
    {
        EditorGUILayout.BeginVertical();
        _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

        EditorGUILayout.LabelField("材质信息", EditorStyles.boldLabel);

        if (_editingMaterial == null)
        {
            EditorGUILayout.HelpBox("未选择材质。请在左侧选择一个 prefab，或点击“创建”。", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        DrawMaterialBaseFields(_editingMaterial);

        EditorGUILayout.Space(10);
        DrawLogicTreeEditor();

        EditorGUILayout.Space(10);
        if (GUILayout.Button("保存到文件夹（按 {id}_{材质名} 命名）", GUILayout.Height(36)))
        {
            Save();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawMaterialBaseFields(MaterialObj mat)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("基础信息", EditorStyles.boldLabel);

            mat.Id = Mathf.Max(0, EditorGUILayout.IntField("ID", mat.Id));
            mat.DisplayName = EditorGUILayout.TextField("材质名", mat.DisplayName);
            mat.Type = (MaterialObj.MaterialType)EditorGUILayout.EnumPopup("类型", mat.Type);
            mat.BaseSprite = (Sprite)EditorGUILayout.ObjectField("Sprite", mat.BaseSprite, typeof(Sprite), false);
            mat.Quality = (MaterialQuality)EditorGUILayout.EnumPopup("品质", mat.Quality);
            mat.ManaCost = Mathf.Max(0, EditorGUILayout.IntField("法力消耗", mat.ManaCost));
            mat.ShelfLifeTurns = Mathf.Max(1, EditorGUILayout.IntField("保质期（回合）", mat.ShelfLifeTurns));

            if (GUILayout.Button("重置剩余保质期（用于测试显示）"))
            {
                mat.ResetInventoryShelfLife();
            }
            EditorGUILayout.LabelField("剩余保质期（运行时）", mat.RemainingShelfLifeTurns.ToString());
        }
    }

    // orderedComponents 链式方案已移除；材质编辑器只保留“逻辑树”配置入口。

    private void DrawLogicTreeEditor()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("逻辑树（树状配置）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "说明：运行时只使用“树状逻辑”。\n" +
                "条件节点（Gate）不满足时只会跳过该分支，不会终止整个遍历。组件可被多个节点复用（节点只是引用）。",
                MessageType.Info);

            if (_editingMaterial == null) return;

            var so = new SerializedObject(_editingMaterial);
            so.Update();
            var roots = so.FindProperty("logicTreeRoots");
            if (roots == null || !roots.isArray)
            {
                EditorGUILayout.HelpBox("找不到 MaterialObj.logicTreeRoots 字段（序列化）。", MessageType.Error);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("添加 Root（搜索）", GUILayout.Width(140)))
                {
                    var r = GUILayoutUtility.GetLastRect();
                    ShowAddNodePopup(r, roots.propertyPath);
                }
                if (GUILayout.Button("清空逻辑树", GUILayout.Width(100)))
                {
                    if (EditorUtility.DisplayDialog("清空逻辑树", "确认清空所有逻辑树节点？（不会删除组件本体）", "清空", "取消"))
                    {
                        // 递归清空（保证“清空=清空整棵树”）
                        for (int i = roots.arraySize - 1; i >= 0; i--)
                        {
                            DeleteNodeRecursive(roots, i);
                        }
                    }
                }
            }

            EditorGUILayout.Space(6);
            DrawNodeArray(roots, 0);

            // 树状最终描述预览（\t 代表层级）
            if (_editingMaterial != null)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("最终描述预览（树状，\\t 表示层级）", EditorStyles.boldLabel);
                var desc = _editingMaterial.BuildDescription();
                if (string.IsNullOrWhiteSpace(desc)) desc = "(空)";
                EditorGUILayout.HelpBox(desc, MessageType.None);
            }

            if (so.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_editingMaterial);
                Repaint();
            }
        }
    }

    private static void AddNewNodeToArray(SerializedProperty arrayProp)
    {
        if (arrayProp == null || !arrayProp.isArray) return;
        int idx = arrayProp.arraySize;
        arrayProp.InsertArrayElementAtIndex(idx);
        var elem = arrayProp.GetArrayElementAtIndex(idx);
        // 新建节点时把引用清空，避免 Unity 复制上一个元素的值
        elem.FindPropertyRelative("Title").stringValue = "";
        var expanded = elem.FindPropertyRelative("Expanded");
        if (expanded != null) expanded.boolValue = true;
        elem.FindPropertyRelative("Component").objectReferenceValue = null;
        elem.FindPropertyRelative("Role").enumValueIndex = 0; // Auto
        elem.FindPropertyRelative("ActionSide").enumValueIndex = 0; // Both
        var children = elem.FindPropertyRelative("Children");
        if (children != null && children.isArray) children.ClearArray();
    }

    private static void AddNewNodeToArray(SerializedProperty arrayProp, MonoBehaviour comp)
    {
        AddNewNodeToArray(arrayProp);
        if (arrayProp == null || !arrayProp.isArray) return;
        var elem = arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1);
        var compProp = elem.FindPropertyRelative("Component");
        if (compProp != null) compProp.objectReferenceValue = comp;
        var titleProp = elem.FindPropertyRelative("Title");
        if (titleProp != null && comp != null && string.IsNullOrWhiteSpace(titleProp.stringValue))
        {
            titleProp.stringValue = comp.GetType().Name;
        }
    }

    private void DrawNodeArray(SerializedProperty arrayProp, int indent)
    {
        if (arrayProp == null || !arrayProp.isArray) return;
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            var node = arrayProp.GetArrayElementAtIndex(i);
            DrawSingleNode(arrayProp, node, i, indent);
        }
    }

    private void DrawSingleNode(SerializedProperty parentArray, SerializedProperty node, int index, int indent)
    {
        if (node == null) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(indent * 14);

                var titleProp = node.FindPropertyRelative("Title");
                var compProp = node.FindPropertyRelative("Component");
                var roleProp = node.FindPropertyRelative("Role");
                var sideProp = node.FindPropertyRelative("ActionSide");
                var expandedProp = node.FindPropertyRelative("Expanded");

                string title = titleProp != null ? titleProp.stringValue : "";
                var comp = compProp != null ? compProp.objectReferenceValue as MonoBehaviour : null;
                var header = BuildNodeHeader(title, comp, roleProp, sideProp);
                bool expanded = expandedProp == null || expandedProp.boolValue;
                expanded = EditorGUILayout.Foldout(expanded, header, true);
                if (expandedProp != null) expandedProp.boolValue = expanded;

                GUI.enabled = index > 0;
                if (GUILayout.Button("↑", GUILayout.Width(28))) parentArray.MoveArrayElement(index, index - 1);
                GUI.enabled = index < parentArray.arraySize - 1;
                if (GUILayout.Button("↓", GUILayout.Width(28))) parentArray.MoveArrayElement(index, index + 1);
                GUI.enabled = true;

                // 只有“逻辑节点（gate/未选组件）”允许添加子节点
                bool isLogicNode = comp == null || comp is IMaterialTraversalGate;
                if (isLogicNode)
                {
                    if (GUILayout.Button("+", GUILayout.Width(28)))
                    {
                        var childArray = node.FindPropertyRelative("Children");
                        if (childArray != null && childArray.isArray)
                        {
                            var r = GUILayoutUtility.GetLastRect();
                            ShowAddNodePopup(r, childArray.propertyPath);
                        }
                    }
                }

                if (GUILayout.Button("删", GUILayout.Width(28)))
                {
                    DeleteNodeRecursive(parentArray, index);
                    // 结构变化，退出避免索引错乱（注意：DeleteNodeRecursive 内部已 Apply）
                    GUIUtility.ExitGUI();
                }
            }

            var expandedNow = node.FindPropertyRelative("Expanded");
            if (expandedNow != null && !expandedNow.boolValue) return;

            GUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                var titleProp = node.FindPropertyRelative("Title");
                GUILayout.Space(indent * 14);
                if (titleProp != null) EditorGUILayout.PropertyField(titleProp);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var compProp = node.FindPropertyRelative("Component");
                GUILayout.Space(indent * 14);
                if (compProp != null) EditorGUILayout.PropertyField(compProp);
            }

            // 严格约束：非逻辑节点不允许挂在树里
            var compCheck = node.FindPropertyRelative("Component")?.objectReferenceValue as MonoBehaviour;
            if (compCheck != null && !(compCheck is IMaterialLogicNode))
            {
                EditorGUILayout.HelpBox("该组件不是逻辑节点（未实现 IMaterialLogicNode），不允许挂在逻辑树里：已自动清空。", MessageType.Error);
                node.FindPropertyRelative("Component").objectReferenceValue = null;
                compCheck = null;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var roleProp = node.FindPropertyRelative("Role");
                var sideProp = node.FindPropertyRelative("ActionSide");
                GUILayout.Space(indent * 14);
                if (roleProp != null) EditorGUILayout.PropertyField(roleProp);
                if (sideProp != null) EditorGUILayout.PropertyField(sideProp);
            }

            // 提示：组件应来自当前 editingInstance
            var comp2 = node.FindPropertyRelative("Component")?.objectReferenceValue as MonoBehaviour;
            if (comp2 != null && _editingInstance != null && comp2.gameObject != _editingInstance)
            {
                EditorGUILayout.HelpBox("该节点引用的组件不在当前材质 prefab 上（可能导致运行时无效）。建议只引用当前 prefab 上的组件。", MessageType.Warning);
            }

            // 参数编辑：直接在逻辑树节点内编辑组件参数（组件可复用）
            if (comp2 != null && !(comp2 is MaterialObj))
            {
                EditorGUILayout.Space(4);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("节点参数（编辑组件字段）", EditorStyles.miniBoldLabel);
                    var so2 = new SerializedObject(comp2);
                    so2.Update();
                    var it = so2.GetIterator();
                    bool enterChildren = true;
                    while (it.NextVisible(enterChildren))
                    {
                        enterChildren = false;
                        if (it.name == "m_Script") continue;
                        if (it.propertyType == SerializedPropertyType.String && it.name == "description") continue;
                        EditorGUILayout.PropertyField(it, true);
                    }
                    if (so2.ApplyModifiedProperties())
                    {
                        EditorUtility.SetDirty(comp2);
                    }
                }
            }

            // Node_Effect：额外把 Effect 的参数直接嵌入显示（策划真正需要配置的是 Effect）
            if (comp2 is Node_Effect ne && ne != null && ne.Effect != null)
            {
                EditorGUILayout.Space(4);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"效果器参数（{ne.Effect.GetType().Name}）", EditorStyles.miniBoldLabel);
                    var soE = new SerializedObject(ne.Effect);
                    soE.Update();
                    var itE = soE.GetIterator();
                    bool enterChildrenE = true;
                    while (itE.NextVisible(enterChildrenE))
                    {
                        enterChildrenE = false;
                        if (itE.name == "m_Script") continue;
                        if (itE.propertyType == SerializedPropertyType.String && itE.name == "description") continue;
                        EditorGUILayout.PropertyField(itE, true);
                    }
                    if (soE.ApplyModifiedProperties())
                    {
                        EditorUtility.SetDirty(ne.Effect);
                    }
                }
            }

            var children = node.FindPropertyRelative("Children");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(indent * 14);
                // 添加统一走 header 的 “+ 搜索”
                if (GUILayout.Button("添加同级节点", GUILayout.Width(90)))
                {
                    int insertAt = index + 1;
                    parentArray.InsertArrayElementAtIndex(insertAt);
                    var elem = parentArray.GetArrayElementAtIndex(insertAt);
                    elem.FindPropertyRelative("Title").stringValue = "";
                    var expanded = elem.FindPropertyRelative("Expanded");
                    if (expanded != null) expanded.boolValue = true;
                    elem.FindPropertyRelative("Component").objectReferenceValue = null;
                    elem.FindPropertyRelative("Role").enumValueIndex = 0;
                    elem.FindPropertyRelative("ActionSide").enumValueIndex = 0;
                    var ch = elem.FindPropertyRelative("Children");
                    if (ch != null && ch.isArray) ch.ClearArray();
                }
            }

            // 非逻辑节点不显示子节点配置
            if ((comp2 == null || comp2 is IMaterialTraversalGate) && children != null && children.isArray && children.arraySize > 0)
            {
                EditorGUILayout.Space(4);
                DrawNodeArray(children, indent + 1);
            }
        }
    }

    /// <summary>
    /// 删除一个节点，并递归清空其所有子节点。
    /// 关键点：删除后必须立刻 ApplyModifiedProperties，否则如果调用了 ExitGUI 会导致删除“不生效”。
    /// </summary>
    private void DeleteNodeRecursive(SerializedProperty parentArray, int index)
    {
        if (parentArray == null || !parentArray.isArray) return;
        if (index < 0 || index >= parentArray.arraySize) return;

        var so = parentArray.serializedObject;
        if (so == null) return;

        so.Update();

        // 删除节点时，也删除对应的组件（会做引用计数，避免误删“被多个节点复用”的组件）
        // - 删除规则：只有当组件不再被任何树节点引用时才 Destroy
        // - 组件必须在当前 _editingInstance 上（避免误删引用到别处的对象）
        var toMaybeDeleteNodes = new List<MonoBehaviour>(8);
        var toMaybeDeleteEffects = new List<MonoBehaviour>(8);
        try
        {
            var roots = so.FindProperty("logicTreeRoots");
            var nodeForCollect = parentArray.GetArrayElementAtIndex(index);
            CollectComponentsInSubtree(nodeForCollect, toMaybeDeleteNodes, toMaybeDeleteEffects);

            // 先递归清空 Children，确保“删除树节点 = 删除整棵子树”
            var node = parentArray.GetArrayElementAtIndex(index);
            RecursiveClearChildren(node);

            int oldSize = parentArray.arraySize;
            parentArray.DeleteArrayElementAtIndex(index);
            // 某些情况下第一次 Delete 只会置空不缩容，这里做一次兜底
            if (parentArray.arraySize == oldSize && index >= 0 && index < parentArray.arraySize)
            {
                parentArray.DeleteArrayElementAtIndex(index);
            }

            so.ApplyModifiedProperties();

            // 结构更新后：检查引用计数再删组件
            if (_editingInstance != null && roots != null && roots.isArray)
            {
                // 先删 Effect（避免 Node_Effect 先删后引用丢失导致计数误判）
                for (int i = 0; i < toMaybeDeleteEffects.Count; i++)
                {
                    var eff = toMaybeDeleteEffects[i];
                    if (eff == null) continue;
                    if (eff.gameObject != _editingInstance) continue;
                    if (CountEffectRefsInTree(roots, eff) > 0) continue;
                    Undo.DestroyObjectImmediate(eff);
                }

                for (int i = 0; i < toMaybeDeleteNodes.Count; i++)
                {
                    var c = toMaybeDeleteNodes[i];
                    if (c == null) continue;
                    if (c.gameObject != _editingInstance) continue;
                    if (CountComponentRefsInTree(roots, c) > 0) continue;
                    Undo.DestroyObjectImmediate(c);
                }
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            if (_editingMaterial != null) EditorUtility.SetDirty(_editingMaterial);
            Repaint();
        }

        return;

    }

    private static void CollectComponentsInSubtree(SerializedProperty nodeProp, List<MonoBehaviour> outNodeComps, List<MonoBehaviour> outEffects)
    {
        if (nodeProp == null) return;
        var compProp = nodeProp.FindPropertyRelative("Component");
        var comp = compProp != null ? compProp.objectReferenceValue as MonoBehaviour : null;
        if (comp != null)
        {
            outNodeComps?.Add(comp);
            if (comp is Node_Effect ne && ne != null && ne.Effect != null)
            {
                outEffects?.Add(ne.Effect);
            }
        }

        var children = nodeProp.FindPropertyRelative("Children");
        if (children == null || !children.isArray) return;
        for (int i = 0; i < children.arraySize; i++)
        {
            var child = children.GetArrayElementAtIndex(i);
            CollectComponentsInSubtree(child, outNodeComps, outEffects);
        }
    }

    private static int CountComponentRefsInTree(SerializedProperty nodesArray, MonoBehaviour target)
    {
        if (nodesArray == null || !nodesArray.isArray || target == null) return 0;
        int count = 0;
        for (int i = 0; i < nodesArray.arraySize; i++)
        {
            var node = nodesArray.GetArrayElementAtIndex(i);
            var comp = node.FindPropertyRelative("Component")?.objectReferenceValue as MonoBehaviour;
            if (comp == target) count++;

            var children = node.FindPropertyRelative("Children");
            if (children != null && children.isArray && children.arraySize > 0)
            {
                count += CountComponentRefsInTree(children, target);
            }
        }
        return count;
    }

    private static int CountEffectRefsInTree(SerializedProperty nodesArray, MonoBehaviour targetEffect)
    {
        if (nodesArray == null || !nodesArray.isArray || targetEffect == null) return 0;
        int count = 0;
        for (int i = 0; i < nodesArray.arraySize; i++)
        {
            var node = nodesArray.GetArrayElementAtIndex(i);
            var comp = node.FindPropertyRelative("Component")?.objectReferenceValue as MonoBehaviour;
            if (comp is Node_Effect ne && ne != null && ne.Effect == targetEffect) count++;

            var children = node.FindPropertyRelative("Children");
            if (children != null && children.isArray && children.arraySize > 0)
            {
                count += CountEffectRefsInTree(children, targetEffect);
            }
        }
        return count;
    }

    private static void RecursiveClearChildren(SerializedProperty nodeProp)
    {
        if (nodeProp == null) return;
        var children = nodeProp.FindPropertyRelative("Children");
        if (children == null || !children.isArray) return;

        for (int i = children.arraySize - 1; i >= 0; i--)
        {
            var child = children.GetArrayElementAtIndex(i);
            RecursiveClearChildren(child);
            children.DeleteArrayElementAtIndex(i);
            // managed type 通常一次就能删；这里做一次兜底
            if (i < children.arraySize && children.GetArrayElementAtIndex(i) == null)
            {
                children.DeleteArrayElementAtIndex(i);
            }
        }
        children.ClearArray();
    }

    private void ShowAddNodePopup(Rect activatorRect, string targetArrayPath)
    {
        if (string.IsNullOrWhiteSpace(targetArrayPath)) return;
        if (_editingInstance == null) return;
        if (_editingMaterial == null) return;

        NodeAddPopupWindow.Show(
            activatorRect,
            _editingInstance,
            _materialComponentTypes,
            onPickNew: t =>
            {
                if (t == null) return;
                var added = Undo.AddComponent(_editingInstance, t) as MonoBehaviour;
                if (added == null) return;
                var so = new SerializedObject(_editingMaterial);
                so.Update();
                var arr = so.FindProperty(targetArrayPath);
                if (arr == null || !arr.isArray) return;
                Undo.RecordObject(_editingMaterial, "Add Logic Node");
                AddPickedComponentAsNode(arr, added);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(_editingMaterial);
                EditorUtility.SetDirty(added);
                Repaint();
            }
        );
    }

    /// <summary>
    /// 弹窗选择后：把“逻辑节点”直接加入树；把“纯效果器”包装成 Node_Effect 再加入树。
    /// </summary>
    private void AddPickedComponentAsNode(SerializedProperty targetArray, MonoBehaviour picked)
    {
        if (targetArray == null || !targetArray.isArray) return;
        if (picked == null) return;

        // 逻辑节点：可直接进入树
        if (picked is IMaterialLogicNode)
        {
            AddNewNodeToArray(targetArray, picked);
            return;
        }

        // 纯效果器：必须用 Node_Effect 包装后才能进树
        if (picked is IMaterialEffect || picked is IMaterialAttackInfoEffect)
        {
            var node = Undo.AddComponent(_editingInstance, typeof(Node_Effect)) as Node_Effect;
            if (node == null) return;
            node.Effect = picked;
            EditorUtility.SetDirty(node);
            AddNewNodeToArray(targetArray, node);
            return;
        }
    }

    private static string BuildNodeHeader(string title, MonoBehaviour comp, SerializedProperty roleProp, SerializedProperty sideProp)
    {
        var t = comp != null ? comp.GetType().Name : "未选择组件";
        var role = roleProp != null ? roleProp.enumDisplayNames[roleProp.enumValueIndex] : "Auto";
        var side = sideProp != null ? sideProp.enumDisplayNames[sideProp.enumValueIndex] : "Both";

        // 让策划更直观：Gate 默认就是条件节点
        if (comp is IMaterialTraversalGate) role = "Condition";

        var name = string.IsNullOrWhiteSpace(title) ? t : title;
        var head = $"{name}  [{role}] [{side}]";

        // 追加中文描述摘要，方便搜索/阅读
        if (comp is IMaterialDescriptionProvider p)
        {
            try
            {
                var sb = new System.Text.StringBuilder(128);
                p.AppendDescription(sb);
                var desc = sb.ToString().Trim().Replace("\r", "").Replace("\n", " | ");
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    head += $"  -  {desc}";
                }
            }
            catch { /* ignore */ }
        }

        return head;
    }

    /// <summary>
    /// 逻辑树节点添加弹窗（内置在同文件，避免 Jam 期间 csproj/Editor Assembly 差异导致的类型不可见问题）。
    /// 支持搜索：类型名 + 中文描述（来自 IMaterialDescriptionProvider 的默认描述）。
    /// </summary>
    private sealed class NodeAddPopupWindow : EditorWindow
    {
        private sealed class Entry
        {
            public bool IsExisting;
            public MonoBehaviour ExistingComp;
            public Type NewType;
            public string Name;
            public string Desc;
            public bool Disabled;
        }

        private readonly List<Entry> _entries = new();
        private Vector2 _scroll;
        private string _search = "";
        private bool _foldLogic = true;
        private bool _foldEffects = true;

        private Action<Type> _onPickNew;

        public static void Show(
            Rect activatorRect,
            GameObject editingInstance,
            IReadOnlyList<Type> candidateTypes,
            Action<Type> onPickNew)
        {
            var w = CreateInstance<NodeAddPopupWindow>();
            w.titleContent = new GUIContent("添加节点");
            w._onPickNew = onPickNew;
            w.BuildEntries(editingInstance, candidateTypes);
            w.ShowAsDropDown(activatorRect, new Vector2(520, 420));
        }

        private void BuildEntries(GameObject editingInstance, IReadOnlyList<Type> candidateTypes)
        {
            _entries.Clear();
            if (candidateTypes == null) return;

            // New component types
            for (int i = 0; i < candidateTypes.Count; i++)
            {
                var t = candidateTypes[i];
                if (t == null) continue;
                bool disallow = Attribute.IsDefined(t, typeof(DisallowMultipleComponent), inherit: true);
                bool hasOne = editingInstance != null && editingInstance.GetComponent(t) != null;
                bool disabled = disallow && hasOne;
                var extra = disabled ? "（该脚本带 DisallowMultipleComponent，不能重复添加）" : "";
                _entries.Add(new Entry
                {
                    IsExisting = false,
                    NewType = t,
                    Name = $"{t.Name}（新建）",
                    Desc = GetDefaultComponentDescription(t) + extra,
                    Disabled = disabled,
                });
            }

            _entries.Sort((a, b) =>
            {
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("添加节点（搜索支持：类型名 / 中文描述）", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField(_search ?? string.Empty);
                if (GUILayout.Button("清空", GUILayout.Width(50))) _search = string.Empty;
            }

            DrawCategoryToggles();

            EditorGUILayout.Space(6);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            var s = _search ?? string.Empty;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e == null) continue;
                if (!string.IsNullOrWhiteSpace(s))
                {
                    bool hit = (e.Name != null && e.Name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) ||
                               (e.Desc != null && e.Desc.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!hit) continue;
                }

                // 分类：逻辑节点 / 纯效果器
                bool isLogic = e.IsExisting
                    ? (e.ExistingComp is IMaterialLogicNode)
                    : (e.NewType != null && typeof(IMaterialLogicNode).IsAssignableFrom(e.NewType));
                if (isLogic && !_foldLogic) continue;
                if (!isLogic && !_foldEffects) continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUI.enabled = !e.Disabled;
                        if (GUILayout.Button("选用", GUILayout.Width(60)))
                        {
                            _onPickNew?.Invoke(e.NewType);
                            Close();
                            GUIUtility.ExitGUI();
                        }
                        GUI.enabled = true;

                        EditorGUILayout.LabelField((isLogic ? "[逻辑] " : "[效果器] ") + e.Name, EditorStyles.boldLabel);
                    }

                    if (!string.IsNullOrWhiteSpace(e.Desc))
                    {
                        EditorGUILayout.LabelField(e.Desc, EditorStyles.miniLabel);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void OnInspectorUpdate()
        {
            // keep repaint smooth for dropdown
            Repaint();
        }

        private void DrawCategoryToggles()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                _foldLogic = EditorGUILayout.ToggleLeft("逻辑节点", _foldLogic, GUILayout.Width(100));
                _foldEffects = EditorGUILayout.ToggleLeft("效果器", _foldEffects, GUILayout.Width(100));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("（可折叠过滤）", EditorStyles.miniLabel, GUILayout.Width(90));
            }
        }

        private static string GetDefaultComponentDescription(Type t)
        {
            if (t == null) return string.Empty;
            var go = new GameObject("tmp_desc_go");
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var comp = go.AddComponent(t) as MonoBehaviour;
                return BuildComponentDescription(comp);
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                DestroyImmediate(go);
            }
        }

        private static string BuildComponentDescription(MonoBehaviour comp)
        {
            if (comp == null) return string.Empty;
            try
            {
                if (comp is IMaterialDescriptionProvider p)
                {
                    var sb = new System.Text.StringBuilder(128);
                    p.AppendDescription(sb);
                    return sb.ToString().Trim().Replace("\r", "").Replace("\n", " | ");
                }
            }
            catch { /* ignore */ }
            return string.Empty;
        }
    }

    // orderedComponents 链式方案已移除：不再提供 “顺序列表” 的初始化/排序/删除 API。

    private void Save()
    {
        var folder = FolderPath;
        if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogError("[材料编辑器] 文件夹无效，无法保存。");
            return;
        }
        if (_editingMaterial == null || _editingInstance == null)
        {
            Debug.LogError("[材料编辑器] 当前无可保存对象。");
            return;
        }

        if (_editingMaterial.Id <= 0)
        {
            _editingMaterial.Id = GetNextIdOrDefault(folder);
        }

        var name = string.IsNullOrWhiteSpace(_editingMaterial.DisplayName) ? "Material" : _editingMaterial.DisplayName;
        name = SanitizeFileName(name);
        var fileName = $"{_editingMaterial.Id}_{name}";
        var targetPath = $"{folder}/{fileName}.prefab";

        // Ensure not overwrite other asset unintentionally
        if (!string.IsNullOrWhiteSpace(_selectedPrefabPath) && _selectedPrefabPath != targetPath)
        {
            // Move existing asset to new path (keeps GUID if possible)
            var moveErr = AssetDatabase.MoveAsset(_selectedPrefabPath, targetPath);
            if (!string.IsNullOrWhiteSpace(moveErr))
            {
                // fallback: save new and delete old
                PrefabUtility.SaveAsPrefabAsset(_editingInstance, targetPath);
                AssetDatabase.DeleteAsset(_selectedPrefabPath);
            }
            else
            {
                PrefabUtility.SaveAsPrefabAsset(_editingInstance, targetPath);
            }
        }
        else
        {
            PrefabUtility.SaveAsPrefabAsset(_editingInstance, targetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Refresh();
        SelectByPath(targetPath);
    }

    private void SelectEntry(PrefabEntry e)
    {
        _selectedEntry = e;
        SelectByPath(e.Path);
    }

    private void SelectByPath(string path)
    {
        CleanupEditingInstance();

        _selectedPrefabPath = path;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[材料编辑器] 无法加载 prefab：{path}");
            return;
        }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        // 同 CreateNew：不能 DontSave，否则无法保存回 prefab
        inst.hideFlags = HideFlags.HideInHierarchy;
        _editingInstance = inst;
        _editingMaterial = inst.GetComponent<MaterialObj>();
        if (_editingMaterial == null)
        {
            Debug.LogError("[材料编辑器] 该 prefab 不包含 MaterialObj。");
            CleanupEditingInstance();
        }
    }

    private void CleanupEditingInstance()
    {
        if (_editingInstance != null)
        {
            DestroyImmediate(_editingInstance);
            _editingInstance = null;
            _editingMaterial = null;
        }
    }

    private void LoadPrefabsFromFolder(string folder)
    {
        _prefabs.Clear();
        if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder)) return;

        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            var mat = go.GetComponent<MaterialObj>();
            if (mat == null) continue;

            int id = mat.Id;
            if (id <= 0) id = TryParseIdFromName(Path.GetFileNameWithoutExtension(path));
            var displayName = !string.IsNullOrWhiteSpace(mat.DisplayName) ? mat.DisplayName : go.name;
            _prefabs.Add(new PrefabEntry { Path = path, Id = id, DisplayName = displayName, Prefab = go });
        }

        _prefabs.Sort((a, b) => a.Id.CompareTo(b.Id));
    }

    private int GetNextIdOrDefault(string folder)
    {
        int max = 0;
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            var mat = go.GetComponent<MaterialObj>();
            if (mat == null) continue;
            int id = mat.Id;
            if (id <= 0) id = TryParseIdFromName(Path.GetFileNameWithoutExtension(path));
            if (id > max) max = id;
        }
        return max > 0 ? max + 1 : 100001;
    }

    private static int TryParseIdFromName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return 0;
        int idx = fileName.IndexOf('_');
        if (idx <= 0) return 0;
        var head = fileName.Substring(0, idx);
        return int.TryParse(head, out var id) ? id : 0;
    }

    private void CacheMaterialComponentTypes()
    {
        _materialComponentTypes.Clear();

        // 弹窗候选类型：
        // - 逻辑节点：IMaterialLogicNode（可直接进入树）
        // - 纯效果器：IMaterialEffect / IMaterialAttackInfoEffect（选中后会自动创建 Node_Effect 包装进树）

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch { continue; }

            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                if (t == null || t.IsAbstract) continue;
                if (!typeof(MonoBehaviour).IsAssignableFrom(t)) continue;
                if (t == typeof(MaterialObj)) continue;
                if (t.Namespace != null && t.Namespace.StartsWith("Unity", StringComparison.Ordinal)) continue;

                bool isLogicNode = typeof(IMaterialLogicNode).IsAssignableFrom(t);
                bool isEffector = typeof(IMaterialEffect).IsAssignableFrom(t) || typeof(IMaterialAttackInfoEffect).IsAssignableFrom(t);
                if (!isLogicNode && !isEffector) continue;

                _materialComponentTypes.Add(t);
            }
        }

        _materialComponentTypes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
    }

    private void AddComponentToEditing(Type t)
    {
        if (_editingInstance == null || t == null) return;
        // Jam：允许同一材质挂多个“同类型”组件实例（用于逻辑树不同分支复用同类型节点）。
        // 若该组件脚本带 [DisallowMultipleComponent]，Unity 会阻止重复添加（这属于 Unity 规则，而不是编辑器限制）。
        Undo.AddComponent(_editingInstance, t);
    }

    private static string GetDefaultComponentDescription(Type t)
    {
        if (t == null) return string.Empty;
        var go = new GameObject("tmp_desc_go");
        go.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var comp = go.AddComponent(t) as MonoBehaviour;
            return BuildComponentDescription(comp);
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            DestroyImmediate(go);
        }
    }

    private static string BuildComponentDescription(MonoBehaviour comp)
    {
        if (comp == null) return string.Empty;
        var sb = new StringBuilder(128);

        if (comp is IMaterialDescriptionProvider p)
        {
            p.AppendDescription(sb);
        }
        else
        {
            // fallback: try a "description" field
            var f = comp.GetType().GetField("description", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(string))
            {
                var v = f.GetValue(comp) as string;
                if (!string.IsNullOrWhiteSpace(v)) sb.AppendLine(v);
            }
        }

        return sb.ToString().Trim();
    }

    private static string SanitizeFileName(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Material";
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            s = s.Replace(c, '_');
        }
        return s.Trim();
    }

    [Serializable]
    private sealed class PrefabEntry
    {
        public string Path;
        public int Id;
        public string DisplayName;
        public GameObject Prefab;
    }
}
#endif


