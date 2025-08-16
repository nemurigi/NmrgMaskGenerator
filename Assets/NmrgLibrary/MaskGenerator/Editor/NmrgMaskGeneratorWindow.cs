using UnityEngine;
using UnityEditor;
using System.IO;

namespace NmrgLibrary.NmrgMaskGenerator
{
    public class NmrgMaskGeneratorWindow : EditorWindow
    {
        private NmrgMaskGenerator maskGenerator;
        private GameObject targetObject;
        private SelectionMode selectionMode = SelectionMode.Add;
        private int textureResolution = 512;
        private Texture2D previewTexture;
        private ValidationResult validationResult = ValidationResult.Valid;
        private Mesh problematicMesh = null;
        private int paddingSize = 2;
        
        private readonly int[] resolutionOptions = { 256, 512, 1024, 2048, 4096 };
        private readonly string[] resolutionLabels = { "256", "512", "1024", "2048", "4096" };
        
        [MenuItem("GameObject/Nmrg Mask Generator", false, 0)]
        public static void ShowWindow()
        {
            var selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a GameObject with a MeshFilter component.", "OK");
                return;
            }
            
            var window = GetWindow<NmrgMaskGeneratorWindow>("Nmrg Mask Generator");
            window.SetTarget(selectedObject);
            window.Show();
        }
        
        private void SetTarget(GameObject target)
        {
            targetObject = target;
            validationResult = ValidationResult.Valid;
            problematicMesh = null;
            
            if (maskGenerator != null)
            {
                maskGenerator.OnVertexMaskChanged -= OnVertexMaskChanged;
                maskGenerator.Dispose();
                maskGenerator = null;
            }
            
            validationResult = NmrgMaskGenerator.ValidateGameObject(target, out problematicMesh);
            if (validationResult != ValidationResult.Valid)
            {
                return; // 検証エラー
            }
            
            try
            {
                maskGenerator = new NmrgMaskGenerator(targetObject);
                maskGenerator.OnVertexMaskChanged += OnVertexMaskChanged;
                SceneView.duringSceneGui += OnSceneGUI;
                
                // 初期Previewを生成
                GeneratePreview();
            }
            catch (System.ArgumentException ex)
            {
                EditorUtility.DisplayDialog("Error", ex.Message, "OK");
                maskGenerator = null;
            }
        }
        
        private void OnDestroy()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            
            if (maskGenerator != null)
            {
                maskGenerator.OnVertexMaskChanged -= OnVertexMaskChanged;
                maskGenerator.Dispose();
            }
            
            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
            }
        }
        
        private void OnGUI()
        {
            if (targetObject == null)
            {
                EditorGUILayout.HelpBox("No target object selected. Please select a GameObject with MeshFilter and try again.", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.LabelField("Target Object", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("GameObject", targetObject, typeof(GameObject), true);
            EditorGUI.EndDisabledGroup();
            
            // 検証結果に基づくエラー表示
            bool hasValidationIssue = validationResult != ValidationResult.Valid;
            
            if (hasValidationIssue)
            {
                EditorGUILayout.Space();
                
                switch (validationResult)
                {
                    case ValidationResult.NoMeshComponent:
                        EditorGUILayout.HelpBox("GameObject must have MeshFilter+MeshRenderer or SkinnedMeshRenderer components.", MessageType.Error);
                        break;
                        
                    case ValidationResult.NoMesh:
                        EditorGUILayout.HelpBox("Mesh component exists but no mesh is assigned.", MessageType.Error);
                        break;
                        
                    case ValidationResult.MeshNotReadable:
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.HelpBox("Read/Write must be enabled in import settings.", MessageType.Error);
                        
                        if (GUILayout.Button("Auto Fix", GUILayout.Width(80), GUILayout.Height(38)))
                        {
                            FixMeshReadability();
                        }
                        EditorGUILayout.EndHorizontal();
                        break;
                }
                
                EditorGUILayout.Space();
            }
            
            // 以下のGUIを検証エラーがある場合はDisable状態で表示
            EditorGUI.BeginDisabledGroup(hasValidationIssue);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Selection Mode", EditorStyles.boldLabel);
            selectionMode = (SelectionMode)EditorGUILayout.EnumPopup("Mode", selectionMode);
            
            if (maskGenerator != null)
            {
                EditorGUI.BeginChangeCheck();
                var newColor = EditorGUILayout.ColorField("Overlay Color", maskGenerator.OverlayColor);
                if (EditorGUI.EndChangeCheck())
                {
                    maskGenerator.OverlayColor = newColor;
                    SceneView.RepaintAll();
                }
                
                EditorGUI.BeginChangeCheck();
                var newWidth = EditorGUILayout.Slider("Width", maskGenerator.WireframeWidth, 0.1f, 5.0f);
                if (EditorGUI.EndChangeCheck())
                {
                    maskGenerator.WireframeWidth = newWidth;
                    SceneView.RepaintAll();
                }
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Operations", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear"))
            {
                if (maskGenerator != null)
                {
                    maskGenerator.ClearMask();
                    SceneView.RepaintAll();
                }
            }
            
            if (GUILayout.Button("Invert"))
            {
                if (maskGenerator != null)
                {
                    maskGenerator.InvertMask();
                    SceneView.RepaintAll();
                }
            }
            
            if (GUILayout.Button("Select All"))
            {
                if (maskGenerator != null)
                {
                    maskGenerator.SelectAllMask();
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Texture Generation", EditorStyles.boldLabel);
            
            int selectedIndex = System.Array.IndexOf(resolutionOptions, textureResolution);
            if (selectedIndex == -1) selectedIndex = 1;
            
            selectedIndex = EditorGUILayout.Popup("Resolution", selectedIndex, resolutionLabels);
            textureResolution = resolutionOptions[selectedIndex];
            
            paddingSize = EditorGUILayout.IntSlider("Padding", paddingSize, 0, 16);
            
            // EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Update Preview"))
            {
                GeneratePreview();
            }
            
            EditorGUI.BeginDisabledGroup(maskGenerator == null);
            if (GUILayout.Button("Save"))
            {
                SaveTexture();
            }
            EditorGUI.EndDisabledGroup();
            // EditorGUILayout.EndHorizontal();
            
            if (previewTexture != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                
                // Window幅に基づいてPreviewサイズを計算（最大256px、最小128px）
                float windowWidth = EditorGUIUtility.currentViewWidth;
                float maxPreviewSize = Mathf.Min(windowWidth - 40f, 512f); // 余白を考慮
                float previewSize = Mathf.Max(maxPreviewSize, 128f);
                
                // 中央寄せのためにHorizontalレイアウトを使用
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace(); // 左側の余白
                var rect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(rect, previewTexture);
                GUILayout.FlexibleSpace(); // 右側の余白
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Click on mesh polygons in Scene view to select islands.\nHold Shift to add, Ctrl to remove.", MessageType.Info);
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (maskGenerator == null || targetObject == null) return;
            
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            
            Event current = Event.current;
            
            if (current.type == EventType.MouseDown && current.button == 0)
            {
                var mode = selectionMode;
                
                if (current.shift)
                    mode = SelectionMode.Add;
                else if (current.control)
                    mode = SelectionMode.Remove;
                
                var ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
                
                if (maskGenerator.TrySelectTriangle(ray, mode, out bool[] newMask))
                {
                    Debug.Log("Select");
                    maskGenerator.VertexMask = newMask;
                    current.Use();
                    sceneView.Repaint();
                }
                else
                {
                    Debug.Log("No selection");
                }
            }
            
            if (current.type == EventType.Repaint)
            {
                maskGenerator.DrawOverlay();
            }
        }
        
        private void GeneratePreview()
        {
            if (maskGenerator == null) return;
            
            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
            }
            
            previewTexture = maskGenerator.GenerateMaskTexture(textureResolution, paddingSize);
            Repaint();
        }
        
        private void FixMeshReadability()
        {
            if (problematicMesh == null) return;
            
            if (!MeshAssetValidator.CanFixMeshReadability(problematicMesh))
            {
                EditorUtility.DisplayDialog("Error", "Cannot fix mesh readability. The mesh may not be an imported asset.", "OK");
                return;
            }
            
            bool confirmed = EditorUtility.DisplayDialog(
                "Fix Mesh Readability", 
                $"This will enable Read/Write for mesh '{problematicMesh.name}' and reimport the asset. Continue?", 
                "Yes", "Cancel"
            );
            
            if (!confirmed) return;
            
            if (MeshAssetValidator.FixMeshReadability(problematicMesh))
            {
                EditorUtility.DisplayDialog("Success", "Mesh readability fixed successfully!", "OK");
                // ターゲットを再設定してMaskGeneratorを再初期化
                SetTarget(targetObject);
                Repaint();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Failed to fix mesh readability. Check the console for details.", "OK");
            }
        }
        
        private void SaveTexture()
        {
            if (maskGenerator == null) return;
            
            string path = EditorUtility.SaveFilePanel(
                "Save Mask Texture",
                "Assets",
                targetObject.name + "_mask",
                "png"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                if (maskGenerator.SaveMaskTexture(path, textureResolution, paddingSize))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        AssetDatabase.Refresh();
                    }
                    EditorUtility.DisplayDialog("Success", "Mask texture saved successfully!", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Failed to save mask texture!", "OK");
                }
            }
        }
        
        private void OnVertexMaskChanged()
        {
            GeneratePreview();
        }
    }
}