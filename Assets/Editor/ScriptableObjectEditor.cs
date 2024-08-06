using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class ScriptableObjectEditorWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private List<Type> scriptableObjectTypes;
    private string[] typeNames;
    private int selectedTypeIndex = 0;

    private List<ScriptableObject> currentTypeObjects = new List<ScriptableObject>();
    private string assetsFolderPath = "Assets/MyGameAssets";

    private List<Assembly> availableAssemblies;
    private string[] assemblyNames;
    private int selectedAssemblyIndex = 0;

    private bool includeDerivedTypes = true;

    [MenuItem("SH Tools/Scriptable Object Editor")]
    public static void ShowWindow()
    {
        GetWindow<ScriptableObjectEditorWindow>("Scriptable Object Editor");
    }

    private void OnEnable()
    {
        LoadAvailableAssemblies();
        LoadScriptableObjectTypes();
    }

    private void LoadAvailableAssemblies()
    {
        availableAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.FullName.StartsWith("UnityEngine") && !assembly.FullName.StartsWith("UnityEditor") && !assembly.FullName.StartsWith("Unity."))
            .OrderBy(assembly => assembly.GetName().Name)
            .ToList();

        assemblyNames = availableAssemblies.Select(assembly => assembly.GetName().Name).Prepend("All Assemblies").ToArray();
    }

    private void LoadScriptableObjectTypes()
    {
        IEnumerable<Type> types;

        if (selectedAssemblyIndex == 0)
        {
            types = availableAssemblies.SelectMany(assembly => assembly.GetTypes());
        }
        else
        {
            types = availableAssemblies[selectedAssemblyIndex - 1].GetTypes();
        }

        scriptableObjectTypes = types
            .Where(type => type.IsSubclassOf(typeof(ScriptableObject)) && !type.IsAbstract && IsInAssetsFolder(type))
            .OrderBy(type => type.Name)
            .ToList();

        typeNames = scriptableObjectTypes.Select(type => type.Name).ToArray();
    }

    private bool IsInAssetsFolder(Type type)
    {
        string[] guids = AssetDatabase.FindAssets($"t:{type.Name}", new[] { assetsFolderPath });
        return guids.Any();
    }

    private void LoadObjectsOfType(Type type)
    {
        currentTypeObjects.Clear();

        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { assetsFolderPath });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

            if (obj != null)
            {
                if (includeDerivedTypes)
                {
                    if (type.IsAssignableFrom(obj.GetType()))
                    {
                        currentTypeObjects.Add(obj);
                    }
                }
                else
                {
                    if (obj.GetType() == type)
                    {
                        currentTypeObjects.Add(obj);
                    }
                }
            }
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();

        EditorGUILayout.LabelField("Asset Management", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Path", GUILayout.Width(40));
        assetsFolderPath = EditorGUILayout.TextField(assetsFolderPath, GUILayout.Width(200));

        EditorGUILayout.LabelField("Assembly", GUILayout.Width(60));
        int newAssemblyIndex = EditorGUILayout.Popup(selectedAssemblyIndex, assemblyNames, GUILayout.Width(200));
        if (newAssemblyIndex != selectedAssemblyIndex)
        {
            selectedAssemblyIndex = newAssemblyIndex;
            LoadScriptableObjectTypes();
            selectedTypeIndex = 0;
            currentTypeObjects.Clear(); // Clear objects when changing assembly
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Type", GUILayout.Width(40));
        selectedTypeIndex = EditorGUILayout.Popup(selectedTypeIndex, typeNames, GUILayout.Width(200));

        bool newIncludeDerivedTypes = EditorGUILayout.ToggleLeft("Include Derived", includeDerivedTypes, GUILayout.Width(120));
        if (newIncludeDerivedTypes != includeDerivedTypes)
        {
            includeDerivedTypes = newIncludeDerivedTypes;
            LoadObjectsOfType(scriptableObjectTypes[selectedTypeIndex]);
        }

        if (GUILayout.Button("Load Objects", GUILayout.Width(100)))
        {
            LoadObjectsOfType(scriptableObjectTypes[selectedTypeIndex]);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (currentTypeObjects.Count > 0)
        {
            EditorGUILayout.LabelField($"Editing {typeNames[selectedTypeIndex]} Instances", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (ScriptableObject obj in currentTypeObjects)
            {
                DrawObjectProperties(obj);
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawObjectProperties(ScriptableObject obj)
    {
        SerializedObject serializedObject = new SerializedObject(obj);
        SerializedProperty property = serializedObject.GetIterator();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(obj.name, EditorStyles.boldLabel, GUILayout.Width(150));

        property.NextVisible(true);
        while (property.NextVisible(false))
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(150));
            EditorGUILayout.LabelField(property.displayName, GUILayout.Width(150));
            EditorGUILayout.PropertyField(property, GUIContent.none, GUILayout.Width(150));
            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.EndHorizontal();
    }
}
