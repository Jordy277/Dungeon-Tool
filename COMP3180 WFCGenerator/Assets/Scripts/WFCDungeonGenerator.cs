using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

public static class WFCDungeonGenerator
{
    private class OpenConnector
    {
        public Transform transform;
        public OpenConnector(Transform t) { transform = t; }
    }

    private struct Placement
    {
        public GameObject prefab;
        public Vector3 pos;
        public Quaternion rot;
    }
    private static readonly List<Placement> lastSolution = new();
    private static GameObject lastParentForPlayback;

    private const float BoundsShrink = 0.01f;

    // Loop-closure tuning
    private const float LoopAlignDotMin = -0.9f;
    private const float LoopDistMax = 0.6f;

    public static bool GenerateDungeon(List<WFCModuleEntry> moduleEntries, int maxModules, GameObject parentObject, int? seed = null)
    {
        System.Random rng = (seed.HasValue ? new System.Random(seed.Value) : new System.Random());

        // Valid entries only
        var entries = new List<WFCModuleEntry>();
        foreach (var e in moduleEntries)
            if (e != null && e.prefab != null) entries.Add(e);
        if (entries.Count == 0) return false;

        List<GameObject> placedModules = new();
        List<OpenConnector> openConnectors = new();
        lastSolution.Clear();
        lastParentForPlayback = parentObject;

        // helpers
        Func<GameObject, List<Transform>> findConnectors = (module) =>
        {
            var result = new List<Transform>();
            foreach (var t in module.GetComponentsInChildren<Transform>(true))
                if (t.name == "Connector") result.Add(t);
            return result;
        };

        Action<GameObject, Transform> addOpenConnectorsForModule = (module, usedConnector) =>
        {
            foreach (var c in findConnectors(module))
                if (usedConnector == null || c != usedConnector)
                    openConnectors.Add(new OpenConnector(c));
        };

        Action<Transform> removeOpenConnector = (connectorTransform) =>
        {
            openConnectors.RemoveAll(conn => conn.transform == connectorTransform);
        };

        Func<GameObject, Bounds> computeWorldBounds = (go) =>
        {
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            b.Expand(-BoundsShrink);
            return b;
        };

        Func<GameObject, bool> isOverlappingExisting = (newModule) =>
        {
            var newCols = newModule.GetComponentsInChildren<Collider>(true);
            if (newCols.Length > 0)
            {
                foreach (var placed in placedModules)
                {
                    var placedCols = placed.GetComponentsInChildren<Collider>(true);
                    foreach (var a in newCols)
                    {
                        if (!a.enabled) continue;
                        foreach (var b in placedCols)
                        {
                            if (!b.enabled) continue;
                            Vector3 dir; float dist;
                            bool ov = Physics.ComputePenetration(
                                a, a.transform.position, a.transform.rotation,
                                b, b.transform.position, b.transform.rotation,
                                out dir, out dist);
                            if (ov && dist > 0f) return true;
                            if (!ov && a.bounds.Intersects(b.bounds)) return true;
                        }
                    }
                }
                return false;
            }

            Bounds nb = computeWorldBounds(newModule);
            foreach (var placed in placedModules)
            {
                Bounds pb = computeWorldBounds(placed);
                if (nb.size != Vector3.zero && pb.size != Vector3.zero && nb.Intersects(pb))
                    return true;
            }
            return false;
        };

        // align attachConnector on newModule to targetConn on existing graph
        Action<GameObject, Transform, Transform, int> alignModuleAtConnector = (newModule, attachConnector, targetConn, mode) =>
        {
            Vector3 desiredFwd = (mode == 0) ? -targetConn.forward : targetConn.forward;
            Quaternion rot = Quaternion.LookRotation(desiredFwd, targetConn.up) *
                             Quaternion.Inverse(Quaternion.LookRotation(attachConnector.forward, attachConnector.up));
            newModule.transform.rotation = rot;

            Vector3 delta = targetConn.position - attachConnector.position;
            newModule.transform.position += delta;
        };

        // try to place a prefab at a given open connector
        Func<GameObject, OpenConnector, GameObject> tryPlaceModuleAt = (prefab, targetOpen) =>
        {
            Transform targetConn = targetOpen.transform;

            GameObject newModule =
                PrefabUtility.InstantiatePrefab(prefab) as GameObject ??
                UnityEngine.Object.Instantiate(prefab);

            newModule.transform.SetParent(parentObject.transform, true);
            newModule.transform.position = Vector3.zero;
            newModule.transform.rotation = Quaternion.identity;

            var connectors = new List<Transform>();
            foreach (var t in newModule.GetComponentsInChildren<Transform>(true))
                if (t.name == "Connector") connectors.Add(t);

            if (connectors.Count == 0)
            {
                UnityEngine.Object.DestroyImmediate(newModule);
                return null;
            }

            bool placed = false;

            foreach (var attach in connectors)
            {
                for (int mode = 0; mode < 2; mode++)
                {
                    newModule.transform.position = Vector3.zero;
                    newModule.transform.rotation = Quaternion.identity;

                    alignModuleAtConnector(newModule, attach, targetConn, mode);

                    if (!isOverlappingExisting(newModule))
                    {
                        placed = true;
                        goto SUCCESS;
                    }
                }
            }

        SUCCESS:
            if (!placed)
            {
                UnityEngine.Object.DestroyImmediate(newModule);
                return null;
            }
            return newModule;
        };

        // select an open connector to attempt to fill
        OpenConnector SelectConnector(List<OpenConnector> oc, List<WFCModuleEntry> ents)
        {
            double roll = rng.NextDouble();

            if (roll < 0.80) return oc[oc.Count - 1];

            if (roll < 0.95) return oc[rng.Next(oc.Count)];

            OpenConnector best = null; int min = int.MaxValue;
            foreach (var conn in oc)
            {
                int count = 0;
                foreach (var e in ents)
                {
                    var t = tryPlaceModuleAt(e.prefab, conn);
                    if (t != null)
                    {
                        UnityEngine.Object.DestroyImmediate(t);
                        count++;
                    }
                }
                if (count < min) { min = count; best = conn; if (min == 0) break; }
            }
            return best ?? oc[oc.Count - 1];
        }

        // loop-closure attempt before placement
        bool TryCloseLoops(List<OpenConnector> oc, List<GameObject> placed)
        {
            for (int i = 0; i < oc.Count; i++)
            {
                var a = oc[i].transform;
                for (int j = i + 1; j < oc.Count; j++)
                {
                    var b = oc[j].transform;

                    if (Mathf.Abs(a.position.y - b.position.y) > 0.05f) continue;

                    if (Vector3.Distance(a.position, b.position) > LoopDistMax) continue;

                    // Facing roughly opposite
                    float dot = Vector3.Dot(a.forward, b.forward);
                    if (dot > LoopAlignDotMin) continue;

                    // Temporarily move A's root to meet B
                    Transform root = a.root;
                    Vector3 oldPos = root.position;
                    Quaternion oldRot = root.rotation;

                    root.rotation = Quaternion.LookRotation(-b.forward, b.up);
                    root.position += (b.position - a.position);

                    bool overlap = isOverlappingExisting(root.gameObject);

                    if (!overlap)
                    {
                        oc.RemoveAt(j);
                        oc.RemoveAt(i);
                        return true;
                    }

                    root.SetPositionAndRotation(oldPos, oldRot);
                }
            }
            return false;
        }

        // randomise starting entries
        List<int> start = new();
        for (int i = 0; i < entries.Count; i++) start.Add(i);
        for (int i = 0; i < start.Count; i++)
        {
            int j = rng.Next(start.Count);
            (start[i], start[j]) = (start[j], start[i]);
        }

        bool successOverall = false;

        foreach (int idx in start)
        {
            foreach (var m in placedModules) UnityEngine.Object.DestroyImmediate(m);
            placedModules.Clear(); openConnectors.Clear();

            var firstPrefab = entries[idx].prefab;
            var first =
                PrefabUtility.InstantiatePrefab(firstPrefab) as GameObject ??
                UnityEngine.Object.Instantiate(firstPrefab);

            first.transform.SetParent(parentObject.transform, true);
            first.transform.position = Vector3.zero;
            first.transform.rotation = Quaternion.identity;
            placedModules.Add(first);
            addOpenConnectorsForModule(first, null);

            if (ExpandDungeon(rng, entries, maxModules, placedModules, openConnectors,
                              addOpenConnectorsForModule, removeOpenConnector,
                              tryPlaceModuleAt, isOverlappingExisting, computeWorldBounds, TryCloseLoops, SelectConnector))
            {
                successOverall = true;
                break;
            }
        }

        if (!successOverall)
        {
            placedModules.Clear();
            lastSolution.Clear();
            return false;
        }

        // capture result for playback
        lastSolution.Clear();
        foreach (var tr in parentObject.GetComponentsInChildren<Transform>(true))
        {
            if (tr == parentObject.transform) continue;
            var root = tr.gameObject;
            if (root.transform.parent == parentObject.transform)
            {
#if UNITY_EDITOR
                var src = PrefabUtility.GetCorrespondingObjectFromSource(root);
                GameObject prefabRef = src as GameObject ?? root;
#else
                GameObject prefabRef = root;
#endif
                lastSolution.Add(new Placement
                {
                    prefab = prefabRef,
                    pos = root.transform.position,
                    rot = root.transform.rotation
                });
            }
        }

        return true;
    }

    private static double nextTime = 0.0;
    private static float playbackDelay = 0.1f;
    private static int playbackIndex = -1;
    private static bool isPlaying = false;

    public static void StartPlayback(float delaySeconds)
    {
        if (lastParentForPlayback == null || lastSolution.Count == 0) return;

        for (int i = lastParentForPlayback.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(lastParentForPlayback.transform.GetChild(i).gameObject);

        playbackDelay = Mathf.Max(0.01f, delaySeconds);
        playbackIndex = 0;
        isPlaying = true;
        nextTime = EditorApplication.timeSinceStartup + playbackDelay;

        EditorApplication.update -= PlaybackTick;
        EditorApplication.update += PlaybackTick;
    }

    private static void PlaybackTick()
    {
        if (!isPlaying) { EditorApplication.update -= PlaybackTick; return; }
        if (EditorApplication.timeSinceStartup < nextTime) return;

        if (playbackIndex >= 0 && playbackIndex < lastSolution.Count)
        {
            var p = lastSolution[playbackIndex++];
            var inst =
#if UNITY_EDITOR
                PrefabUtility.InstantiatePrefab(p.prefab) as GameObject ??
                UnityEngine.Object.Instantiate(p.prefab);
#else
                UnityEngine.Object.Instantiate(p.prefab);
#endif
            inst.transform.SetParent(lastParentForPlayback.transform, true);
            inst.transform.SetPositionAndRotation(p.pos, p.rot);
            nextTime = EditorApplication.timeSinceStartup + playbackDelay;
        }
        else
        {
            isPlaying = false;
            EditorApplication.update -= PlaybackTick;
        }
    }

    public static bool HasSolutionForPlayback() => lastSolution.Count > 0 && lastParentForPlayback != null;

    private static bool ExpandDungeon(
        System.Random rng,
        List<WFCModuleEntry> entries, int maxModules,
        List<GameObject> placedModules, List<OpenConnector> openConnectors,
        Action<GameObject, Transform> addOpenConnectorsForModule,
        Action<Transform> removeOpenConnector,
        Func<GameObject, OpenConnector, GameObject> tryPlaceModuleAt,
        Func<GameObject, bool> isOverlappingExisting,
        Func<GameObject, Bounds> computeWorldBounds,
        Func<List<OpenConnector>, List<GameObject>, bool> tryCloseLoops,
        Func<List<OpenConnector>, List<WFCModuleEntry>, OpenConnector> selectConnector)
    {
        if (placedModules.Count >= maxModules) return true;
        if (openConnectors.Count == 0) return false;

        if (tryCloseLoops(openConnectors, placedModules))
        {
            return ExpandDungeon(rng, entries, maxModules, placedModules, openConnectors,
                                 addOpenConnectorsForModule, removeOpenConnector,
                                 tryPlaceModuleAt, isOverlappingExisting, computeWorldBounds,
                                 tryCloseLoops, selectConnector);
        }

        var bestConnector = selectConnector(openConnectors, entries);
        Transform targetConn = bestConnector.transform;

        List<WFCModuleEntry> viable = new();
        foreach (var e in entries)
        {
            var test = tryPlaceModuleAt(e.prefab, bestConnector);
            if (test != null)
            {
                viable.Add(e);
                UnityEngine.Object.DestroyImmediate(test);
            }
        }
        if (viable.Count == 0) return false;

        List<WFCModuleEntry> nonDead = new();
        List<WFCModuleEntry> dead = new();
        foreach (var e in viable)
        {
            int c = e.GetConnectorCount();
            if (c <= 1) dead.Add(e); else nonDead.Add(e);
        }
        List<WFCModuleEntry> pool = (nonDead.Count > 0) ? nonDead : dead;

        // Weighted pick helper
        WFCModuleEntry PickWeighted(List<WFCModuleEntry> list)
        {
            float total = 0f;
            foreach (var e in list) total += Mathf.Max(0f, e.baseWeight);
            if (total <= 0f) return list[rng.Next(list.Count)];
            float roll = (float)rng.NextDouble() * total;
            foreach (var e in list)
            {
                roll -= Mathf.Max(0f, e.baseWeight);
                if (roll <= 0f) return e;
            }
            return list[list.Count - 1];
        }

        // Remove the connector now that we're attempting to fill it
        openConnectors.Remove(bestConnector);

        var tryList = new List<WFCModuleEntry>(pool);
        while (tryList.Count > 0)
        {
            var pick = PickWeighted(tryList);
            tryList.Remove(pick);

            var newModule = tryPlaceModuleAt(pick.prefab, bestConnector);
            if (newModule == null) continue;

            placedModules.Add(newModule);

            // Identify used connector on the new module
            Transform usedConnector = null;
            foreach (var t in newModule.GetComponentsInChildren<Transform>(true))
                if (t.name == "Connector" &&
                    Vector3.Distance(t.position, targetConn.position) < 0.001f)
                { usedConnector = t; break; }

            addOpenConnectorsForModule(newModule, usedConnector);

            if (ExpandDungeon(rng, entries, maxModules, placedModules, openConnectors,
                              addOpenConnectorsForModule, removeOpenConnector,
                              tryPlaceModuleAt, isOverlappingExisting, computeWorldBounds,
                              tryCloseLoops, selectConnector))
                return true;

            // Backtrack
            foreach (var t in newModule.GetComponentsInChildren<Transform>(true))
                if (t.name == "Connector") removeOpenConnector(t);

            placedModules.Remove(newModule);
            UnityEngine.Object.DestroyImmediate(newModule);
        }

        openConnectors.Add(bestConnector);
        return false;
    }
}
