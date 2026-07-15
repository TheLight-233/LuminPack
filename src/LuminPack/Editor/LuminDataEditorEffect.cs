using System.Collections.Generic;
using LuminPack.Enum;


namespace LuminPack.Editor
{
#if UNITY_EDITOR
    public static class LuminDataEditorEffect
    {
        
        private static Dictionary<int, Vector2> scrollPositions = new Dictionary<int, Vector2>();
        
        public static void ShowEnumField(in LuminDataFiled filed)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Enum命名空间：", GUILayout.Width(100));

                filed.NameSpace =
                    EditorGUILayout.TextField(filed.NameSpace, GUILayout.Width(200));

                EditorGUILayout.LabelField("Enum类型：", GUILayout.Width(100));
                filed.EnumType =
                    (LuminEnumFieldType)EditorGUILayout.EnumPopup(filed.EnumType, GUILayout.Width(120));
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        public static void ShowStructOrClassField(in LuminDataFiled filed, int indentLevel = 0)
        {
            // 计算最小保留宽度（至少保留200px可视区域）
            float minWidth = Mathf.Max(200, EditorGUIUtility.currentViewWidth - indentLevel * 30 - 50);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                // 折叠标题
                EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                GUILayout.Space(indentLevel * 15);
                filed.ShowNestedFields = EditorGUILayout.Foldout(
                    filed.ShowNestedFields,
                    $"嵌套层级 {indentLevel + 1}",
                    true,
                    EditorStyles.foldoutHeader
                );
                EditorGUILayout.EndHorizontal();

                if (filed.ShowNestedFields)
                {
                    // 命名空间和类名设置
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("命名空间：", GUILayout.Width(100));
                    filed.NameSpace = EditorGUILayout.TextField(
                        filed.NameSpace,
                        GUILayout.Width(minWidth - 120) // 动态宽度
                    );
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.Space(2);
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("类或结构体名字：", GUILayout.Width(100));
                    filed.ClassName = EditorGUILayout.TextField(
                        filed.ClassName,
                        GUILayout.Width(minWidth - 120) // 动态宽度
                    );
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.Space(2);
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("泛型参数：", GUILayout.Width(100));
                    filed.ClassGenericType = EditorGUILayout.TextField(
                        filed.ClassGenericType,
                        GUILayout.Width(minWidth - 120) // 动态宽度
                    );
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(2);
                    
                    // 嵌套字段区域
                    EditorGUILayout.BeginVertical(GUILayout.Width(minWidth));
                    {
                        // 获取当前层级的滚动位置，如果不存在则初始化
                        if (!scrollPositions.ContainsKey(indentLevel))
                        {
                            scrollPositions[indentLevel] = Vector2.zero;
                        }
                        scrollPositions[indentLevel] = EditorGUILayout.BeginScrollView(
                            scrollPositions[indentLevel],
                            GUILayout.Height(Mathf.Min(filed.ClassFields.Count * 45 + 30, 300))
                        );

                        for (int i = 0; i < filed.ClassFields.Count;)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(indentLevel * 15 + 30);
                            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(minWidth - 50));
                            {
                                // 字段头
                                EditorGUILayout.BeginHorizontal();
                                {
                                    DrawTypePopup(filed.ClassFields[i], minWidth * 0.3f);
                                    DrawNameField(filed.ClassFields[i], minWidth * 0.4f);
                                    DrawRemoveButton(filed.ClassFields, i);
                                }
                                EditorGUILayout.EndHorizontal();

                                // 递归处理嵌套类型
                                CheckFieldType(filed.ClassFields[i], indentLevel + 1);
                            }
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.EndHorizontal();

                            if (i < filed.ClassFields.Count) i++;
                        }
                        EditorGUILayout.EndScrollView();
                    }
                    EditorGUILayout.EndVertical();

                    // 添加按钮
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(indentLevel * 15 + 30);
                    if (GUILayout.Button(
                            "➕ 添加嵌套字段",
                            GUILayout.Width(minWidth - 50),
                            GUILayout.Height(25))
                    )
                    {
                        filed.ClassFields.Add(new LuminDataFiled());
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();
        }
        
        public static void ShowListField(in LuminDataFiled filed, int index = 0)
        {
            if (filed.GenericType.Count <= index)
            {
                // 如果长度不足，扩展 GenericType
                while (filed.GenericType.Count <= index)
                {
                    filed.GenericType.Add(LuminGenericsType.Null);
                }
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("List泛型：", GUILayout.Width(100));

                filed.GenericType[index] = 
                    (LuminGenericsType)EditorGUILayout.EnumPopup(filed.GenericType[index], GUILayout.Width(120));
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            
            switch (filed.GenericType[index])
            {
                case LuminGenericsType.Enum:
                    ShowEnumField(filed); break;
                case LuminGenericsType.Class or LuminGenericsType.Struct:
                    ShowStructOrClassField(filed); break;
                case LuminGenericsType.List:
                    ShowListField(filed, index + 1); break;
            }
        }
        
        private static void CheckFieldType(LuminDataFiled type, int indentLevel = 0)
        {
            switch (type.Type)
            {
                case LuminFiledType.Enum:
                    ShowEnumField(type);
                    break;
                case LuminFiledType.Class or LuminFiledType.Struct:
                    ShowStructOrClassField(type, indentLevel); // 传递缩进层级
                    break;
                case LuminFiledType.List:
                    ShowListField(type);
                    break;
            }
        }
        
        private static void DrawTypePopup(LuminDataFiled field, float width)
        {
            EditorGUILayout.LabelField("类型：", GUILayout.Width(40));
            field.Type = (LuminFiledType)EditorGUILayout.EnumPopup(
                field.Type,
                GUILayout.Width(width)
            );
        }

        private static void DrawNameField(LuminDataFiled field, float width)
        {
            EditorGUILayout.LabelField("名称：", GUILayout.Width(40));
            field.Name = EditorGUILayout.TextField(
                field.Name,
                GUILayout.Width(width)
            );
        }
        
        private static void DrawRemoveButton(List<LuminDataFiled> list, int index)
        {
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                list.RemoveAt(index);
                GUIUtility.ExitGUI(); // 防止索引越界
            }
        }
    }
#endif
}