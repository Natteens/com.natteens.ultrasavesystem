using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UltraSaveSystem.Editor
{
    [CustomEditor(typeof(UltraSaveConfig))]
    public class UltraSaveConfigEditor : UnityEditor.Editor
    {
        private Vector2 scrollPosition;
        private bool showTrackedObjects = true;
        private bool showSystemInfo = true;
        private bool showSlotManager = true;
        private bool showActions = true;
        
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle buttonStyle;
        private GUIStyle toggleStyle;
        
        private void InitializeStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black }
                };
                
                boxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 10, 10),
                    margin = new RectOffset(0, 0, 5, 5)
                };
                
                buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fixedHeight = 25,
                    margin = new RectOffset(2, 2, 2, 2)
                };
                
                toggleStyle = new GUIStyle(EditorStyles.toggle)
                {
                    fontSize = 12
                };
            }
        }
        
        public override void OnInspectorGUI()
        {
            InitializeStyles();
            
            var config = (UltraSaveConfig)target;
            
            EditorGUILayout.LabelField("Ultra Save System Configuration", headerStyle);
            EditorGUILayout.Space(10);
            
            DrawSystemSettings(config);
            EditorGUILayout.Space(5);
            
            DrawAutoSaveSettings(config);
            EditorGUILayout.Space(5);
            
            DrawSlotSettings(config);
            EditorGUILayout.Space(5);
            
            DrawPlayerSettings(config);
            EditorGUILayout.Space(5);
            
            DrawPerformanceSettings(config);
            EditorGUILayout.Space(5);
            
            DrawDebugSettings(config);
            EditorGUILayout.Space(10);
            
            DrawSystemInfo(config);
            EditorGUILayout.Space(5);
            
            DrawSlotManager(config);
            EditorGUILayout.Space(5);
            
            DrawTrackedObjects(config);
            EditorGUILayout.Space(5);
            
            DrawActions(config);
            
            if (GUI.changed)
            {
                EditorUtility.SetDirty(config);
            }
        }
        
        private void DrawSystemSettings(UltraSaveConfig config)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Sistema", EditorStyles.boldLabel);
            
            config.enableSystem = EditorGUILayout.ToggleLeft("Ativar Sistema", config.enableSystem, toggleStyle);
            config.enableEncryption = EditorGUILayout.ToggleLeft("Ativar Criptografia", config.enableEncryption, toggleStyle);
            config.enableVerboseLogging = EditorGUILayout.ToggleLeft("Logs Verbosos", config.enableVerboseLogging, toggleStyle);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawAutoSaveSettings(UltraSaveConfig config)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Autosave", EditorStyles.boldLabel);
            
            config.enableAutoSave = EditorGUILayout.ToggleLeft("Ativar Autosave", config.enableAutoSave, toggleStyle);
            
            if (config.enableAutoSave)
            {
                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Intervalo:", GUILayout.Width(60));
                
                var newMinutes = EditorGUILayout.IntSlider(config.AutoSaveMinutes, 1, 60);
                newMinutes = Mathf.RoundToInt(newMinutes / 5f) * 5;
                if (newMinutes < 1) newMinutes = 1;
                
                config.AutoSaveMinutes = newMinutes;
                
                EditorGUILayout.LabelField($"{config.AutoSaveMinutes}min", GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.HelpBox($"Autosave a cada {config.AutoSaveMinutes} minuto(s) ({config.autoSaveInterval}s)", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSlotSettings(UltraSaveConfig config)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Slots de Save", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Máximo de Slots:", GUILayout.Width(120));
            config.maxSaveSlots = EditorGUILayout.IntSlider(config.maxSaveSlots, 1, 20);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Slot Atual:", GUILayout.Width(120));
            config.currentSlot = EditorGUILayout.IntSlider(config.currentSlot, 0, config.maxSaveSlots - 1);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawPlayerSettings(UltraSaveConfig config)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Jogador", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Nome:", GUILayout.Width(50));
            config.playerName = EditorGUILayout.TextField(config.playerName);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawPerformanceSettings(UltraSaveConfig config)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
            
            config.useJobSystem = EditorGUILayout.ToggleLeft("Usar Job System", config.useJobSystem, toggleStyle);
            
            if (config.useJobSystem)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Max Jobs/Frame:", GUILayout.Width(120));
                config.maxJobsPerFrame = EditorGUILayout.IntSlider(config.maxJobsPerFrame, 1, 16);
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawDebugSettings(UltraSaveConfig config)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            
            config.showDebugInfo = EditorGUILayout.ToggleLeft("Mostrar Info Debug", config.showDebugInfo, toggleStyle);
            config.logSaveOperations = EditorGUILayout.ToggleLeft("Log Operações Save", config.logSaveOperations, toggleStyle);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSystemInfo(UltraSaveConfig config)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            showSystemInfo = EditorGUILayout.Foldout(showSystemInfo, "Informações do Sistema", true);
            if (showSystemInfo)
            {
                EditorGUI.indentLevel++;
                
                var isInitialized = Application.isPlaying && UltraSaveManager.IsInitialized;
                var statusColor = isInitialized ? Color.green : Color.red;
                var statusText = isInitialized ? "Inicializado" : "Não Inicializado";
                
                var oldColor = GUI.color;
                GUI.color = statusColor;
                EditorGUILayout.LabelField($"Status: {statusText}");
                GUI.color = oldColor;
                
                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField($"Objetos Rastreados: {UltraSaveManager.TrackedObjectCount}");
                    EditorGUILayout.LabelField($"Diretório de Save: {UltraSaveManager.SaveDirectory}");
                }
                else
                {
                    EditorGUILayout.LabelField("Execute o jogo para ver informações em tempo real");
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSlotManager(UltraSaveConfig config)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            showSlotManager = EditorGUILayout.Foldout(showSlotManager, "Gerenciador de Slots", true);
            if (showSlotManager)
            {
                EditorGUILayout.HelpBox("Clique em um slot para ver informações ou deletar", MessageType.Info);
                
                var cols = Mathf.Min(5, config.maxSaveSlots);
                var rows = Mathf.CeilToInt((float)config.maxSaveSlots / cols);
                
                for (int row = 0; row < rows; row++)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    for (int col = 0; col < cols; col++)
                    {
                        var slotIndex = row * cols + col;
                        if (slotIndex >= config.maxSaveSlots) break;
                        
                        var hasSlot = Application.isPlaying && UltraSaveManager.HasSave(slotIndex);
                        var isCurrentSlot = slotIndex == config.currentSlot;
                        
                        var buttonText = $"Slot {slotIndex + 1}";
                        if (hasSlot) buttonText += " ✓";
                        if (isCurrentSlot) buttonText = $"[{buttonText}]";
                        
                        var oldColor = GUI.backgroundColor;
                        if (isCurrentSlot) GUI.backgroundColor = Color.cyan;
                        else if (hasSlot) GUI.backgroundColor = Color.green;
                        
                        if (GUILayout.Button(buttonText, buttonStyle))
                        {
                            HandleSlotClick(slotIndex, hasSlot, config);
                        }
                        
                        GUI.backgroundColor = oldColor;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void HandleSlotClick(int slot, bool hasSlot, UltraSaveConfig config)
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Info", "Execute o jogo para gerenciar slots", "OK");
                return;
            }
            
            if (hasSlot)
            {
                var info = UltraSaveManager.GetSaveSlotInfo(slot);
                var message = $"Slot: {slot + 1}\n" +
                             $"Tamanho: {info.sizeBytes} bytes\n" +
                             $"Modificado: {info.lastModified:dd/MM/yyyy HH:mm:ss}";
                
                var choice = EditorUtility.DisplayDialogComplex("Informações do Slot", message, "Usar Este Slot", "Deletar", "Cancelar");
                
                switch (choice)
                {
                    case 0: // Usar Este Slot
                        config.currentSlot = slot;
                        break;
                    case 1: // Deletar
                        if (EditorUtility.DisplayDialog("Confirmar", $"Deletar o slot {slot + 1}?", "Sim", "Não"))
                        {
                            UltraSaveManager.DeleteSave(slot);
                        }
                        break;
                }
            }
            else
            {
                config.currentSlot = slot;
                EditorUtility.DisplayDialog("Slot Selecionado", $"Slot {slot + 1} selecionado como slot atual", "OK");
            }
        }
        
        private void DrawTrackedObjects(UltraSaveConfig config)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            showTrackedObjects = EditorGUILayout.Foldout(showTrackedObjects, $"Objetos Rastreados ({config.TrackedObjects.Count})", true);
            if (showTrackedObjects && config.TrackedObjects.Count > 0)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
                
                foreach (var obj in config.TrackedObjects)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    EditorGUILayout.LabelField($"Chave: {obj.saveKey}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Tipo: {obj.typeName}");
                    EditorGUILayout.LabelField($"Cena: {obj.sceneName}");
                    
                    if (obj.hasTransform)
                        EditorGUILayout.LabelField($"Posição: {obj.position}");
                    
                    EditorGUILayout.LabelField($"Campos: {obj.fieldCount}");
                    EditorGUILayout.LabelField($"Tamanho: {obj.dataSize} bytes");
                    EditorGUILayout.LabelField($"Último Save: {obj.lastSaved:dd/MM/yyyy HH:mm:ss}");
                    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
                
                EditorGUILayout.EndScrollView();
            }
            else if (config.TrackedObjects.Count == 0)
            {
                EditorGUILayout.HelpBox("Nenhum objeto rastreado. Execute o jogo e faça um save para ver objetos aqui.", MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawActions(UltraSaveConfig config)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            
            showActions = EditorGUILayout.Foldout(showActions, "Ações", true);
            if (showActions)
            {
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Abrir Pasta de Saves", buttonStyle))
                {
                    var path = Application.isPlaying ? UltraSaveManager.SaveDirectory : 
                              Path.Combine(Application.persistentDataPath, "UltraSaves");
                    
                    if (Directory.Exists(path))
                    {
                        Process.Start(path);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Pasta não encontrada", "A pasta de saves ainda não foi criada", "OK");
                    }
                }
                
                if (GUILayout.Button("Limpar Objetos", buttonStyle))
                {
                    config.ClearTrackedObjects();
                    EditorUtility.SetDirty(config);
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Forçar Refresh", buttonStyle))
                {
                    if (Application.isPlaying)
                    {
                        UltraSaveManager.ForceRefreshObjects();
                        EditorUtility.DisplayDialog("Refresh", "Objetos atualizados com sucesso!", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Info", "Execute o jogo para fazer refresh", "OK");
                    }
                }
                
                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = Color.red;
                
                if (GUILayout.Button("Limpar Todos os Saves", buttonStyle))
                {
                    if (EditorUtility.DisplayDialog("Atenção", "Isso irá deletar TODOS os arquivos de save. Tem certeza?", "Sim, Deletar Tudo", "Cancelar"))
                    {
                        var path = Application.isPlaying ? UltraSaveManager.SaveDirectory : 
                                  Path.Combine(Application.persistentDataPath, "UltraSaves");
                        
                        if (Directory.Exists(path))
                        {
                            Directory.Delete(path, true);
                            Directory.CreateDirectory(path);
                            config.ClearTrackedObjects();
                            EditorUtility.SetDirty(config);
                            EditorUtility.DisplayDialog("Concluído", "Todos os saves foram removidos", "OK");
                        }
                    }
                }
                
                GUI.backgroundColor = oldColor;
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }
    }
}