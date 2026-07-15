using System.Diagnostics;
using System.Linq;
using LuminPack.CodeGen;
using LuminPack.Enum;

namespace LuminPack.Editor
{
#if UNITY_EDITOR
    
    using UnityEditor;
    using UnityEngine;
    using System.IO;
    
    public sealed class LuminDataEditorWindow : EditorWindow
    {
        
        private readonly LuminDataInfo _dataInfo = new LuminDataInfo();
        
        private Vector2 scrollPosition;
        private Texture2D customIcon;

        [MenuItem("Tools/LightData Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<LuminDataEditorWindow>();
            window.titleContent = new GUIContent(" LightData Editor", LoadLogoIcon("logo.png"));
            window.minSize = new Vector2(800, 600);
            window.maxSize = new Vector2(800, 1200);
        }

        private static string GetScriptDirectory()
        {
            // 通过MonoScript获取当前脚本路径
            var monoScript = MonoScript.FromScriptableObject(CreateInstance<LuminDataEditorWindow>());
            string scriptPath = AssetDatabase.GetAssetPath(monoScript);
            return Path.GetDirectoryName(scriptPath);
        }

        // 加载同目录图标
        private static Texture2D LoadLogoIcon(string fileName)
        {
            string dir = GetScriptDirectory();
            string iconPath = Path.Combine(dir, fileName).Replace("\\", "/");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        }

        private void OnGUI()
        {
            ApplyCustomStyles();
    
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true));
    
            // Header
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(28)); // Icon space
            EditorGUILayout.LabelField("Data Class Settings", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.Space();
    
            // Class Name
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Class Name", GUILayout.Width(120));
            _dataInfo.className = EditorGUILayout.TextField(_dataInfo.className, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
    
            // Class NameSpace
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Class NameSpace", GUILayout.Width(120));
            _dataInfo.classNameSpace = EditorGUILayout.TextField(_dataInfo.classNameSpace, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(2);
            
            // Data Value Or Reference And Burst
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("IsValueType", GUILayout.Width(120));
            _dataInfo.isValueType = EditorGUILayout.Toggle(_dataInfo.isValueType, GUILayout.Width(40));
            GUILayout.Label("EnableBurst", GUILayout.Width(120));
            _dataInfo.enableBurst = EditorGUILayout.Toggle(_dataInfo.enableBurst);

            EditorGUILayout.EndHorizontal();
    
            // Fields Header
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Data Fields:", EditorStyles.boldLabel);
    
            // Fields List
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < _dataInfo.fields.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("字段数据类型：", GUILayout.Width(100));
                    // Type dropdown
                    _dataInfo.fields[i].Type = 
                        (LuminFiledType)EditorGUILayout.EnumPopup(_dataInfo.fields[i].Type, GUILayout.Width(120));
            
                    EditorGUILayout.LabelField("字段名称：", GUILayout.Width(100));
                    // Field name
                    _dataInfo.fields[i].Name = 
                        EditorGUILayout.TextField(_dataInfo.fields[i].Name, GUILayout.Width(200));
        
                    // Remove button
                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        _dataInfo.fields.RemoveAt(i);
                        
                        EditorGUILayout.EndHorizontal();
                        
                        EditorGUILayout.Space();
                        
                        continue;
                    }
                }
                EditorGUILayout.EndHorizontal();
        
                // 检查字段类型并处理
                CheckFieldType(_dataInfo.fields[i].Type, i);
        
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndScrollView();

            // Add Field
            if (GUILayout.Button("➕ Add Field", GUILayout.Height(30)))
            {
                _dataInfo.fields.Add(new LuminDataFiled());
            }

            // Generate Button
            EditorGUILayout.Space();
            if (GUILayout.Button("Generate Parser Class", GUILayout.Height(40)))
            {
                if (GenerateCodeBeforeCheck())
                    LuminCodeGenerator.CodeGenerator(_dataInfo);
            }
    
            EditorGUILayout.EndVertical();
        }

        private void ApplyCustomStyles()
        {
            // Custom icon implementation
            if (customIcon == null)
            {
                customIcon = LoadLogoIcon("logo.png");
            }
            if (customIcon != null)
            {
                var iconRect = new Rect(4, 4, 32, 28);
                GUI.DrawTexture(iconRect, customIcon);
            }

            // Custom background color
            GUI.backgroundColor = new Color(0.9f, 0.9f, 1f);
        }
        
        [DebuggerStepThrough]
        private bool GenerateCodeBeforeCheck()
        {
            if (_dataInfo.fields.Count == 0)
            {
                Debug.LogError("字段数量为0！数据解析器生成失败！");
                
                return false;
            }

            if (_dataInfo.fields.GroupBy(x => x.Name).Any(x => x.Count() > 1))
            {
                Debug.LogError("含有相同名字字段！数据解析器生成失败！");
                
                return false;
            }
            
            return true;
        }

        private void CheckFieldType(LuminFiledType type, int index)
        {
            switch (type)
            {
                case LuminFiledType.Enum:
                    LuminDataEditorEffect.ShowEnumField(_dataInfo.fields[index]);
                    break;
                case LuminFiledType.Class or LuminFiledType.Struct:
                    LuminDataEditorEffect.ShowStructOrClassField(_dataInfo.fields[index]);
                    break;
                case LuminFiledType.List:
                    LuminDataEditorEffect.ShowListField(_dataInfo.fields[index]);
                    break;
            }
        }

        

    }
#endif
}