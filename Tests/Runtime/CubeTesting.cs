using System;
using System.Collections;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UltraSaveSystem
{
    [Saveable("rotating_cube", true)]
    public class CubeTesting : MonoBehaviour, ICustomSaveable
    {
        [Header("Configurações de Movimento")] [SaveField]
        public float rotationSpeed = 50f;

        [Header("Configurações Visuais")] [SaveField]
        public float scale = 1f;

        [SaveField] public int currentColorIndex;

        [Header("Referências")] 
        public Color[] colorPalette = { Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta };

        private GameObject cubeInstance;
        private Renderer cubeRenderer;
        private bool guiMinimized;
        private Material instanceMaterial;

        private void Start()
        {
            CreateCube();
            SetupInitialState();
            
            UltraSaveSystem.OnSaveCompleted += () => Debug.Log("✅ Dados salvos com sucesso!");
            UltraSaveSystem.OnLoadCompleted += () =>
            {
                Debug.Log("📂 Dados carregados com sucesso!");
                RefreshVisuals();
            };
        }

        private void Update()
        {
            transform.Rotate(new Vector3(1,0,1), rotationSpeed * Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.F5)) _ = UltraSaveSystem.Save();
            if (Input.GetKeyDown(KeyCode.F6)) _ = UltraSaveSystem.Load();
            if (Input.GetKeyDown(KeyCode.F7)) guiMinimized = !guiMinimized;
        }

        private void OnGUI()
        {
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;

            var guiWidth = guiMinimized ? 200f : 300f;
            var guiHeight = guiMinimized ? 150f : 400f;
            var guiX = 20f;
            var guiY = 20f;

            GUILayout.BeginArea(new Rect(guiX, guiY, guiWidth, guiHeight));
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                margin = new RectOffset(2, 2, 2, 2)
            };
            GUILayout.BeginHorizontal();
            GUILayout.Label("🎮 CUBO TESTE", headerStyle);
            if (GUILayout.Button(guiMinimized ? "▼" : "▲", GUILayout.Width(25))) guiMinimized = !guiMinimized;
            GUILayout.EndHorizontal();

            if (!guiMinimized)
            {
                GUILayout.Space(5);
                GUILayout.Label("🔄 MOVIMENTO", headerStyle);
                GUILayout.Label($"Rotação: {rotationSpeed:F0}");
                rotationSpeed = GUILayout.HorizontalSlider(rotationSpeed, 0f, 200f);
                GUILayout.Label($"Escala: {scale:F1}");
                var newScale = GUILayout.HorizontalSlider(scale, 0.1f, 5f);
                if (Mathf.Abs(newScale - scale) > 0.01f)
                {
                    scale = newScale;
                    ApplyScale();
                }
                GUILayout.Space(8);
                GUILayout.Label("🎨 COR", headerStyle);
                var colorRect = GUILayoutUtility.GetRect(guiWidth - 20, 20);
                EditorGUI.DrawRect(colorRect, GetCurrentColor());
                GUILayout.Label($"Cor: {currentColorIndex + 1}/{colorPalette.Length} ({GetColorName()})");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("◄", buttonStyle, GUILayout.Width(30))) PreviousColor();
                if (GUILayout.Button("🎲", buttonStyle, GUILayout.Width(30))) RandomizeColor();
                if (GUILayout.Button("►", buttonStyle, GUILayout.Width(30))) NextColor();
                GUILayout.EndHorizontal();
                GUILayout.Space(8);
                GUILayout.Label("💾 SAVE SYSTEM", headerStyle);
                var currentEncryption = UltraSaveSystem.IsEncryptionEnabled();
                var newEncryption = GUILayout.Toggle(currentEncryption, "🔐 Criptografia");
                if (newEncryption != currentEncryption) UltraSaveSystem.EnableEncryption(newEncryption);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("💾 Salvar", buttonStyle)) _ = UltraSaveSystem.Save();
                if (GUILayout.Button("📂 Carregar", buttonStyle)) _ = UltraSaveSystem.Load();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("ℹ️ Debug", buttonStyle)) Debug.Log(UltraSaveSystem.GetDebugInfo());
                if (GUILayout.Button("🔄 Reset", buttonStyle)) ResetToDefaults();
                GUILayout.EndHorizontal();
                GUILayout.Space(5);
                GUILayout.Label("⌨️ F5=Save | F6=Load | F7=GUI", new GUIStyle(GUI.skin.label) { fontSize = 20 });
            }
            GUILayout.EndArea();
        }

        public void OnBeforeSave()
        {
            Debug.Log($"📤 Preparando save - Cor: {GetColorName()}, Escala: {scale:F1}");
        }

        public void OnAfterLoad()
        {
            Debug.Log($"📥 Load completo - Cor: {GetColorName()}, Escala: {scale:F1}");
            RefreshVisuals();
        }

        public byte[] SerializeCustomData()
        {
            var customData = $"LastPlayed:{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            return Encoding.UTF8.GetBytes(customData);
        }

        public void DeserializeCustomData(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                var customData = Encoding.UTF8.GetString(data);
                Debug.Log($"📦 Dados customizados: {customData}");
            }
        }

        private void CreateCube()
        {
            cubeInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeInstance.transform.SetParent(transform, false);
            cubeRenderer = cubeInstance.GetComponent<Renderer>();
            instanceMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            cubeRenderer.material = instanceMaterial;
        }

        private void SetupInitialState()
        {
            ApplyScale();
            ApplyColor();
        }

        private void ApplyScale()
        {
            scale = Mathf.Clamp(scale, 0.1f, 5f);
            transform.localScale = Vector3.one * scale;
        }

        private void ApplyColor()
        {
            if (colorPalette.Length > 0 && instanceMaterial != null)
            {
                currentColorIndex = Mathf.Clamp(currentColorIndex, 0, colorPalette.Length - 1);
                instanceMaterial.color = colorPalette[currentColorIndex];
            }
        }

        private void NextColor()
        {
            currentColorIndex = (currentColorIndex + 1) % colorPalette.Length;
            ApplyColor();
        }

        private void PreviousColor()
        {
            currentColorIndex = (currentColorIndex - 1 + colorPalette.Length) % colorPalette.Length;
            ApplyColor();
        }

        private void RandomizeColor()
        {
            if (colorPalette.Length > 0)
            {
                var newIndex = Random.Range(0, colorPalette.Length);
                if (newIndex == currentColorIndex && colorPalette.Length > 1)
                    newIndex = (newIndex + 1) % colorPalette.Length;
                currentColorIndex = newIndex;
                ApplyColor();
            }
        }

        private void ResetToDefaults()
        {
            rotationSpeed = 50f;
            scale = 1f;
            currentColorIndex = 0;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            ApplyScale();
            ApplyColor();
        }

        private Color GetCurrentColor()
        {
            if (colorPalette.Length > 0 && currentColorIndex >= 0 && currentColorIndex < colorPalette.Length)
                return colorPalette[currentColorIndex];
            return Color.white;
        }

        private string GetColorName()
        {
            var color = GetCurrentColor();

            if (color == Color.red) return "Vermelho";
            if (color == Color.green) return "Verde";
            if (color == Color.blue) return "Azul";
            if (color == Color.yellow) return "Amarelo";
            if (color == Color.cyan) return "Ciano";
            if (color == Color.magenta) return "Magenta";
            if (color == Color.white) return "Branco";
            if (color == Color.black) return "Preto";

            return $"RGB({color.r:F1},{color.g:F1},{color.b:F1})";
        }
      
        private void RefreshVisuals()
        {
            StartCoroutine(RefreshVisualsCoroutine());
        }

        private IEnumerator RefreshVisualsCoroutine()
        {
            yield return null;
            ApplyScale();
            ApplyColor();
        }
    }
}

public static class EditorGUI
{
    public static void DrawRect(Rect rect, Color color)
    {
        var originalColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = originalColor;
    }
}