using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class ModelToPNGExporter : EditorWindow
{
    private enum CaptureView
    {
        Isometric,
        Front,
        Back,
        Left,
        Right,
        Top,
        Bottom
    }

    private GameObject targetModel;
    private int resolution = 1024;
    private int previewResolution = 512;
    private string savePath = "Assets/ExportedPNG/";
    private CaptureView selectedView = CaptureView.Isometric;
    private bool transparentBackground = true;
    private bool cropTransparentPadding = true;
    private int cropPaddingPixels = 24;
    private float cameraPaddingPercent = 4f;

    private Texture2D previewTexture;
    private string lastPreviewKey = string.Empty;
    private bool previewQueued;
    private Vector2 scroll;

    [MenuItem("Tools/Export Model To PNG")]
    public static void ShowWindow()
    {
        GetWindow<ModelToPNGExporter>("Model To PNG");
    }

    private void OnDisable()
    {
        ClearPreview();
    }

    private void OnGUI()
    {
        float leftPanelWidth = GetLeftPanelWidth();

        using (new EditorGUILayout.HorizontalScope())
        {
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Width(leftPanelWidth));

            EditorGUILayout.LabelField("Export 3D Model to PNG", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawDropArea();

            EditorGUI.BeginChangeCheck();
            targetModel = (GameObject)EditorGUILayout.ObjectField("Target Model", targetModel, typeof(GameObject), true);
            selectedView = (CaptureView)EditorGUILayout.EnumPopup("Preview / Export View", selectedView);
            resolution = Mathf.Clamp(EditorGUILayout.IntField("Export Resolution", resolution), 64, 8192);
            previewResolution = Mathf.Clamp(EditorGUILayout.IntField("Preview Resolution", previewResolution), 64, 2048);
            savePath = EditorGUILayout.TextField("Save Path", savePath);

            EditorGUILayout.Space(4);
            transparentBackground = EditorGUILayout.Toggle("Transparent Background", transparentBackground);
            cameraPaddingPercent = Mathf.Clamp(EditorGUILayout.FloatField("Camera Padding %", cameraPaddingPercent), 0f, 50f);
            cropTransparentPadding = EditorGUILayout.Toggle("Crop Transparent Edges", cropTransparentPadding);
            using (new EditorGUI.DisabledScope(!cropTransparentPadding))
            {
                cropPaddingPixels = Mathf.Clamp(EditorGUILayout.IntField("Crop Padding Pixels", cropPaddingPixels), 0, 512);
            }

            if (EditorGUI.EndChangeCheck())
            {
                QueuePreviewRefresh();
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Preview"))
                {
                    RefreshPreview();
                }

                using (new EditorGUI.DisabledScope(targetModel == null))
                {
                    if (GUILayout.Button("Export Current View"))
                    {
                        ExportCurrentView();
                    }

                    if (GUILayout.Button("Export All Views"))
                    {
                        ExportAllViews();
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);

            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                DrawPreview();
            }
        }

        AutoRefreshPreviewIfNeeded();
    }

    private float GetLeftPanelWidth()
    {
        return Mathf.Clamp(position.width * 0.42f, 320f, 460f);
    }

    private void DrawDropArea()
    {
        Rect dropRect = GUILayoutUtility.GetRect(0, 68, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "Drag & drop model / prefab here\nPreview will be rendered on the right automatically", EditorStyles.helpBox);

        Event evt = Event.current;
        if (!dropRect.Contains(evt.mousePosition))
            return;

        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            GameObject draggedObject = GetDraggedGameObject();
            DragAndDrop.visualMode = draggedObject != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

            if (evt.type == EventType.DragPerform && draggedObject != null)
            {
                DragAndDrop.AcceptDrag();
                targetModel = draggedObject;
                QueuePreviewRefresh();
            }

            evt.Use();
        }
    }

    private GameObject GetDraggedGameObject()
    {
        foreach (Object obj in DragAndDrop.objectReferences)
        {
            GameObject go = obj as GameObject;
            if (go != null)
                return go;
        }
        return null;
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("Capture Preview", EditorStyles.boldLabel);

        if (targetModel == null)
        {
            EditorGUILayout.HelpBox("Drag a model/prefab into the box above, or assign it in Target Model.", MessageType.Info);
            return;
        }

        if (previewTexture == null)
        {
            EditorGUILayout.HelpBox("No preview yet. Click Refresh Preview if it does not appear automatically.", MessageType.Info);
            return;
        }

        float maxWidth = Mathf.Max(120, position.width - GetLeftPanelWidth() - 42f);
        float aspect = (float)previewTexture.width / Mathf.Max(1, previewTexture.height);
        float height = Mathf.Clamp(maxWidth / aspect, 120, Mathf.Max(120, position.height - 56f));
        Rect rect = GUILayoutUtility.GetRect(maxWidth, height, GUILayout.ExpandWidth(true));
        GUI.DrawTexture(rect, previewTexture, ScaleMode.ScaleToFit, true);
    }

    private void AutoRefreshPreviewIfNeeded()
    {
        if (targetModel == null)
            return;

        string key = BuildPreviewKey();
        if (key != lastPreviewKey)
            QueuePreviewRefresh();
    }

    private string BuildPreviewKey()
    {
        int id = targetModel != null ? targetModel.GetInstanceID() : 0;
        return id + "|" + selectedView + "|" + previewResolution + "|" + transparentBackground + "|" + cropTransparentPadding + "|" + cropPaddingPixels + "|" + cameraPaddingPercent;
    }

    private void QueuePreviewRefresh()
    {
        if (previewQueued)
            return;

        previewQueued = true;
        EditorApplication.delayCall += () =>
        {
            previewQueued = false;
            if (this != null)
                RefreshPreview();
        };
    }

    private void RefreshPreview()
    {
        if (targetModel == null)
            return;

        ClearPreview();
        previewTexture = RenderModel(selectedView, previewResolution, true);
        lastPreviewKey = BuildPreviewKey();
        Repaint();
    }

    private void ClearPreview()
    {
        if (previewTexture != null)
        {
            DestroyImmediate(previewTexture);
            previewTexture = null;
        }
    }

    private void ExportCurrentView()
    {
        if (!ValidateTarget())
            return;

        EnsureDirectory(savePath);
        Texture2D tex = RenderModel(selectedView, resolution, true);
        string filePath = Path.Combine(savePath, targetModel.name + "_" + selectedView.ToString().ToLowerInvariant() + ".png");
        File.WriteAllBytes(filePath, tex.EncodeToPNG());
        DestroyImmediate(tex);
        RefreshAssetsIfNeeded(filePath);
        Debug.Log("Exported PNG: " + filePath);
    }

    private void ExportAllViews()
    {
        if (!ValidateTarget())
            return;

        EnsureDirectory(savePath);
        int exported = 0;
        foreach (CaptureView view in System.Enum.GetValues(typeof(CaptureView)))
        {
            Texture2D tex = RenderModel(view, resolution, true);
            string filePath = Path.Combine(savePath, targetModel.name + "_" + view.ToString().ToLowerInvariant() + ".png");
            File.WriteAllBytes(filePath, tex.EncodeToPNG());
            DestroyImmediate(tex);
            exported++;
        }

        AssetDatabase.Refresh();
        Debug.Log("Exported " + exported + " PNG views to: " + savePath);
    }

    private bool ValidateTarget()
    {
        if (targetModel != null)
            return true;

        EditorUtility.DisplayDialog("Error", "Please assign or drag & drop a model first.", "OK");
        return false;
    }

    private Texture2D RenderModel(CaptureView view, int size, bool allowCrop)
    {
        GameObject modelInstance = null;
        GameObject camGO = null;
        GameObject lightGO = null;
        RenderTexture rt = null;
        RenderTexture oldRT = RenderTexture.active;

        try
        {
            modelInstance = Instantiate(targetModel);
            modelInstance.name = targetModel.name + "_PNGRenderInstance";
            modelInstance.hideFlags = HideFlags.HideAndDontSave;
            ResetTransform(modelInstance.transform);

            Bounds bounds;
            if (!TryCalculateBounds(modelInstance, out bounds))
                throw new System.Exception("The selected model has no Renderer components.");

            camGO = new GameObject("Temp_PNG_Camera");
            camGO.hideFlags = HideFlags.HideAndDontSave;
            Camera cam = camGO.AddComponent<Camera>();
            cam.clearFlags = transparentBackground ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            cam.backgroundColor = transparentBackground ? new Color(0f, 0f, 0f, 0f) : Color.gray;
            cam.orthographic = true;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 10000f;
            cam.allowHDR = false;
            cam.allowMSAA = true;

            Vector3 direction = GetViewDirection(view).normalized;
            Vector3 up = GetViewUp(view);
            Quaternion camRotation = Quaternion.LookRotation(direction, up);
            FitCameraToBounds(cam, bounds, camRotation, size, size);

            lightGO = new GameObject("Temp_PNG_Light");
            lightGO.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Light light = lightGO.AddComponent<UnityEngine.Light>();
            light.type = UnityEngine.LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.LookRotation(direction, up);

            rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 8;
            cam.targetTexture = rt;

            RenderTexture.active = rt;
            GL.Clear(true, true, cam.backgroundColor);
            cam.Render();

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply(false, false);

            if (allowCrop && transparentBackground && cropTransparentPadding)
            {
                Texture2D cropped = CropTransparentEdges(tex, cropPaddingPixels);
                if (cropped != tex)
                    DestroyImmediate(tex);
                tex = cropped;
            }

            return tex;
        }
        finally
        {
            RenderTexture.active = oldRT;
            if (rt != null)
                DestroyImmediate(rt);
            if (camGO != null)
                DestroyImmediate(camGO);
            if (lightGO != null)
                DestroyImmediate(lightGO);
            if (modelInstance != null)
                DestroyImmediate(modelInstance);
        }
    }

    private Vector3 GetViewDirection(CaptureView view)
    {
        switch (view)
        {
            case CaptureView.Front: return Vector3.back;
            case CaptureView.Back: return Vector3.forward;
            case CaptureView.Left: return Vector3.right;
            case CaptureView.Right: return Vector3.left;
            case CaptureView.Top: return Vector3.down;
            case CaptureView.Bottom: return Vector3.up;
            default: return new Vector3(-0.65f, -0.35f, -0.68f);
        }
    }

    private Vector3 GetViewUp(CaptureView view)
    {
        if (view == CaptureView.Top || view == CaptureView.Bottom)
            return Vector3.forward;

        return Vector3.up;
    }

    private void FitCameraToBounds(Camera cam, Bounds bounds, Quaternion rotation, int width, int height)
    {
        Vector3[] corners = GetBoundsCorners(bounds);
        Quaternion inv = Quaternion.Inverse(rotation);

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = inv * (corners[i] - bounds.center);
            minX = Mathf.Min(minX, local.x);
            maxX = Mathf.Max(maxX, local.x);
            minY = Mathf.Min(minY, local.y);
            maxY = Mathf.Max(maxY, local.y);
            minZ = Mathf.Min(minZ, local.z);
            maxZ = Mathf.Max(maxZ, local.z);
        }

        float aspect = width / (float)height;
        float halfHeight = Mathf.Max((maxY - minY) * 0.5f, ((maxX - minX) * 0.5f) / aspect);
        cam.orthographicSize = Mathf.Max(0.001f, halfHeight * (1f + cameraPaddingPercent / 100f));

        float depth = Mathf.Max(1f, maxZ - minZ);
        Vector3 forward = rotation * Vector3.forward;
        cam.transform.rotation = rotation;
        cam.transform.position = bounds.center - forward * (depth + bounds.extents.magnitude + 2f);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = depth + bounds.extents.magnitude * 4f + 10f;
    }

    private Texture2D CropTransparentEdges(Texture2D source, int padding)
    {
        Color32[] pixels = source.GetPixels32();
        int width = source.width;
        int height = source.height;
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (pixels[row + x].a > 4)
                {
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
        }

        if (maxX < minX || maxY < minY)
            return source;

        minX = Mathf.Max(0, minX - padding);
        minY = Mathf.Max(0, minY - padding);
        maxX = Mathf.Min(width - 1, maxX + padding);
        maxY = Mathf.Min(height - 1, maxY + padding);

        int newWidth = maxX - minX + 1;
        int newHeight = maxY - minY + 1;
        if (newWidth == width && newHeight == height)
            return source;

        Texture2D cropped = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
        Color32[] croppedPixels = new Color32[newWidth * newHeight];
        for (int y = 0; y < newHeight; y++)
        {
            System.Array.Copy(pixels, (minY + y) * width + minX, croppedPixels, y * newWidth, newWidth);
        }
        cropped.SetPixels32(croppedPixels);
        cropped.Apply(false, false);
        return cropped;
    }

    private Vector3[] GetBoundsCorners(Bounds b)
    {
        Vector3 min = b.min;
        Vector3 max = b.max;
        return new Vector3[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, max.y, max.z)
        };
    }

    private bool TryCalculateBounds(GameObject obj, out Bounds bounds)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        bounds = new Bounds(obj.transform.position, Vector3.zero);
        bool hasBounds = false;

        foreach (Renderer r in renderers)
        {
            if (!r.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return hasBounds;
    }

    private void ResetTransform(Transform t)
    {
        // Keep the model's authored rotation and scale, only move the temporary clone away from the scene.
        t.position = Vector3.zero;
    }

    private void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    private void RefreshAssetsIfNeeded(string filePath)
    {
        if (filePath.Replace("\\", "/").StartsWith("Assets/"))
            AssetDatabase.Refresh();
    }
}
