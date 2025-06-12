# 🚀 Ultra Save System

Um sistema de save automático para Unity que simplifica completamente o salvamento de dados no seu jogo. Marque os campos que você quer salvar e pronto! 

<div align="center">

![Unity](https://img.shields.io/badge/Unity-2021.3+-blue?logo=unity&logoColor=white)
![C#](https://img.shields.io/badge/C%23-latest-green?logo=csharp&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-yellow)
![Status](https://img.shields.io/badge/Status-Em%20Desenvolvimento-orange)

</div>

## ✨ Por que usar?
**Ultra Save System** é a solução ideal para desenvolvedores que querem:

**🎯 Facilidade:** Apenas marque os campos com o atributo `[SaveField]`  
**🔄 Automático:** Registre uma vez, salve sempre  
**⚡ Rápido:** Serialização otimizada  
**🛡️ Confiável:** Sistema robusto de callbacks  

> ⚠️ **Projeto em desenvolvimento ativo** - Novas features e melhorias sendo adicionadas constantemente!

## 📦 Instalação

<details>
<summary><strong>Via Package Manager (Git URL)</strong></summary>

1. Abra o Package Manager no Unity
2. Clique em **"+"** → **"Add package from git URL"**
3. Cole: `https://github.com/Natteens/com.natteens.ultrasavesystem.git`

</details>

<details>
<summary><strong>Manual</strong></summary>

1. Baixe o repositório
2. Copie a pasta para `Assets/Packages/`

</details>

## 🚀 Como usar - Básico

### 1️⃣ Marque sua classe para save
```csharp
[Saveable("player_data")]
public class Player : MonoBehaviour
{
    [SaveField] public int level = 1;
    [SaveField] public float health = 100f;
    [SaveField] public string playerName = "Jogador";
    
    private void Start()
    {
        // Registra automaticamente
        this.RegisterForSave();
    }
}
```

### 2️⃣ Salve e carregue
```csharp
// Salvar 💾
await UltraSave.Save();

// Carregar 📁
await UltraSave.Load();
```

**É só isso!** O sistema cuida do resto automaticamente. ✨

---

## 🏷️ Atributos Disponíveis

### `[Saveable]`
Marca uma classe como salvável.

```csharp
[Saveable("minha_chave")]           // Com chave personalizada
[Saveable("player", true)]          // Com chave + auto-register
[Saveable]                          // Chave automática
```

### `[SaveField]`
Marca um campo específico para ser salvo.

```csharp
[SaveField] public int pontos;
[SaveField("vida_atual")] public float vida;  // Com chave personalizada
```

---

## 🔄 Callbacks Personalizados

Quer executar algo depois que os dados carregarem? Use callbacks:

```csharp
public class MinhaClasse : MonoBehaviour
{
    [SaveField] public Color corDoObjeto;
    
    void Start()
    {
        this.SetupDefaultCallbacks(
            onAfterLoad: () => AplicarCor()
        );
    }
    
    void AplicarCor()
    {
        GetComponent<Renderer>().material.color = corDoObjeto;
    }
}
```

<details>
<summary><strong>Mais opções de callbacks</strong></summary>

```csharp
this.AddOnAfterLoadCallback(() => Debug.Log("Carregou!"));
this.AddOnBeforeSaveCallback(() => Debug.Log("Vai salvar!"));
```

</details>

---

## 🎮 Exemplo Prático - RPG

```csharp
[Saveable("rpg_player")]
public class RPGPlayer : MonoBehaviour
{
    [Header("⚔️ Stats do Jogador")]
    [SaveField] public string nomeJogador = "Herói";
    [SaveField] public int nivel = 1;
    [SaveField] public float experiencia = 0f;
    [SaveField] public int ouro = 100;
    
    [Header("📍 Posição")]
    [SaveField] public Vector3 ultimaPosicao;
    [SaveField] public string ultimaFase = "Cidade";
    
    private void Start()
    {
        this.SetupDefaultCallbacks(
            onAfterLoad: () => {
                transform.position = ultimaPosicao;
                AtualizarUI();
            },
            onBeforeSave: () => {
                ultimaPosicao = transform.position;
            }
        );
    }
    
    public void GanharExperiencia(float exp)
    {
        experiencia += exp;
        _ = UltraSave.Save(); // Auto-save 🎯
    }
    
    private void AtualizarUI()
    {
        FindObjectOfType<UIManager>()?.AtualizarStats(this);
    }
}
```

---

## 📋 Tipos Suportados

<div align="center">

| ✅ **Funcionam direto** | ❌ **Precisam tratamento** |
|:---|:---|
| `int`, `float`, `string`, `bool` | `Dictionary<,>` |
| `Vector3`, `Vector2`, `Quaternion` | Referencias de GameObjects |
| `Color`, `Transform` | Componentes externos |
| Arrays e Lists básicos |  |
| Enums |  |

</div>

---

## 🗂️ Exemplo com Arrays/Listas

```csharp
[Saveable("inventario_player")]
public class Inventario : MonoBehaviour
{
    [SaveField] public List<string> itens = new List<string>();
    [SaveField] public int[] quantidades = new int[10];
    
    // Para Dictionary, use listas separadas:
    [SaveField] public List<string> chavesEquipamentos;
    [SaveField] public List<int> valoresEquipamentos;
    
    private Dictionary<string, int> equipamentos;
    
    void Start()
    {
        this.SetupDefaultCallbacks(
            onAfterLoad: RestaurarDictionary,
            onBeforeSave: SerializarDictionary
        );
    }
    
    void SerializarDictionary()
    {
        if (equipamentos != null)
        {
            chavesEquipamentos = new List<string>(equipamentos.Keys);
            valoresEquipamentos = new List<int>(equipamentos.Values);
        }
    }
    
    void RestaurarDictionary()
    {
        if (chavesEquipamentos != null && valoresEquipamentos != null)
        {
            equipamentos = new Dictionary<string, int>();
            for (int i = 0; i < chavesEquipamentos.Count; i++)
            {
                equipamentos[chavesEquipamentos[i]] = valoresEquipamentos[i];
            }
        }
    }
}
```

---

## ⚙️ Interface ICustomSaveable

Para casos mais avançados, implemente a interface:

<details>
<summary><strong>Ver exemplo completo</strong></summary>

```csharp
public class InventarioComplexo : MonoBehaviour, ICustomSaveable
{
    [SaveField] public List<Item> itens;
    
    public void OnBeforeSave()
    {
        // Preparação antes de salvar
    }
    
    public void OnAfterLoad()
    {
        // Ações após carregar
        AtualizarUI();
    }
    
    public byte[] SerializeCustomData()
    {
        // Serialização personalizada
        return JsonUtility.ToJson(itens).ToByteArray();
    }
    
    public void DeserializeCustomData(byte[] data)
    {
        // Deserialização personalizada
        string json = Encoding.UTF8.GetString(data);
        itens = JsonUtility.FromJson<List<Item>>(json);
    }
}
```

</details>

---

## 🛠️ Configuração

Crie um `UltraSaveConfig` em `Resources/`:

<details>
<summary><strong>Ver configuração</strong></summary>

```csharp
[CreateAssetMenu(menuName = "Ultra Save/Config")]
public class MeuConfig : UltraSaveConfig
{
    public override string SaveDirectory => "MeuJogo/Saves";
    public override string SaveFileName => "save_game.json";
    public override bool EnableVerboseLogging => true;
    public override bool AutoSaveOnSceneChange => true;
}
```

</details>

---

## 🎯 Comandos Úteis

```csharp
// 💾 Salvar
await UltraSave.Save();
await UltraSave.SaveToFile("backup.json");

// 📁 Carregar
await UltraSave.Load();
await UltraSave.LoadFromFile("backup.json");

// ❓ Verificar se existe save
bool existeSave = UltraSave.SaveExists();

// 🗑️ Apagar save
UltraSave.DeleteSave();

// 🎯 Salvar objeto específico
meuObjeto.RegisterForSave("chave_especial");
```

---

## 💡 Dicas de Performance

| 🚫 **Evite** | ✅ **Faça** |
|:---|:---|
| Salvar todo frame | Use timers ou eventos |
| Muitos campos desnecessários | Marque só o essencial |
| Chaves duplicadas | Use chaves únicas |
| Save sem verificação | Use `SaveExists()` |

---

## 🔧 Troubleshooting

<details>
<summary><strong>❌ "Field não está sendo salvo"</strong></summary>

- ✅ Verifique se tem o atributo `[SaveField]`
- ✅ Certifique-se que chamou `RegisterForSave()`

</details>

<details>
<summary><strong>❌ "Save não persiste entre sessões"</strong></summary>

- ✅ Verifique as permissões de escrita
- ✅ Confira se o `SaveDirectory` está correto

</details>

<details>
<summary><strong>❌ "Performance ruim"</strong></summary>

- ✅ Reduza a frequência de saves
- ✅ Limite os campos com `[SaveField]`

</details>

---

<div align="center">

**Feito com ❤️ para devs Unity**

[![GitHub](https://img.shields.io/badge/GitHub-Natteens-blue?logo=github)](https://github.com/Natteens)

</div>