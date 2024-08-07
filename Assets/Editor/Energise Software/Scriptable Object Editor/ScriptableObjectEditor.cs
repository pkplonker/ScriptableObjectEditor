using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ScriptableObjectEditor
{
	public class ScriptableObjectEditorWindow : EditorWindow
	{
		private Vector2 scrollPosition;
		private List<Type> scriptableObjectTypes;
		private string[] typeNames;
		private int selectedTypeIndex;

		private readonly List<ScriptableObject> currentTypeObjects = new();
		private static string assetsFolderPath = "Assets/ScriptableObjects";

		private List<Assembly> availableAssemblies;
		private string[] assemblyNames;
		private int selectedAssemblyIndex;

		private bool includeDerivedTypes = true;
		private DateTime lastAssemblyCheckTime = DateTime.Now;

		[MenuItem("Window/Scriptable Object Editor &%S")]
		public static void ShowWindow() => GetWindow<ScriptableObjectEditorWindow>("Scriptable Object Editor");

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

		/// <summary>
		/// Used in the callback of EditorApplication.Update to potentially check for new loaded assemblies
		/// </summary>
		private void OnEditorUpdate()
		{
			if ((DateTime.Now - lastAssemblyCheckTime).TotalSeconds > 5)
			{
				RefreshAssembliesIfChanged();
				lastAssemblyCheckTime = DateTime.Now;
			}
		}

		/// <summary>
		/// Reloads everything if the assemblies have changed
		/// </summary>
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

		/// <summary>
		/// Reloads the assemblies
		/// </summary>
		private void LoadAvailableAssemblies()
		{
			availableAssemblies = GetAssembliesWithScriptableObjects();

			assemblyNames = availableAssemblies
				.Select(assembly => assembly.GetName().Name)
				.Prepend("All Assemblies")
				.ToArray();
		}

		/// <summary>
		/// Gets all assemblies containing a scriptable object
		/// </summary>
		/// <returns>The collection of assemblies</returns>
		private static List<Assembly> GetAssembliesWithScriptableObjects()
		{
			return AppDomain.CurrentDomain.GetAssemblies()
				.Where(assembly =>
					!assembly.FullName.StartsWith("UnityEngine") &&
					!assembly.FullName.StartsWith("UnityEditor") &&
					!assembly.FullName.StartsWith("Unity."))
				.Where(assembly =>
					assembly.GetTypes().Any(type =>
						type.IsSubclassOf(typeof(ScriptableObject)) && !type.IsAbstract && IsInAssetsFolder(type)))
				.OrderBy(assembly => assembly.GetName().Name)
				.ToList();
		}

		/// <summary>
		/// Gets all the types derived from <see cref="scriptableObjectTypes"/>
		/// </summary>
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
				.Where(type =>
					type.IsSubclassOf(typeof(ScriptableObject)) && !type.IsAbstract && IsInAssetsFolder(type))
				.OrderBy(type => type.Name)
				.ToList();

			typeNames = scriptableObjectTypes.Select(type => type.Name).ToArray();
		}

		private static bool IsInAssetsFolder(Type type)
		{
			var guids = AssetDatabase.FindAssets($"t:{type.Name}", new[] {assetsFolderPath});
			return guids.Any();
		}

		/// <summary>
		/// Adds to "currentTypeObjects"/> all objects of the provided type
		/// </summary>
		/// <param name="type">The type to load</param>
		private void LoadObjectsOfType(Type type)
		{
			if (type == null)
			{
				currentTypeObjects.Clear();
				return;
			}

			currentTypeObjects.Clear();

			var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] {assetsFolderPath});
			foreach (var guid in guids)
			{
				var assetPath = AssetDatabase.GUIDToAssetPath(guid);
				var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

				if (obj == null) continue;
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

		/// <summary>
		/// Updates the UI
		/// </summary>
		private void OnGUI()
		{
			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
			{
				EditorGUILayout.BeginVertical("box");
				{
					EditorGUILayout.LabelField("Asset Management", EditorStyles.boldLabel);

					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.LabelField("Path", GUILayout.Width(40));
						assetsFolderPath = EditorGUILayout.TextField(assetsFolderPath, GUILayout.Width(200));

						if (GUILayout.Button("Browse", GUILayout.Width(80)))
						{
							string selectedPath = EditorUtility.OpenFolderPanel("Select Scriptable Object Folder",
								assetsFolderPath, "");
							if (!string.IsNullOrEmpty(selectedPath))
							{
								assetsFolderPath = selectedPath.Replace(Application.dataPath, "Assets");
								LoadScriptableObjectTypes();
								LoadObjectsOfType(scriptableObjectTypes.FirstOrDefault());
							}
						}

						EditorGUILayout.LabelField("Assembly", GUILayout.Width(60));
						var newAssemblyIndex =
							EditorGUILayout.Popup(selectedAssemblyIndex, assemblyNames, GUILayout.Width(200));
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
					}
					EditorGUILayout.EndHorizontal();

					EditorGUILayout.Space();

					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.LabelField("Type", GUILayout.Width(40));
						var newSelectedTypeIndex =
							EditorGUILayout.Popup(selectedTypeIndex, typeNames, GUILayout.Width(200));
						if (newSelectedTypeIndex != selectedTypeIndex)
						{
							selectedTypeIndex = newSelectedTypeIndex;
							LoadObjectsOfType(scriptableObjectTypes[selectedTypeIndex]);
						}

						var newIncludeDerivedTypes =
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
					}
					EditorGUILayout.EndHorizontal();

					EditorGUILayout.Space();

					if (currentTypeObjects.Any() && typeNames.Any())
					{
						EditorGUILayout.LabelField($"Editing {typeNames[selectedTypeIndex]} Instances",
							EditorStyles.boldLabel);
						EditorGUILayout.BeginVertical();
						{
							DrawPropertiesGrid();
						}
						EditorGUILayout.EndVertical();
					}
				}
				EditorGUILayout.EndVertical();
			}
			EditorGUILayout.EndScrollView();
		}

		private void DrawPropertiesGrid()
		{
			if (currentTypeObjects.Count == 0)
				return;

			var serializedObject = new SerializedObject(currentTypeObjects[0]);
			var property = serializedObject.GetIterator();

			EditorGUILayout.BeginHorizontal("box");
			EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel, GUILayout.Width(60));
			EditorGUILayout.LabelField("Instance Name", EditorStyles.boldLabel, GUILayout.Width(150));
			property.NextVisible(true);

			var columnWidths = new List<float>();

			while (property.NextVisible(false))
			{
				columnWidths.Add(Mathf.Max(100, property.displayName.Length * 10));
				EditorGUILayout.LabelField(property.displayName, EditorStyles.boldLabel,
					GUILayout.Width(columnWidths.Last()));
			}

			EditorGUILayout.EndHorizontal();

			for (int i = 0; i < currentTypeObjects.Count; i++)
			{
				var obj = currentTypeObjects[i];
				serializedObject = new SerializedObject(obj);
				property = serializedObject.GetIterator();

				EditorGUILayout.BeginHorizontal("box");

				if (GUILayout.Button(EditorGUIUtility.IconContent("d_Toolbar Plus"), GUILayout.Width(30),
					    GUILayout.Height(18)))
				{
					var assetPath = AssetDatabase.GetAssetPath(obj);
					if (TryGetUniqueAssetPath(assetPath, obj.name, out var newAssetPath))
					{
						var newObj = Instantiate(obj);
						AssetDatabase.CreateAsset(newObj, newAssetPath);
						AssetDatabase.SaveAssets();
					}
				}

				if (GUILayout.Button(EditorGUIUtility.IconContent("d_TreeEditor.Trash"), GUILayout.Width(30),
					    GUILayout.Height(18)))
				{
					var assetPath = AssetDatabase.GetAssetPath(obj);
					AssetDatabase.DeleteAsset(assetPath);
					i--;
					AssetDatabase.SaveAssets();
				}
				else
				{
					EditorGUILayout.LabelField(obj.name, EditorStyles.textField, GUILayout.Width(150));

					property.NextVisible(true);

					var columnIndex = 0;
					while (property.NextVisible(false))
					{
						EditorGUILayout.PropertyField(property, GUIContent.none,
							GUILayout.Width(columnWidths[columnIndex]));
						columnIndex++;
					}

					serializedObject.ApplyModifiedProperties();
				}

				
				EditorGUILayout.EndHorizontal();
			}
		}

		private bool TryGetUniqueAssetPath(string originalPath, string originalName, out string newPath)
		{
			newPath = string.Empty;
			try
			{
				var directory = System.IO.Path.GetDirectoryName(originalPath);
				var extension = System.IO.Path.GetExtension(originalPath);

				var copyIndex = 1;
				do
				{
					var newFileName = $"{originalName}_Copy{copyIndex}";
					newPath = System.IO.Path.Combine(directory, $"{newFileName}{extension}");
					copyIndex++;
				} while (System.IO.File.Exists(newPath));
			}
			catch (Exception e)
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// Handles updating of asset types when assets are loaded
		/// </summary>
		public class ScriptableObjectPostprocessor : AssetPostprocessor
		{
			/// <summary>
			/// Callback for when assets are updated
			/// </summary>
			/// <param name="importedAssets">The new assets</param>
			/// <param name="deletedAssets">The deleted assets</param>
			/// <param name="movedAssets">The moved assets</param>
			/// <param name="movedFromAssetPaths">The moved from assets path</param>
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
	}
}