using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class ScriptableObjectEditorWindow : EditorWindow
{
	private Vector2 scrollPosition;
	public List<Type> scriptableObjectTypes;
	private string[] typeNames;
	public int selectedTypeIndex;

	private List<ScriptableObject> currentTypeObjects = new();
	private static string assetsFolderPath = "Assets/ScriptableObjects";

	private List<Assembly> availableAssemblies;
	private string[] assemblyNames;
	private int selectedAssemblyIndex;

	private bool includeDerivedTypes = true;
	private DateTime lastAssemblyCheckTime = DateTime.Now;

	[MenuItem("SH Tools/Scriptable Object Editor")]
	public static void ShowWindow()
	{
		GetWindow<ScriptableObjectEditorWindow>("Scriptable Object Editor");
	}

	private void OnEnable()
	{
		LoadAvailableAssemblies();
		LoadScriptableObjectTypes();
		EditorApplication.update += OnEditorUpdate;
	}

	private void OnDisable()
	{
		EditorApplication.update -= OnEditorUpdate;
	}

	private void OnEditorUpdate()
	{
		if ((DateTime.Now - lastAssemblyCheckTime).TotalSeconds > 5)
		{
			RefreshAssembliesIfChanged();
			lastAssemblyCheckTime = DateTime.Now;
		}
	}

	private void RefreshAssembliesIfChanged()
	{
		var currentAssemblies = GetAssembliesWithScriptableObjects();

		if (!currentAssemblies.SequenceEqual(availableAssemblies))
		{
			LoadAvailableAssemblies();
			LoadScriptableObjectTypes();
			LoadObjectsOfType(scriptableObjectTypes.FirstOrDefault());
		}
	}

	private void LoadAvailableAssemblies()
	{
		availableAssemblies = GetAssembliesWithScriptableObjects();

		assemblyNames = availableAssemblies
			.Select(assembly => assembly.GetName().Name)
			.Prepend("All Assemblies")
			.ToArray();
	}

	private static List<Assembly> GetAssembliesWithScriptableObjects()
	{
		return AppDomain.CurrentDomain.GetAssemblies()
			.Where(assembly =>
				!assembly.FullName.StartsWith("UnityEngine") &&
				!assembly.FullName.StartsWith("UnityEditor") &&
				!assembly.FullName.StartsWith("Unity."))
			.Where(assembly =>
				assembly.GetTypes().Any(type => type.IsSubclassOf(typeof(ScriptableObject)) && !type.IsAbstract && IsInAssetsFolder(type)))
			.Where(assembly => IsUserAssembly(assembly))
			.OrderBy(assembly => assembly.GetName().Name)
			.ToList();
	}

	private static bool IsUserAssembly(Assembly assembly)
	{
		// Filter by project path or specific naming convention for user assemblies
		var a = assembly;
		return true;
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

	private static bool IsInAssetsFolder(Type type)
	{
		string[] guids = AssetDatabase.FindAssets($"t:{type.Name}", new[] {assetsFolderPath});
		return guids.Any();
	}

	public void LoadObjectsOfType(Type type)
	{
		if (type == null)
		{
			currentTypeObjects.Clear();
			return;
		}

		currentTypeObjects.Clear();

		string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] {assetsFolderPath});
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
		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Width(position.width), GUILayout.Height(position.height));
		EditorGUILayout.BeginVertical("box");

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
			LoadObjectsOfType(scriptableObjectTypes.FirstOrDefault());
		}

		if (GUILayout.Button("Refresh Assemblies", GUILayout.Width(150)))
		{
			LoadAvailableAssemblies();
			LoadScriptableObjectTypes();
			LoadObjectsOfType(scriptableObjectTypes.FirstOrDefault());
		}

		EditorGUILayout.EndHorizontal();

		EditorGUILayout.Space();

		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField("Type", GUILayout.Width(40));
		int newSelectedTypeIndex = EditorGUILayout.Popup(selectedTypeIndex, typeNames, GUILayout.Width(200));
		if (newSelectedTypeIndex != selectedTypeIndex)
		{
			selectedTypeIndex = newSelectedTypeIndex;
			LoadObjectsOfType(scriptableObjectTypes[selectedTypeIndex]);
		}

		bool newIncludeDerivedTypes =
			EditorGUILayout.ToggleLeft("Include Derived", includeDerivedTypes, GUILayout.Width(120));
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
		if (selectedTypeIndex > typeNames.Length)
		{
			selectedTypeIndex = 0;
		}

		if (currentTypeObjects.Any() && typeNames.Any())
		{
			EditorGUILayout.LabelField($"Editing {typeNames[selectedTypeIndex]} Instances", EditorStyles.boldLabel);
			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

			DrawPropertiesGrid();

			EditorGUILayout.EndScrollView();
		}

		EditorGUILayout.EndVertical();
	}

	private void DrawPropertiesGrid()
	{
		if (currentTypeObjects.Count == 0)
			return;

		SerializedObject serializedObject = new SerializedObject(currentTypeObjects[0]);
		SerializedProperty property = serializedObject.GetIterator();

		EditorGUILayout.BeginHorizontal("box");
		EditorGUILayout.LabelField("Instance Name", EditorStyles.boldLabel, GUILayout.Width(150));
		property.NextVisible(true);

		List<float> columnWidths = new List<float>();

		while (property.NextVisible(false))
		{
			columnWidths.Add(Mathf.Max(100, property.displayName.Length * 10));
			EditorGUILayout.LabelField(property.displayName, EditorStyles.boldLabel,
				GUILayout.Width(columnWidths.Last()));
		}

		EditorGUILayout.EndHorizontal();

		for (int i = 0; i < currentTypeObjects.Count; i++)
		{
			ScriptableObject obj = currentTypeObjects[i];
			serializedObject = new SerializedObject(obj);
			property = serializedObject.GetIterator();
			try
			{
				EditorGUILayout.BeginHorizontal("box");
				EditorGUILayout.LabelField(obj.name, EditorStyles.textField, GUILayout.Width(150));

				property.NextVisible(true);

				int columnIndex = 0;
				while (property.NextVisible(false))
				{
					EditorGUILayout.PropertyField(property, GUIContent.none,
						GUILayout.Width(columnWidths[columnIndex]));
					columnIndex++;
				}

				serializedObject.ApplyModifiedProperties();
			}
			finally
			{
				EditorGUILayout.EndHorizontal();
			}
			
		}
	}
}

public class ScriptableObjectPostprocessor : AssetPostprocessor
{
	static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
		string[] movedFromAssetPaths)
	{
		bool refreshNeeded = importedAssets
			.Concat(deletedAssets)
			.Concat(movedAssets)
			.Any(assetPath => assetPath.EndsWith(".asset"));
		if (refreshNeeded)
		{
			var windows = Resources.FindObjectsOfTypeAll<ScriptableObjectEditorWindow>();
			foreach (var window in windows)
			{
				window.LoadObjectsOfType(window.scriptableObjectTypes[window.selectedTypeIndex]);
			}
		}
	}
}