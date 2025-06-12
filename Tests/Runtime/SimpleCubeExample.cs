using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;

namespace UltraSaveSystem.Tests.Runtime
{
    [Saveable("gpu_cube_ocean", true)]
    public class SimpleCubeExample : MonoBehaviour
    {
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Colors = Shader.PropertyToID("_Colors");

        [Header("Grid de Cubos")]
        [SaveField] public int gridWidth = 64;
        [SaveField] public int gridHeight = 64;
        [SaveField] public float spacing = 1f;
        [SaveField] public float waveSpeed = 1f;
        [SaveField] public float waveHeight = 1f;
        [SaveField] public float rotationSpeed = 30f;
        [SaveField] public int colorVariations = 1;
        [SaveField] public Color baseColor = Color.cyan;
        
        [Header("Rendering")]
        public Material cubeMaterial;
        public Mesh cubeMesh;
        
        private NativeArray<Matrix4x4> matrices;
        private NativeArray<Vector4> colors;
        private NativeArray<float3> basePositions;
        private JobHandle jobHandle;
        private float time;
        private bool showGUI = true;
        private Vector2 scrollPosition;
        private MaterialPropertyBlock materialProperties;
        private int previousArraySize;
        
        private Color[] colorPalette = {
            Color.cyan, Color.blue, new Color(0, 0.5f, 1f),
            new Color(0.2f, 0.8f, 1f), new Color(0.5f, 0.9f, 1f)
        };
        
        private void Start()
        {
            this.SetupDefaultCallbacks(onAfterLoad: RefreshOcean);
            
            SetupMaterialAndMesh();
            InitializeOcean();
        }
        
        private void SetupMaterialAndMesh()
        {
            if (cubeMesh == null)
            {
                var cubeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubeMesh = cubeGO.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(cubeGO);
            }
            
            if (cubeMaterial == null)
            {
                cubeMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                cubeMaterial.SetFloat(Smoothness, 0f);
                cubeMaterial.SetFloat(Metallic, 0f);
                cubeMaterial.enableInstancing = true;
                
                cubeMaterial.SetColor(BaseColor, baseColor);
            }
            
            materialProperties = new MaterialPropertyBlock();
        }
        
        private void Update()
        {
            HandleInput();
            UpdateOcean();
            UpdateColors();
            RenderOcean();
        }
        
        private void OnDestroy()
        {
            DisposeArrays();
        }
        
        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) showGUI = !showGUI;
            if (Input.GetKeyDown(KeyCode.Alpha2)) showGUI = !showGUI;
            if (Input.GetKeyDown(KeyCode.F5)) _ = UltraSave.Save();
            if (Input.GetKeyDown(KeyCode.F6)) _ = UltraSave.Load();
        }
        
        private void InitializeOcean()
        {
            DisposeArrays();
            
            int totalCubes = gridWidth * gridHeight;
            matrices = new NativeArray<Matrix4x4>(totalCubes, Allocator.Persistent);
            colors = new NativeArray<Vector4>(totalCubes, Allocator.Persistent);
            basePositions = new NativeArray<float3>(totalCubes, Allocator.Persistent);
            previousArraySize = totalCubes;
            
            var setupJob = new SetupOceanJob
            {
                gridWidth = gridWidth,
                gridHeight = gridHeight,
                spacing = spacing,
                colorVariations = colorVariations,
                colorPalette = new NativeArray<Vector4>(colorPalette.Length, Allocator.TempJob),
                basePositions = basePositions,
                colors = colors
            };
            
            for (int i = 0; i < colorPalette.Length; i++)
            {
                setupJob.colorPalette[i] = new Vector4(colorPalette[i].r, colorPalette[i].g, colorPalette[i].b, colorPalette[i].a);
            }
            
            jobHandle = setupJob.Schedule(totalCubes, 64);
            jobHandle.Complete();
            setupJob.colorPalette.Dispose();
            
            if (cubeMaterial != null)
            {
                cubeMaterial.SetColor(BaseColor, colorPalette[0]);
            }
        }
        
        private void UpdateOcean()
        {
            jobHandle.Complete();
            time += Time.deltaTime;
            
            var updateJob = new UpdateOceanJob
            {
                time = time,
                waveSpeed = waveSpeed,
                waveHeight = waveHeight,
                rotationSpeed = rotationSpeed,
                gridWidth = gridWidth,
                basePositions = basePositions,
                matrices = matrices
            };
            
            jobHandle = updateJob.Schedule(matrices.Length, 64);
        }
        
        private void UpdateColors()
        {
            if (cubeMaterial != null && colorPalette.Length > 0)
            {
                Color currentColor = Color.Lerp(colorPalette[0], colorPalette[colorVariations % colorPalette.Length], 
                    0.5f + 0.5f * Mathf.Sin(time * 0.5f));
                
                cubeMaterial.SetColor(BaseColor, currentColor);
                
                if (colors.IsCreated && colors.Length <= 4096 && colors.Length == previousArraySize)
                {
                    materialProperties.SetVectorArray(Colors, colors.ToArray());
                }
            }
        }
        
        private void RenderOcean()
        {
            jobHandle.Complete();
            
            if (cubeMaterial == null || !cubeMaterial.enableInstancing)
            {
                Debug.LogError("Material não configurado para GPU Instancing!");
                return;
            }
            
            int batchSize = 1023;
            for (int i = 0; i < matrices.Length; i += batchSize)
            {
                int currentBatchSize = Mathf.Min(batchSize, matrices.Length - i);
                
                Matrix4x4[] batch = new Matrix4x4[currentBatchSize];
                for (int j = 0; j < currentBatchSize; j++)
                {
                    batch[j] = matrices[i + j];
                }
                
                Graphics.DrawMeshInstanced(
                    cubeMesh, 
                    0, 
                    cubeMaterial, 
                    batch, 
                    currentBatchSize,
                    materialProperties
                );
            }
        }
        
        private void RefreshOcean()
        {
            SetupMaterialAndMesh();
            InitializeOcean();
        }
        
        private void DisposeArrays()
        {
            if (jobHandle.IsCompleted == false) jobHandle.Complete();
            
            if (matrices.IsCreated) matrices.Dispose();
            if (colors.IsCreated) colors.Dispose();
            if (basePositions.IsCreated) basePositions.Dispose();
        }
        
        private void ChangePalette(Color[] newPalette)
        {
            colorPalette = newPalette;
            InitializeOcean();
        }
        
        private void OnGUI()
        {
            if (!showGUI) return;
            
            Rect windowRect = new Rect(20, 20, 320, Screen.height - 100);
            GUI.Window(0, windowRect, DrawGUI, "Ocean");
        }
        
        private void DrawGUI(int windowID)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(Screen.height - 150));
            
            GUILayout.Label("GPU Ocean", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            GUILayout.Label("Configs da Grid:");
            
            GUILayout.Label($"Largura: {gridWidth} cubos");
            int newWidth = Mathf.RoundToInt(GUILayout.HorizontalSlider(gridWidth, 10, 500));
            
            GUILayout.Label($"Altura: {gridHeight} cubos");
            int newHeight = Mathf.RoundToInt(GUILayout.HorizontalSlider(gridHeight, 10, 500));
            
            GUILayout.Label($"Total de cubos renderizados: {gridWidth * gridHeight:N0}");
            
            if (newWidth != gridWidth || newHeight != gridHeight)
            {
                gridWidth = newWidth;
                gridHeight = newHeight;
                InitializeOcean();
            }
            
            GUILayout.Label($"Espaço entre cubos: {spacing:F1}");
            float newSpacing = GUILayout.HorizontalSlider(spacing, 0.5f, 3f);
            if (Mathf.Abs(newSpacing - spacing) > 0.01f)
            {
                spacing = newSpacing;
                InitializeOcean();
            }
            
            GUILayout.Space(10);
            GUILayout.Label("Animação das ondas:");
            
            GUILayout.Label($"Velocidade: {waveSpeed:F1}");
            waveSpeed = GUILayout.HorizontalSlider(waveSpeed, 0f, 10f);
            
            GUILayout.Label($"Altura: {waveHeight:F1}");
            waveHeight = GUILayout.HorizontalSlider(waveHeight, 0f, 5f);
            
            GUILayout.Label($"Velocidade: {rotationSpeed:F0} graus por segundo");
            rotationSpeed = GUILayout.HorizontalSlider(rotationSpeed, -180f, 180f);
            
            GUILayout.Space(10);
            GUILayout.Label("Configs Visuais:");
            
            GUILayout.Label($"Variações da cor ativa: {colorVariations}");
            int newColorVar = Mathf.RoundToInt(GUILayout.HorizontalSlider(colorVariations, 1, colorPalette.Length));
            if (newColorVar != colorVariations)
            {
                colorVariations = newColorVar;
                if (cubeMaterial != null)
                {
                    cubeMaterial.SetColor(BaseColor, colorPalette[0]);
                }
            }
            
            GUILayout.Space(15);
            GUILayout.Label("Informaçoes de performance:");
            GUILayout.Label($"FPS: {1f / Time.deltaTime:F0} FPS");
            GUILayout.Label($"Cubos renderizados: {gridWidth * gridHeight:N0}");
            GUILayout.Label($"GPU Instancing: {(cubeMaterial?.enableInstancing == true ? "Sim" : "Nao")}");
          
            GUILayout.Space(15);
            GUILayout.Label("Controles:");
            
            if (GUILayout.Button("Salvar(F5)", GUILayout.Height(35)))
                _ = UltraSave.Save();
            
            if (GUILayout.Button("Carregar(F6)", GUILayout.Height(35)))
                _ = UltraSave.Load();
            
            if (GUILayout.Button("Reiniciar", GUILayout.Height(35)))
                InitializeOcean();
            
            GUILayout.Space(10);
            GUILayout.Label("Paletas de cor:");
            
            if (GUILayout.Button("Azul", GUILayout.Height(30)))
            {
                ChangePalette(new[] {
                    Color.cyan, Color.blue, new Color(0, 0.5f, 1f),
                    new Color(0.2f, 0.8f, 1f), new Color(0.5f, 0.9f, 1f)
                });
            }
            
            if (GUILayout.Button("Vermelho", GUILayout.Height(30)))
            {
                ChangePalette(new[] {
                    Color.red, Color.yellow, new Color(1f, 0.5f, 0f),
                    new Color(1f, 0.2f, 0f), new Color(0.8f, 0f, 0f)
                });
            }
            
            if (GUILayout.Button("Verde", GUILayout.Height(30)))
            {
                ChangePalette(new[] {
                    Color.green, new Color(0.5f, 1f, 0.5f), new Color(0, 0.8f, 0),
                    new Color(0.2f, 0.6f, 0.2f), new Color(0, 1f, 0)
                });
            }
            
            if (GUILayout.Button("Roxo", GUILayout.Height(30)))
            {
                ChangePalette(new [] {
                    Color.magenta, new Color(1f, 0.5f, 0.8f), new Color(0.8f, 0.2f, 0.6f),
                    new Color(1f, 0.7f, 0.9f), new Color(0.9f, 0.4f, 0.7f)
                });
            }
            
            if (GUILayout.Button("Colorido", GUILayout.Height(30)))
            {
                ChangePalette(new[] {
                    Color.red, Color.yellow, Color.green, Color.cyan, Color.blue, Color.magenta
                });
            }
            
            GUILayout.Space(15);
            GUILayout.Label("Dicas de uso:");
            GUILayout.Label("Pressione tecla 1 ou 2 para mostrar/esconder a gui");
            GUILayout.Label("Use F5 para salvar e F6 para carregar suas configurações");
            GUILayout.Label("Deixe o numero de cubos baixo para melhor performance");
            
            GUILayout.EndScrollView();
        }
        
        private struct SetupOceanJob : IJobParallelFor
        {
            public int gridWidth, gridHeight, colorVariations;
            public float spacing;
            
            [ReadOnly] public NativeArray<Vector4> colorPalette;
            
            public NativeArray<float3> basePositions;
            public NativeArray<Vector4> colors;
            
            public void Execute(int index)
            {
                int x = index % gridWidth;
                int z = index / gridWidth;
                
                float3 position = new float3(
                    (x - gridWidth * 0.5f) * spacing,
                    0,
                    (z - gridHeight * 0.5f) * spacing
                );
                
                basePositions[index] = position;
                
                int colorIndex = (x + z) % colorVariations;
                colors[index] = colorPalette[colorIndex % colorPalette.Length];
            }
        }
        
        private struct UpdateOceanJob : IJobParallelFor
        {
            public float time, waveSpeed, waveHeight, rotationSpeed;
            public int gridWidth;
            
            [ReadOnly] public NativeArray<float3> basePositions;
            public NativeArray<Matrix4x4> matrices;
            
            public void Execute(int index)
            {
                float3 basePos = basePositions[index];
                
                int x = index % gridWidth;
                int z = index / gridWidth;
                
                float wave1 = math.sin(time * waveSpeed + basePos.x * 0.5f) * waveHeight;
                float wave2 = math.cos(time * waveSpeed * 0.7f + basePos.z * 0.3f) * waveHeight * 0.5f;
                float wave3 = math.sin(time * waveSpeed * 1.3f + (x + z) * 0.2f) * waveHeight * 0.3f;
                
                float3 finalPos = basePos + new float3(0, wave1 + wave2 + wave3, 0);
                
                quaternion rotation = quaternion.RotateY(time * rotationSpeed * math.PI / 180f);
                
                float scaleVariation = 0.8f + 0.4f * math.sin(time * 2f + index * 0.1f);
                float3 scale = new float3(scaleVariation);
                
                matrices[index] = Matrix4x4.TRS(finalPos, rotation, scale);
            }
        }
    }
}