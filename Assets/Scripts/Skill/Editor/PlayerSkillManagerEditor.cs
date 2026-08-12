using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PlayerSkillManager 自定义 Inspector：
/// 将 _initialSkillId0~3 从手动输入文本改为从 SkillLibrary 下拉选择。
/// </summary>
[CustomEditor(typeof(PlayerSkillManager))]
public class PlayerSkillManagerEditor : Editor
{
    private string[] _skillDisplayNames;
    private string[] _skillIds;
    private int[] _selectedIndices = new int[4];
    private bool _initialized;

    private static readonly string[] SlotLabels = { "槽位 0 (Q)", "槽位 1 (E)", "槽位 2 (Z)", "槽位 3 (X)" };
    private static readonly string[] SlotFields = { "_initialSkillId0", "_initialSkillId1", "_initialSkillId2", "_initialSkillId3" };

    private void InitSkillList(SkillLibrary library)
    {
        if (library == null || library.AllSkills.Count == 0)
        {
            _skillDisplayNames = new string[0];
            _skillIds = new string[0];
            _initialized = false;
            return;
        }

        var names = new List<string> { "（空）" };
        var ids = new List<string> { "" };

        foreach (var skill in library.AllSkills)
        {
            string display = string.IsNullOrEmpty(skill.skillName)
                ? skill.skillId
                : $"{skill.skillName} ({skill.skillId})";
            names.Add(display);
            ids.Add(skill.skillId);
        }

        _skillDisplayNames = names.ToArray();
        _skillIds = ids.ToArray();
        _initialized = true;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var psm = (PlayerSkillManager)target;

        // —— 技能库引用 ——
        var libProp = serializedObject.FindProperty("_skillLibrary");
        EditorGUILayout.PropertyField(libProp);
        var library = (SkillLibrary)libProp.objectReferenceValue;

        if (library == null)
        {
            EditorGUILayout.HelpBox("请先拖入 SkillLibrary 资产。", MessageType.Warning);
            DrawDefaultExceptSkillFields();
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // —— 初始化技能列表 ——
        if (!_initialized)
            InitSkillList(library);

        // —— 初始技能装配 ——
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("初始技能装配", EditorStyles.boldLabel);

        for (int i = 0; i < 4; i++)
        {
            var fieldProp = serializedObject.FindProperty(SlotFields[i]);
            string currentId = fieldProp.stringValue;

            // 查找当前选中索引
            int selectedIndex = 0;
            for (int j = 0; j < _skillIds.Length; j++)
            {
                if (_skillIds[j] == currentId)
                {
                    selectedIndex = j;
                    break;
                }
            }
            _selectedIndices[i] = selectedIndex;

            int newIndex = EditorGUILayout.Popup(SlotLabels[i], selectedIndex, _skillDisplayNames);
            if (newIndex != selectedIndex)
            {
                fieldProp.stringValue = _skillIds[newIndex];
            }

            // 显示技能简要信息
            if (newIndex > 0 && newIndex < _skillIds.Length)
            {
                var data = library.GetById(_skillIds[newIndex]);
                if (data != null)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("描述: " + data.description);
                    EditorGUILayout.LabelField("冷却: " + data.cooldown + "s");
                    EditorGUILayout.LabelField("伤害系数: " + data.damageMultiplier);
                    EditorGUI.indentLevel--;
                }
            }
        }

        // —— 一键装配随机技能 ——
        EditorGUILayout.Space();
        if (GUILayout.Button("随机装配 4 个技能"))
        {
            var random = library.GetRandomDistinct(4);
            for (int i = 0; i < 4 && i < random.Count; i++)
            {
                serializedObject.FindProperty(SlotFields[i]).stringValue = random[i].skillId;
            }
        }

        // —— 一键清空 ——
        if (GUILayout.Button("清空所有槽位"))
        {
            for (int i = 0; i < 4; i++)
                serializedObject.FindProperty(SlotFields[i]).stringValue = "";
        }

        DrawDefaultExceptSkillFields();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDefaultExceptSkillFields()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("其他设置", EditorStyles.boldLabel);

        var prop = serializedObject.GetIterator();
        if (!prop.NextVisible(true)) return;
        do
        {
            string name = prop.name;
            if (name == "_skillLibrary" ||
                name == "_initialSkillId0" ||
                name == "_initialSkillId1" ||
                name == "_initialSkillId2" ||
                name == "_initialSkillId3")
                continue;
            EditorGUILayout.PropertyField(prop, true);
        }
        while (prop.NextVisible(false));
    }
}
