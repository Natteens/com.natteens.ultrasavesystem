using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UltraSaveSystem.Editor
{
    [CustomEditor(typeof(UltraSaveConfig))]
    public class UltraSaveConfigEditor : UnityEditor.Editor
    {
        private enum EditorTab
        {
            Settings = 0,
            SlotManager = 1,
            Information = 2,
            Actions = 3
        }

        private EditorTab currentTab = EditorTab.Settings;
        private Vector2 scrollPosition;
        
        private GUIStyle headerStyle;
        private GUIStyle tabButtonStyle;
        private GUIStyle activeTabStyle;
        private GUIStyle boxStyle;
        private GUIStyle slotButtonStyle;
        private GUIStyle currentSlotStyle;
        private GUIStyle occupiedSlotStyle;
        private GUIStyle deleteButtonStyle;
        
        private readonly string[] tabNames = { "Configurações", "Slots", "Informações", "Ações" };
        private readonly Color primaryColor = new Color(0.2f, 0.6f, 1f);
        private readonly Color successColor = new Color(0.3f, 0.8f, 0.3f);
        private readonly Color dangerColor = new Color(1f, 0.4f, 0.4f);

        private void InitializeStyles()
        {
            if (headerStyle != null) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black }
            };

            tabButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fixedHeight = 28,
                fontSize = 11,
                fontStyle = FontStyle.Normal
            };

            activeTabStyle = new GUIStyle(tabButtonStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { background = MakeTex(2, 2, primaryColor * 0.7f) }
            };

            boxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 8, 8),
                margin = new RectOffset(0, 0, 4, 4)
            };

            slotButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 40,
                margin = new RectOffset(2, 2, 2, 2),
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter
            };

            currentSlotStyle = new GUIStyle(slotButtonStyle)
            {
                normal = { background = MakeTex(2, 2, primaryColor * 0.8f) },
                fontStyle = FontStyle.Bold
            };

            occupiedSlotStyle = new GUIStyle(slotButtonStyle)
            {
                normal = { background = MakeTex(2, 2, successColor * 0.6f) }
            };

            deleteButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedWidth = 18,
                fixedHeight = 18,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white, background = MakeTex(2, 2, dangerColor) }
            };
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();
            var config = (UltraSaveConfig)target;

            DrawCustomHeader();
            DrawTabs();
            EditorGUILayout.Space(8);
            
            switch (currentTab)
            {
                case EditorTab.Settings:
                    DrawSettingsTab(config);
                    break;
                case EditorTab.SlotManager:
                    DrawSlotManagerTab(config);
                    break;
                case EditorTab.Information:
                    DrawInformationTab(config);
                    break;
                case EditorTab.Actions:
                    DrawActionsTab(config);
                    break;
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(config);
            }
        }

        private void DrawCustomHeader()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Ultra Save System", headerStyle);
            EditorGUILayout.Space(8);
        }

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();
            
            for (int i = 0; i < tabNames.Length; i++)
            {
                var style = (EditorTab)i == currentTab ? activeTabStyle : tabButtonStyle;
                
                if (GUILayout.Button(tabNames[i], style))
                {
                    currentTab = (EditorTab)i;
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSettingsTab(UltraSaveConfig config)
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawSection("Sistema", () =>
            {
                config.enableSystem = DrawToggle("Ativar Sistema", config.enableSystem, 
                    "Ativa/desativa completamente o sistema de save");
                
                if (config.enableSystem)
                {
                    config.enableEncryption = DrawToggle("Criptografia", config.enableEncryption, 
                        "Criptografa os arquivos de save para maior segurança");
                    config.enableVerboseLogging = DrawToggle("Logs Detalhados", config.enableVerboseLogging, 
                        "Exibe logs detalhados das operações");
                }
                else
                {
                    EditorGUILayout.HelpBox("Sistema desativado. Nenhuma operação de save será executada.", MessageType.Warning);
                }
            });

            DrawSection("Autosave", () =>
            {
                config.enableAutoSave = DrawToggle("Ativar Autosave", config.enableAutoSave, 
                    "Salva automaticamente em intervalos regulares");

                if (config.enableAutoSave)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Intervalo:", GUILayout.Width(70));
                    
                    var newMinutes = EditorGUILayout.IntSlider(config.AutoSaveMinutes, 1, 60);
                    newMinutes = Mathf.RoundToInt(newMinutes / 5f) * 5;
                    if (newMinutes < 1) newMinutes = 1;
                    config.AutoSaveMinutes = newMinutes;
                    
                    EditorGUILayout.LabelField($"{config.AutoSaveMinutes}min", GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();
                }
            });

            DrawSection("Configuração de Slots", () =>
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Máximo de Slots:", GUILayout.Width(120));
                var newMaxSlots = EditorGUILayout.IntSlider(config.maxSaveSlots, 1, 20);
                if (newMaxSlots != config.maxSaveSlots)
                {
                    config.maxSaveSlots = newMaxSlots;
                    if (config.currentSlot >= config.maxSaveSlots)
                        config.currentSlot = config.maxSaveSlots - 1;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Slot Ativo:", GUILayout.Width(120));
                var displaySlot = EditorGUILayout.IntSlider(config.currentSlot + 1, 1, config.maxSaveSlots);
                config.currentSlot = displaySlot - 1;
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.HelpBox($"Slot ativo: Slot {config.currentSlot + 1}", MessageType.Info);
            });

            DrawSection("Performance e Armazenamento", () =>
            {
                config.useJobSystem = DrawToggle("Job System", config.useJobSystem, 
                    "Usa o Job System para operações assíncronas");

                if (config.useJobSystem)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Max Jobs/Frame:", GUILayout.Width(120));
                    config.maxJobsPerFrame = EditorGUILayout.IntSlider(config.maxJobsPerFrame, 1, 16);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(5);
                
                config.enableCompression = DrawToggle("Compressão de Arquivos", config.enableCompression, 
                    "Comprime os arquivos de save para reduzir o tamanho (recomendado)");
                
                if (config.enableCompression)
                {
                    EditorGUILayout.HelpBox("Compressão ativa: reduz o tamanho dos arquivos em até 70%", MessageType.Info);
                }
            });

            DrawSection("Interface do Editor", () =>
            {
                config.showEditorDialogs = DrawToggle("Mostrar Diálogos", config.showEditorDialogs, 
                    "Exibe diálogos informativos no editor");
                config.showDebugInfo = DrawToggle("Info de Debug", config.showDebugInfo, 
                    "Mostra informações de debug no inspector");
                config.logSaveOperations = DrawToggle("Log de Operações", config.logSaveOperations, 
                    "Registra operações de save/load no console");
            });

            DrawSection("Jogador", () =>
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Nome:", GUILayout.Width(50));
                config.playerName = EditorGUILayout.TextField(config.playerName);
                EditorGUILayout.EndHorizontal();
            });

            EditorGUILayout.EndScrollView();
        }

        private void DrawSlotManagerTab(UltraSaveConfig config)
        {
            DrawSection("Gerenciador de Slots", () =>
            {
                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Execute o jogo para ver o status dos slots e usar as funcionalidades.", MessageType.Info);
                }

                DrawSlotGrid(config);
                EditorGUILayout.Space(10);
                DrawQuickActions(config);
            });
        }

        private void DrawSlotGrid(UltraSaveConfig config)
        {
            GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true));
            var availableWidth = EditorGUIUtility.currentViewWidth - 40;
            
            var slotWidth = 85f;
            var slotsPerRow = Mathf.Clamp(Mathf.FloorToInt(availableWidth / slotWidth), 1, 4);
            var rows = Mathf.CeilToInt((float)config.maxSaveSlots / slotsPerRow);
            
            for (int row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();
                
                var slotsInThisRow = Mathf.Min(slotsPerRow, config.maxSaveSlots - (row * slotsPerRow));
                
                if (slotsInThisRow < slotsPerRow)
                {
                    var spaceBefore = (slotsPerRow - slotsInThisRow) * slotWidth * 0.5f;
                    GUILayout.Space(spaceBefore);
                }
                
                for (int col = 0; col < slotsPerRow; col++)
                {
                    var slotIndex = row * slotsPerRow + col;
                    if (slotIndex >= config.maxSaveSlots) break;
                    
                    DrawSlotButton(slotIndex, config);
                }
                
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }
        }

        private void DrawSlotButton(int slotIndex, UltraSaveConfig config)
        {
            var hasSlot = Application.isPlaying && UltraSaveManager.HasSave(slotIndex);
            var isCurrentSlot = slotIndex == config.currentSlot;

            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            
            var style = isCurrentSlot ? currentSlotStyle : (hasSlot ? occupiedSlotStyle : slotButtonStyle);
            var slotNumber = slotIndex + 1;
            var content = $"Slot {slotNumber}\n{(isCurrentSlot ? "ATIVO" : (hasSlot ? "SALVO" : "VAZIO"))}";

            if (GUILayout.Button(content, style, GUILayout.Width(76), GUILayout.Height(40)))
            {
                config.currentSlot = slotIndex;
                if (config.showEditorDialogs && !Application.isPlaying)
                {
                    EditorUtility.DisplayDialog("Slot Selecionado", 
                        $"Slot {slotNumber} definido como ativo.", "OK");
                }
            }

            if (hasSlot)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("✕", deleteButtonStyle))
                {
                    if (!config.showEditorDialogs || 
                        EditorUtility.DisplayDialog("Deletar Save", 
                            $"Deletar o save do Slot {slotNumber}?", "Sim", "Não"))
                    {
                        UltraSaveManager.DeleteSave(slotIndex);
                    }
                }
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Space(22);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQuickActions(UltraSaveConfig config)
        {
            EditorGUILayout.LabelField("Ações Rápidas", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            var currentSlotText = $"Slot {config.currentSlot + 1}";
            
            if (GUILayout.Button($"Salvar ({currentSlotText})", GUILayout.Height(30)))
            {
                if (Application.isPlaying)
                {
                    _ = UltraSaveManager.SaveAsync(config.currentSlot);
                    if (config.showEditorDialogs)
                        EditorUtility.DisplayDialog("Save", $"Salvando no {currentSlotText}...", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Info", "Execute o jogo para salvar", "OK");
                }
            }

            if (GUILayout.Button($"Carregar ({currentSlotText})", GUILayout.Height(30)))
            {
                if (Application.isPlaying)
                {
                    if (UltraSaveManager.HasSave(config.currentSlot))
                    {
                        _ = UltraSaveManager.LoadAsync(config.currentSlot);
                        if (config.showEditorDialogs)
                            EditorUtility.DisplayDialog("Load", $"Carregando do {currentSlotText}...", "OK");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Erro", $"{currentSlotText} está vazio!", "OK");
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Info", "Execute o jogo para carregar", "OK");
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawInformationTab(UltraSaveConfig config)
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawSection("Status do Sistema", () =>
            {
                var isInitialized = Application.isPlaying && UltraSaveManager.IsInitialized;
                var statusColor = isInitialized ? successColor : dangerColor;
                var statusText = isInitialized ? "✓ Inicializado" : "✗ Não Inicializado";

                var oldColor = GUI.color;
                GUI.color = statusColor;
                EditorGUILayout.LabelField($"Status: {statusText}", EditorStyles.boldLabel);
                GUI.color = oldColor;

                if (Application.isPlaying && isInitialized)
                {
                    EditorGUILayout.LabelField($"Objetos Rastreados: {UltraSaveManager.TrackedObjectCount}");
                    EditorGUILayout.LabelField($"Slot Ativo: Slot {config.currentSlot + 1}");
                    EditorGUILayout.LabelField($"Compressão: {(config.enableCompression ? "Ativada" : "Desativada")}");
                    EditorGUILayout.LabelField($"Criptografia: {(config.enableEncryption ? "Ativada" : "Desativada")}");
                    EditorGUILayout.LabelField($"Diretório: {UltraSaveManager.SaveDirectory}");
                }
                else if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Execute o jogo para ver informações em tempo real", MessageType.Info);
                }
                else if (!isInitialized)
                {
                    EditorGUILayout.HelpBox("Sistema não inicializado. Verifique se o config está correto.", MessageType.Warning);
                }
            });

            if (config.TrackedObjects.Count > 0)
            {
                DrawSection($"Objetos Rastreados ({config.TrackedObjects.Count})", () =>
                {
                    foreach (var obj in config.TrackedObjects)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        
                        EditorGUILayout.LabelField($"🔑 {obj.saveKey}", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"Tipo: {obj.typeName}");
                        EditorGUILayout.LabelField($"Cena: {obj.sceneName}");
                        
                        if (obj.hasTransform)
                            EditorGUILayout.LabelField($"Posição: {obj.position}");
                        
                        EditorGUILayout.LabelField($"Campos: {obj.fieldCount} | Tamanho: {FormatBytes(obj.dataSize)}");
                        EditorGUILayout.LabelField($"Último Save: {obj.lastSaved:dd/MM/yyyy HH:mm:ss}");
                        
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(2);
                    }
                });
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawActionsTab(UltraSaveConfig config)
        {
            DrawSection("Ferramentas", () =>
            {
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("📁 Abrir Pasta de Saves", GUILayout.Height(35)))
                {
                    OpenSaveFolder();
                }

                if (GUILayout.Button("🔄 Atualizar Objetos", GUILayout.Height(35)))
                {
                    RefreshObjects(config);
                }
                
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("🧹 Limpar Lista de Objetos", GUILayout.Height(35)))
                {
                    ClearTrackedObjects(config);
                }

                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = dangerColor;
                
                if (GUILayout.Button("🗑️ Deletar Todos os Saves", GUILayout.Height(35)))
                {
                    DeleteAllSaves(config);
                }
                
                GUI.backgroundColor = oldColor;
                
                EditorGUILayout.EndHorizontal();
            });

            DrawSection("Configuração", () =>
            {
                if (GUILayout.Button("🔧 Recriar Configuração", GUILayout.Height(30)))
                {
                    CreateConfigFile();
                }

                if (GUILayout.Button("📋 Validar Sistema", GUILayout.Height(30)))
                {
                    ValidateSystem();
                }
            });
        }

        private bool DrawToggle(string label, bool value, string tooltip = "", bool disabled = false)
        {
            EditorGUI.BeginDisabledGroup(disabled);
            var content = new GUIContent(label, tooltip);
            var result = EditorGUILayout.Toggle(content, value);
            EditorGUI.EndDisabledGroup();
            return result;
        }

        private void DrawSection(string title, System.Action content)
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            content?.Invoke();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes / (1024f * 1024f):F1} MB";
        }

        private void OpenSaveFolder()
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

        private void RefreshObjects(UltraSaveConfig config)
        {
            if (Application.isPlaying)
            {
                UltraSaveManager.ForceRefreshObjects();
                if (config.showEditorDialogs)
                    EditorUtility.DisplayDialog("Refresh", "Objetos atualizados!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Info", "Execute o jogo para fazer refresh", "OK");
            }
        }

        private void ClearTrackedObjects(UltraSaveConfig config)
        {
            if (!config.showEditorDialogs || 
                EditorUtility.DisplayDialog("Confirmar", "Limpar lista de objetos rastreados?", "Sim", "Não"))
            {
                config.ClearTrackedObjects();
                EditorUtility.SetDirty(config);
                if (config.showEditorDialogs)
                    EditorUtility.DisplayDialog("Concluído", "Lista limpa!", "OK");
            }
        }

        private void DeleteAllSaves(UltraSaveConfig config)
        {
            if (EditorUtility.DisplayDialog("⚠️ ATENÇÃO", 
                "Deletar TODOS os saves permanentemente?\n\nEsta ação NÃO PODE ser desfeita!", 
                "Sim, Deletar", "Cancelar"))
            {
                var path = Application.isPlaying ? UltraSaveManager.SaveDirectory : 
                          Path.Combine(Application.persistentDataPath, "UltraSaves");
                
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    Directory.CreateDirectory(path);
                    config.ClearTrackedObjects();
                    EditorUtility.SetDirty(config);
                    EditorUtility.DisplayDialog("Concluído", "Todos os saves foram removidos!", "OK");
                }
            }
        }

        private void CreateConfigFile()
        {
            UltraSaveMenuItems.CreateConfig();
        }

        private void ValidateSystem()
        {
            var config = Resources.Load<UltraSaveConfig>("UltraSave/UltraSaveConfig");
            if (config != null)
            {
                EditorUtility.DisplayDialog("Sistema Válido", "✓ Configuração encontrada e válida!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Sistema Inválido", "✗ Configuração não encontrada!\n\nUse 'Tools > Ultra Save System > Create Config'", "OK");
            }
        }
    }
}