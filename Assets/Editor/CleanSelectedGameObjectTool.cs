using UnityEngine;
using UnityEditor;

public class CleanSelectedGameObjectTool : EditorWindow
{
    private bool removeBoxCollider2D = true;
    private bool removeBoxCollider = true;
    private bool removeMeshCollider = true;
    private bool removeMissingScripts = true;

    [MenuItem("Tools/Cleanup/GameObject Cleaner")]
    private static void OpenWindow()
    {
        GetWindow<CleanSelectedGameObjectTool>("GameObject Cleaner");
    }

    private void OnGUI()
    {
        GUILayout.Label("Remove Options", EditorStyles.boldLabel);

        removeBoxCollider2D = EditorGUILayout.ToggleLeft("Remove BoxCollider2D", removeBoxCollider2D);
        removeBoxCollider = EditorGUILayout.ToggleLeft("Remove BoxCollider", removeBoxCollider);
        removeMeshCollider = EditorGUILayout.ToggleLeft("Remove MeshCollider", removeMeshCollider);
        removeMissingScripts = EditorGUILayout.ToggleLeft("Remove Missing Scripts", removeMissingScripts);

        GUILayout.Space(10);

        GameObject selected = Selection.activeGameObject;

        EditorGUILayout.LabelField("Selected GameObject", selected != null ? selected.name : "None");

        GUI.enabled = selected != null;

        if (GUILayout.Button("Clean Selected GameObject And Children", GUILayout.Height(32)))
        {
            Clean(selected);
        }

        GUI.enabled = true;
    }

    private void Clean(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        int removedBoxCollider2DCount = 0;
        int removedBoxColliderCount = 0;
        int removedMeshColliderCount = 0;
        int removedMissingScriptCount = 0;

        Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);

        Undo.SetCurrentGroupName("Clean GameObject And Children");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (Transform transform in allTransforms)
        {
            GameObject go = transform.gameObject;

            if (removeBoxCollider2D)
            {
                BoxCollider2D[] colliders = go.GetComponents<BoxCollider2D>();
                foreach (BoxCollider2D collider in colliders)
                {
                    Undo.DestroyObjectImmediate(collider);
                    removedBoxCollider2DCount++;
                }
            }

            if (removeBoxCollider)
            {
                BoxCollider[] colliders = go.GetComponents<BoxCollider>();
                foreach (BoxCollider collider in colliders)
                {
                    Undo.DestroyObjectImmediate(collider);
                    removedBoxColliderCount++;
                }
            }

            if (removeMeshCollider)
            {
                MeshCollider[] colliders = go.GetComponents<MeshCollider>();
                foreach (MeshCollider collider in colliders)
                {
                    Undo.DestroyObjectImmediate(collider);
                    removedMeshColliderCount++;
                }
            }

            if (removeMissingScripts)
            {
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);

                if (missingCount > 0)
                {
                    Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    removedMissingScriptCount += missingCount;
                }
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log(
            $"Clean done on '{root.name}'. " +
            $"Removed BoxCollider2D: {removedBoxCollider2DCount}, " +
            $"BoxCollider: {removedBoxColliderCount}, " +
            $"MeshCollider: {removedMeshColliderCount}, " +
            $"Missing Scripts: {removedMissingScriptCount}"
        );
    }
}