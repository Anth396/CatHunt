#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds the "Wall" tag and gives the Building prefab one-way edge colliders along its two outer sides.
/// Each edge only blocks things coming from outside the building (a platform effector with its "up"
/// turned outwards), so the cat can touch and cling to the outline but is never stopped from inside.
/// Safe to run again.
/// </summary>
public static class WallEdgeBuilder
{
    const string PrefabPath = "Assets/Prefabs/SO/Building.prefab";
    public const string WallTag = "Wall";

    [MenuItem("Tools/CatHunt/Build Building Wall Edges")]
    public static string Build()
    {
        StringBuilder log = new StringBuilder();
        EnsureTag(WallTag);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            // The first version used one object with two colliders; replace it
            Transform old = root.transform.Find("WallEdge");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // The building's outline: everything drawn by the building, not counting the wall edges
            Bounds bounds = new Bounds();
            bool any = false;
            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.transform.name.StartsWith("WallEdge")) continue;
                if (!any) { bounds = sr.bounds; any = true; }
                else bounds.Encapsulate(sr.bounds);
            }
            if (!any) return "no sprites found in the building prefab";

            MakeSide(root, "WallEdgeLeft", bounds.min.x, bounds, 90f, log);
            MakeSide(root, "WallEdgeRight", bounds.max.x, bounds, -90f, log);
            log.AppendLine("building bounds min=" + bounds.min + " max=" + bounds.max);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return log.ToString();
    }

    // outwardRotation: 90 turns the effector's "up" to face left, -90 to face right
    static void MakeSide(GameObject root, string name, float x, Bounds bounds, float outwardRotation, StringBuilder log)
    {
        Transform existing = root.transform.Find(name);
        GameObject side = existing != null ? existing.gameObject : new GameObject(name);
        side.transform.SetParent(root.transform, false);
        side.transform.localPosition = Vector3.zero;
        side.transform.localRotation = Quaternion.identity;
        side.transform.localScale = Vector3.one;
        side.tag = WallTag;

        Vector2 bottom = side.transform.InverseTransformPoint(new Vector3(x, bounds.min.y, 0f));
        Vector2 top = side.transform.InverseTransformPoint(new Vector3(x, bounds.max.y, 0f));

        EdgeCollider2D edge = side.GetComponent<EdgeCollider2D>();
        if (edge == null) edge = side.AddComponent<EdgeCollider2D>();
        edge.points = new[] { bottom, top };
        edge.edgeRadius = 0f;
        edge.usedByEffector = true;

        PassableWhenGrounded marker = side.GetComponent<PassableWhenGrounded>();
        if (marker == null) marker = side.AddComponent<PassableWhenGrounded>();
        marker.outwardX = outwardRotation > 0f ? -1 : 1;     // rotation 90 faces left, -90 faces right

        PlatformEffector2D effector = side.GetComponent<PlatformEffector2D>();
        if (effector == null) effector = side.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 170f;
        effector.useSideFriction = false;
        effector.useSideBounce = false;
        effector.rotationalOffset = outwardRotation;

        log.AppendLine(name + ": " + bottom + " -> " + top);
    }

    static void EnsureTag(string tag)
    {
        foreach (string t in UnityEditorInternal.InternalEditorUtility.tags)
        {
            if (t == tag) return;
        }
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }
}
#endif
