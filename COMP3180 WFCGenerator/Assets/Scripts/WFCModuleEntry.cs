using UnityEngine;

[CreateAssetMenu(fileName = "WFCModuleEntry", menuName = "WFC/Module Entry")]
public class WFCModuleEntry : ScriptableObject
{
    [Tooltip("The prefab this entry controls.")]
    public GameObject prefab;

    [Tooltip("Relative selection weight. Larger = more likely when viable.")]
    [Range(0.0f, 10.0f)]
    public float baseWeight = 1.0f;

    public int GetConnectorCount()
    {
        if (prefab == null) return 0;
        int count = 0;
        foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            if (t.name == "Connector") count++;
        return count;
    }
}
