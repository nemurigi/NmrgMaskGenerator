using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Object = UnityEngine.Object;

namespace NmrgLibrary.NmrgMaskGenerator
{
    public class NmrgMaskGenerator
    {
        private MeshInfo _meshInfo;
        private bool[] _maskAttribute;
        private Mesh _workingMesh;
        private GameObject _proxyObj;
        private MeshCollider _proxyCollider;
        private MeshRenderer _previewRenderer;
        private Transform _targetTransform;
        private RenderTexture _maskRenderTexture;
        private Material _overlayMaterial;
        private MaterialPropertyBlock _propertyBlock;
        private Color _overlayColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);
        private float _wireframeWidth = 1.0f;
        
        public event System.Action OnVertexMaskChanged;
        
        public MeshInfo MeshInfo => _meshInfo;
        public bool[] VertexMask 
        { 
            get => _maskAttribute; 
            set 
            {
                if (value == null)
                    throw new System.ArgumentNullException(nameof(value));
                if (value.Length != _meshInfo.vertexCount)
                    throw new System.ArgumentException($"Mask length must be {_meshInfo.vertexCount}, got {value.Length}");
                _maskAttribute = value;
                DrawOverlay();
                OnVertexMaskChanged?.Invoke();
            }
        }

        public Color OverlayColor 
        { 
            get => _overlayColor; 
            set 
            {
                _overlayColor = value;
                // 色変更時に自動的にオーバーレイを更新
                if (_previewRenderer != null && _previewRenderer.enabled)
                {
                    UpdateOverlayProperties();
                }
            }
        }

        public float WireframeWidth 
        { 
            get => _wireframeWidth; 
            set 
            {
                _wireframeWidth = value;
                // 線幅変更時に自動的にオーバーレイを更新
                if (_previewRenderer != null && _previewRenderer.enabled)
                {
                    UpdateOverlayProperties();
                }
            }
        }
        
        public NmrgMaskGenerator(GameObject target)
        {
            if (target == null)
            {
                throw new System.ArgumentNullException(nameof(target));
            }
                
            var meshFilter = target.GetComponent<MeshFilter>();
            var meshRenderer = target.GetComponent<MeshRenderer>();
            var skinnedMeshRenderer = target.GetComponent<SkinnedMeshRenderer>();
            
            Mesh mesh = null;
            if (meshFilter != null && meshRenderer != null)
            {
                mesh = meshFilter.sharedMesh;
            }
            else if (skinnedMeshRenderer != null)
            {
                mesh = new Mesh();
                skinnedMeshRenderer.BakeMesh(mesh);
            }
            else
            {
                throw new System.ArgumentException("Target object must have MeshRenderer or SkinnedMeshRenderer components", nameof(target));
            }
            
            if (mesh == null)
            {
                throw new System.ArgumentException("Target does not contain a valid mesh", nameof(target));
            }
            
            _meshInfo = new MeshInfo(mesh);
            _maskAttribute = new bool[_meshInfo.vertexCount];
            _workingMesh = _meshInfo.ToUnityMesh();
            _targetTransform = target.transform;
            _propertyBlock = new MaterialPropertyBlock();
            
            _proxyObj = new GameObject("NmrgMaskGeneratorProxy");
            _proxyObj.hideFlags = HideFlags.HideAndDontSave;
            var proxyMeshFilter = _proxyObj.AddComponent<MeshFilter>();
            proxyMeshFilter.mesh = _workingMesh;
            _proxyCollider = _proxyObj.AddComponent<MeshCollider>();
            _proxyCollider.convex = false;
            _proxyCollider.sharedMesh = _workingMesh;
            _overlayMaterial = new Material(Shader.Find("NmrgLibrary/NmrgMaskGenerator/SceneOverlay"));
            _previewRenderer = _proxyObj.AddComponent<MeshRenderer>();
            _previewRenderer.material = _overlayMaterial;
            _proxyObj.transform.SetPositionAndRotation(
                _targetTransform.position, 
                _targetTransform.rotation
            );
            _proxyObj.transform.localScale = _targetTransform.lossyScale;
            
        }
        
        public bool TrySelectTriangle(Ray ray, SelectionMode mode, out bool[] newMask)
        {
            newMask = null;
            if (_proxyCollider == null) return false;
            
            if (_proxyCollider.Raycast(ray, out RaycastHit hit, float.MaxValue))
            {
                int triangleIndex = hit.triangleIndex;
                int islandId = _meshInfo.islandIds[triangleIndex];
                
                newMask = GetIslandMask(islandId, mode, _maskAttribute);
                return true;
            }
            
            return false;
        }
        
        private bool[] GetIslandMask(int islandId, SelectionMode mode, bool[] currentMask)
        {
            if (currentMask == null)
                throw new System.ArgumentNullException(nameof(currentMask));
            if (currentMask.Length != _meshInfo.vertexCount)
                throw new System.ArgumentException($"Mask length must be {_meshInfo.vertexCount}, got {currentMask.Length}");
                
            var newMask = new bool[currentMask.Length];
            System.Array.Copy(currentMask, newMask, currentMask.Length);
            
            for (int triIndex = 0; triIndex < _meshInfo.islandIds.Length; triIndex++)
            {
                if (_meshInfo.islandIds[triIndex] == islandId)
                {
                    for (int vertIdx = 0; vertIdx < 3; vertIdx++)
                    {
                        int vertex = _meshInfo.triangles[triIndex * 3 + vertIdx];
                        
                        switch (mode)
                        {
                            case SelectionMode.Add:
                                newMask[vertex] = true;
                                break;
                            case SelectionMode.Remove:
                                newMask[vertex] = false;
                                break;
                        }
                    }
                }
            }
            
            return newMask;
        }
        
        private bool[] GetClearedMask()
        {
            return new bool[_meshInfo.vertexCount];
        }
        
        private bool[] GetInvertedMask(bool[] mask)
        {
            if (mask == null)
                throw new System.ArgumentNullException(nameof(mask));
            if (mask.Length != _meshInfo.vertexCount)
                throw new System.ArgumentException($"Mask length must be {_meshInfo.vertexCount}, got {mask.Length}");
                
            var newMask = new bool[mask.Length];
            for (int i = 0; i < mask.Length; i++)
            {
                newMask[i] = !mask[i];
            }
            return newMask;
        }
        
        private bool[] GetFullMask()
        {
            var mask = new bool[_meshInfo.vertexCount];
            for (int i = 0; i < mask.Length; i++)
            {
                mask[i] = true;
            }
            return mask;
        }
        
        public void DrawOverlay()
        {
            if (_workingMesh == null || _previewRenderer == null || _proxyObj == null) 
                return;
            
            // マスク情報のみを頂点カラーで渡す
            var colors = _maskAttribute.Select(mask => mask 
                ? new Color32(255, 255, 255, 255)  // 白 = マスクあり
                : new Color32(0, 0, 0, 0)).ToArray();  // 黒 = マスクなし
            _workingMesh.colors32 = colors;
            
            // 色情報と線幅を更新
            UpdateOverlayProperties();
            _previewRenderer.enabled = true;
        }
        
        private void UpdateOverlayProperties()
        {
            if (_previewRenderer == null)
            {
                return;
            }
            
            // 色情報と線幅情報をマテリアルプロパティで渡す
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }
            _propertyBlock.SetColor("_OverlayColor", _overlayColor);
            _propertyBlock.SetFloat("_WireframeWidth", _wireframeWidth);
            _previewRenderer.SetPropertyBlock(_propertyBlock);
        }
        
        public void HideOverlay()
        {
            if (_previewRenderer != null)
            {
                _previewRenderer.enabled = false;
            }
        }
        
        public Texture2D GenerateMaskTexture(int resolution, int paddingSize)
        {
            if (paddingSize > 0)
            {
                var maskTexture = GenerateMaskTexture(resolution);
                var paddedTexture = ApplyPadding(maskTexture, paddingSize);
                Object.DestroyImmediate(maskTexture);
                return paddedTexture;
            }
            else
            {
                return GenerateMaskTexture(resolution);
            }
        }
        
        private Texture2D GenerateMaskTexture(int resolution)
        {
            if (_maskRenderTexture != null)
            {
                RenderTexture.ReleaseTemporary(_maskRenderTexture);
            }
            
            // 現在のRenderTextureを保存
            var oldRT = RenderTexture.active;
            
            _maskRenderTexture = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGB32);
            
            // マスク生成専用シェーダーを使用
            var shader = Shader.Find("NmrgLibrary/NmrgMaskGenerator/MaskTextureGenerator");
            var maskRenderMaterial = new Material(shader);
            // マスクテクスチャ生成用の一時メッシュを作成
            var tempMesh = Object.Instantiate(_workingMesh);
            // 頂点カラーでマスク情報を設定
            var colors = _maskAttribute.Select(mask => mask 
                ? new Color32(255, 0, 0, 255)
                : new Color32(0, 0, 0, 255)).ToArray();
            tempMesh.colors32 = colors;
            
            try
            {
                // レンダーターゲット設定
                Graphics.SetRenderTarget(_maskRenderTexture);
                GL.Clear(true, true, Color.black);
                
                // マテリアルのパス設定
                maskRenderMaterial.SetPass(0);
                
                // UV座標用の行列設定
                var viewMatrix = Matrix4x4.identity;
                var projMatrix = Matrix4x4.Ortho(-1, 1, -1, 1, -1, 1);
                
                GL.PushMatrix();
                GL.LoadProjectionMatrix(projMatrix);
                GL.modelview = viewMatrix;
                
                // 一時メッシュを描画
                Graphics.DrawMeshNow(tempMesh, Matrix4x4.identity);
                
                GL.PopMatrix();
                
                Debug.Log("DrawMeshNow completed");
            }
            finally
            {
                // 一時メッシュを破棄
                Object.DestroyImmediate(tempMesh);
                
                // RenderTextureを復元
                RenderTexture.active = oldRT;
            }
            
            // テクスチャ読み取り
            RenderTexture.active = _maskRenderTexture;
            var texture = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false);
            texture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            texture.Apply();
            RenderTexture.active = oldRT;
            
            Debug.Log("Texture generation completed");
            
            Object.DestroyImmediate(maskRenderMaterial);
            
            return texture;
        }
        
        private Texture2D ApplyPadding(Texture2D sourceTexture, int paddingSize)
        {
            var paddingShader = Shader.Find("NmrgLibrary/NmrgMaskGenerator/MaskPadding");
            if (paddingShader == null)
            {
                Debug.LogError("MaskPadding shader not found! Returning original texture.");
                return sourceTexture;
            }
            
            var paddingMaterial = new Material(paddingShader);
            
            var rt1 = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32);
            var rt2 = RenderTexture.GetTemporary(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGB32);
            
            // 初期テクスチャをrt1にコピー
            Graphics.Blit(sourceTexture, rt1);
            
            // paddingSize回のdilation処理
            for (int i = 0; i < paddingSize; i++)
            {
                Graphics.Blit(rt1, rt2, paddingMaterial);
                
                // RenderTarget入れ替え
                var temp = rt1;
                rt1 = rt2;
                rt2 = temp;
            }
            
            // 最終結果をTexture2Dに変換
            var oldRT = RenderTexture.active;
            RenderTexture.active = rt1;
            
            var result = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.ARGB32, false);
            result.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0);
            result.Apply();
            
            RenderTexture.active = oldRT;
            
            // クリーンアップ
            RenderTexture.ReleaseTemporary(rt1);
            RenderTexture.ReleaseTemporary(rt2);
            Object.DestroyImmediate(paddingMaterial);
            
            Debug.Log($"Padding applied: {paddingSize} iterations");
            
            return result;
        }
        
        public static ValidationResult ValidateGameObject(GameObject target, out Mesh problematicMesh)
        {
            problematicMesh = null;
            if (target == null) 
                return ValidationResult.NoMeshComponent;
            
            var meshFilter = target.GetComponent<MeshFilter>();
            var skinnedMeshRenderer = target.GetComponent<SkinnedMeshRenderer>();
            
            // メッシュコンポーネントの存在確認
            if (meshFilter == null && skinnedMeshRenderer == null)
                return ValidationResult.NoMeshComponent;
            
            // メッシュの取得
            Mesh mesh = null;
            if (meshFilter != null)
                mesh = meshFilter.sharedMesh;
            else if (skinnedMeshRenderer != null)
                mesh = skinnedMeshRenderer.sharedMesh;
            
            // メッシュの存在確認
            if (mesh == null) 
                return ValidationResult.NoMesh;
            
            // メッシュの読み取り可能性確認
            if (!MeshAssetValidator.IsMeshReadable(mesh))
            {
                problematicMesh = mesh;
                return ValidationResult.MeshNotReadable;
            }
            
            return ValidationResult.Valid;
        }
        
        public bool SaveMaskTexture(string filePath, int resolution, int paddingSize)
        {
            try 
            {
                var texture = GenerateMaskTexture(resolution, paddingSize);
                var bytes = texture.EncodeToPNG();
                Object.DestroyImmediate(texture);
                
                File.WriteAllBytes(filePath, bytes);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to save mask texture: {ex.Message}");
                return false;
            }
        }
        
        public void ClearMask() => VertexMask = GetClearedMask();
        public void InvertMask() => VertexMask = GetInvertedMask(VertexMask);
        public void SelectAllMask() => VertexMask = GetFullMask();
        
        public void Dispose()
        {
            if (_proxyObj != null)
            {
                Object.DestroyImmediate(_proxyObj);
            }
            
            if (_workingMesh != null)
            {
                Object.DestroyImmediate(_workingMesh);
            }
            
            if (_overlayMaterial != null)
            {
                Object.DestroyImmediate(_overlayMaterial);
            }
            
            if (_maskRenderTexture != null)
            {
                RenderTexture.ReleaseTemporary(_maskRenderTexture);
            }
        }
    }
    
    public enum SelectionMode
    {
        Add,
        Remove
    }
    
    public enum ValidationResult
    {
        Valid,
        NoMeshComponent,
        NoMesh,
        MeshNotReadable
    }
}