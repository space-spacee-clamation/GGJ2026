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
        go.hideFlags = HideFlags.HideAndDontSave;
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
        DrawAddComponentPanel();

        EditorGUILayout.Space(10);
        DrawComponentEditors();

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

    private void DrawAddComponentPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("添加组件", EditorStyles.boldLabel);
            _componentSearch = EditorGUILayout.TextField("搜索", _componentSearch ?? string.Empty);

            var filtered = _materialComponentTypes
                .Where(t => string.IsNullOrWhiteSpace(_componentSearch) || t.Name.IndexOf(_componentSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(50)
                .ToList();

            for (int i = 0; i < filtered.Count; i++)
            {
                var t = filtered[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("添加", GUILayout.Width(48)))
                    {
                        AddComponentToEditing(t);
                    }

                    EditorGUILayout.LabelField(t.Name, GUILayout.Width(220));

                    var desc = GetDefaultComponentDescription(t);
                    EditorGUILayout.LabelField(desc, EditorStyles.miniLabel);
                }
            }
        }
    }

    private void DrawComponentEditors()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("组件参数", EditorStyles.boldLabel);

            var comps = _editingInstance != null ? _editingInstance.GetComponents<MonoBehaviour>() : Array.Empty<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c == null) continue;
                if (c is MaterialObj) continue;

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(c.GetType().Name, EditorStyles.boldLabel);

                // Draw serialized fields
                var so = new SerializedObject(c);
                var it = so.GetIterator();
                bool enterChildren = true;
                while (it.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (it.name == "m_Script") continue;
                    EditorGUILayout.PropertyField(it, true);
                }
                so.ApplyModifiedProperties();

                // ReadOnly description
                var d = BuildComponentDescription(c);
                if (!string.IsNullOrWhiteSpace(d))
                {
                    EditorGUILayout.HelpBox(d, MessageType.None);
                }
            }
        }
    }

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
        inst.hideFlags = HideFlags.HideAndDontSave;
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

        var markerInterfaces = new[]
        {
            typeof(IMaterialBindEffect),
            typeof(IFightComponent),
            typeof(IAttackInfoModifier),
            typeof(IPersistentGrowthProvider),
            typeof(IMaterialDescriptionProvider),
        };

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

                bool ok = false;
                for (int k = 0; k < markerInterfaces.Length; k++)
                {
                    if (markerInterfaces[k].IsAssignableFrom(t)) { ok = true; break; }
                }
                if (!ok) continue;

                _materialComponentTypes.Add(t);
            }
        }

        _materialComponentTypes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
    }

    private void AddComponentToEditing(Type t)
    {
        if (_editingInstance == null || t == null) return;
        if (_editingInstance.GetComponent(t) != null) return;
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


