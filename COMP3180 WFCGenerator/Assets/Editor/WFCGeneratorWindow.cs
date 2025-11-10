using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public class WFCGeneratorWindow : EditorWindow
{
    [SerializeField] private List<WFCModuleEntry> moduleEntries = new List<WFCModuleEntry>();
    [SerializeField] private int maxModules = 20;
    [SerializeField] private bool randomSeed = true;
    [SerializeField] private int seed = 0;
    [SerializeField] private GameObject parentObject = null;
    [SerializeField] private bool clearBeforeGenerate = true;
    [SerializeField] private bool visualizeAfterGenerate = true;
    [SerializeField] private float visualizeStepDelay = 0.15f;

    [MenuItem("Tools/WFC Dungeon Generator")]
    public static void ShowWindow()
    {
        GetWindow<WFCGeneratorWindow>("WFC Dungeon Generator");
    }

    private void OnGUI()
    {
        SerializedObject so = new SerializedObject(this);
        SerializedProperty entriesProp = so.FindProperty("moduleEntries");

        EditorGUILayout.LabelField("Weighted Modules", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(entriesProp, new GUIContent("Entries"), true);
        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
        maxModules = EditorGUILayout.IntField(new GUIContent("Max Modules"), maxModules);
        if (maxModules < 1) maxModules = 1;

        randomSeed = EditorGUILayout.Toggle(new GUIContent("Random Seed"), randomSeed);
        EditorGUI.BeginDisabledGroup(randomSeed);
        seed = EditorGUILayout.IntField(new GUIContent("Seed"), seed);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space(10);

        parentObject = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Parent Object"), parentObject, typeof(GameObject), true);

        clearBeforeGenerate = EditorGUILayout.Toggle(
            new GUIContent("Clear Before Generate"), clearBeforeGenerate);

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Visualisation", EditorStyles.boldLabel);
        visualizeAfterGenerate = EditorGUILayout.Toggle("Visualise After Generate", visualizeAfterGenerate);
        visualizeStepDelay = EditorGUILayout.Slider("Step Delay (s)", visualizeStepDelay, 0.05f, 1.0f);

        EditorGUILayout.Space(15);

        if (GUILayout.Button("Generate Dungeon"))
        {
            if (moduleEntries == null || moduleEntries.Count == 0)
            {
                Debug.LogWarning("No module entries assigned. Please add entries.");
                return;
            }

            int valid = 0;
            foreach (var e in moduleEntries) if (e != null && e.prefab != null) valid++;
            if (valid == 0)
            {
                Debug.LogWarning("All entries are empty. Assign prefabs to your WFCModuleEntry assets.");
                return;
            }

            GameObject parentForDungeon = parentObject;
            if (parentForDungeon == null)
            {
                parentForDungeon = new GameObject("WFCDungeon");
            }
            else if (clearBeforeGenerate)
            {
                for (int i = parentForDungeon.transform.childCount - 1; i >= 0; i--)
                    DestroyImmediate(parentForDungeon.transform.GetChild(i).gameObject);
            }

            int? useSeed = randomSeed ? System.Environment.TickCount : seed;
            Debug.Log($"[WFCGeneratorWindow] Using seed {useSeed} for generation.");

            bool success = WFCDungeonGenerator.GenerateDungeon(
                moduleEntries, maxModules, parentForDungeon, useSeed);

            if (success)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                if (visualizeAfterGenerate && WFCDungeonGenerator.HasSolutionForPlayback())
                {
                    WFCDungeonGenerator.StartPlayback(visualizeStepDelay);
                }
            }
            else
            {
                Debug.LogWarning("[WFCGeneratorWindow] Dungeon generation ended with no valid configuration.");
            }
        }

        so.ApplyModifiedProperties();
    }
}
