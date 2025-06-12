using UnityEngine;

namespace UltraSaveSystem.Tests.Runtime
{
    [Saveable("rotating_cube", true)]
    public class SimpleCubeExample : MonoBehaviour
    {
        [SaveField] public float rotationSpeed = 50f;
        [SaveField] public float scale = 1f;
        [SaveField] public int colorIndex;
        
        public Color[] colors =
        { 
            Color.white, 
            Color.black, 
            Color.red,
            Color.green,
            Color.blue,
            Color.yellow,
            Color.cyan, 
            Color.magenta
        };
        
        private Renderer cubeRenderer;
        private bool isInitialized;
        
        private void Start()
        {
            SetupCube();
            this.AutoRegisterSaveableFields();
            isInitialized = true;
        }
        
        private void Update()
        {
            if (!isInitialized) return;
            
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            
            if (Input.GetKeyDown(KeyCode.F5))
                _ = UltraSave.Save();
            
            if (Input.GetKeyDown(KeyCode.F6))
                _ = UltraSave.Load();
        }
        
        private void SetupCube()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(transform);
            cube.transform.localPosition = Vector3.zero;
            
            cubeRenderer = cube.GetComponent<Renderer>();
            cubeRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            
            ApplyVisualSettings();
        }
        
        private void ApplyVisualSettings()
        {
            if (cubeRenderer != null)
            {
                transform.localScale = Vector3.one * scale;
                cubeRenderer.material.color = colors[Mathf.Clamp(colorIndex, 0, colors.Length - 1)];
            }
        }
        
        // Método alternativo que também será detectado
        private void OnAfterLoad()
        {
            ApplyVisualSettings();
        }
        
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 250, 250));
            
            GUILayout.Label("Ultra Save System");
            GUILayout.Space(10);
            
            GUILayout.Label($"Rotation: {rotationSpeed:F0}");
            rotationSpeed = GUILayout.HorizontalSlider(rotationSpeed, 0f, 200f);
            
            GUILayout.Label($"Scale: {scale:F1}");
            var newScale = GUILayout.HorizontalSlider(scale, 0.1f, 3f);
            if (Mathf.Abs(newScale - scale) > 0.01f)
            {
                scale = newScale;
                ApplyVisualSettings();
            }
            
            GUILayout.Label($"Color: {colorIndex + 1}/{colors.Length}");
            var newColorIndex = Mathf.RoundToInt(GUILayout.HorizontalSlider(colorIndex, 0, colors.Length - 1));
            if (newColorIndex != colorIndex)
            {
                colorIndex = newColorIndex;
                ApplyVisualSettings();
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Save (F5)"))
                _ = UltraSave.Save();
            
            if (GUILayout.Button("Load (F6)"))
                _ = UltraSave.Load();
            
            GUILayout.EndArea();
        }
    }
}