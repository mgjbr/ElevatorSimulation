# Turnaj výtahových strategií - Návod pro studenty

## Co je to turnaj?

Turnaj automaticky:
1. Najde všechny strategie v projektu (třídy implementující `IElevatorStrategy`)
2. Spustí každou strategii na 5 různých scénářích (seedech)
3. Spočítá průměrné statistiky pro každou strategii
4. Seřadí strategie podle **průměrné celkové doby** (čím nižší, tím lepší)
5. Vypíše tabulku s výsledky

## Jak přidat svou strategii

### Krok 1: Vytvořte nový soubor
Ve složce `Strategies/` vytvořte nový soubor, například `MojeStrategieStrategy.cs`

### Krok 2: Implementujte rozhraní
```csharp
namespace ElevatorSimulation.Strategies;

/// <summary>
/// Popis vaší strategie zde.
/// </summary>
public class MojeStrategieStrategy : IElevatorStrategy
{
    public MoveResult DecideNextMove(ElevatorSystem elevator)
    {
        // TODO: Implementujte svůj algoritmus
        
        return MoveResult.NoAction;
    }
}
```

### Krok 3: Implementujte logiku rozhodování

Máte přístup k těmto informacím:

```csharp
// Aktuální patro výtahu (int)
elevator.CurrentElevatorFloor

// Cestující čekající na vyzvednutí (List<RiderRequest>)
elevator.PendingRequests
// Pro každý požadavek: request.From, request.To, request.CreatedAt

// Cestující uvnitř výtahu (List<RiderRequest>)
elevator.ActiveRiders
// Pro každý požadavek: request.To (cílové patro)

// Konfigurace budovy
elevator.Building.MinFloor  // obvykle 0
elevator.Building.MaxFloor  // obvykle 9

// Aktuální čas simulace
elevator.CurrentTime
```

Musíte vrátit jednu z těchto akcí:
- `MoveResult.MoveUp` – jeď o patro nahoru
- `MoveResult.MoveDown` – jeď o patro dolů
- `MoveResult.OpenDoors` – otevři dveře (vyzvedni/vyloď cestující)
- `MoveResult.NoAction` – nečinnost (čekej)


## Tipy pro dobrou strategii

1. **Prioritizujte aktivní cestující** – Lidé ve výtahu by měli být doručeni rychle
2. **Minimalizujte zbytečné pohyby** – Nepojíždějte sem a tam bez důvodu
3. **Skupinujte požadavky** – Pokud jedete nahoru, vyzvedněte všechny cestující po cestě
4. **Zvažte směr pohybu** – Udržujte konzistentní směr dokud to dává smysl
5. **Testujte různé přístupy** – Někdy jednodušší strategie je lepší!

## Hodnocení

Hlavní metrika: **Average Total Time** (průměrná celková doba)
- Měří průměrnou dobu od vytvoření požadavku do doručení cestujícího
- Nižší hodnota = lepší strategie

Další metriky:
- **Average Wait Time** – průměrná doba čekání na vyzvednutí
- **Average Travel Time** – průměrná doba cesty ve výtahu
- **Completed** – počet dokončených požadavků (mělo by být stejné pro všechny)

## Testování

### Turnajový režim (výchozí)
Spustí všechny strategie na více scénářích:
```bash
dotnet run
```

### Test jedné strategie
V `Program.cs` nastavte:
```csharp
public const bool TournamentMode = false;
```

Pak v `Main()` odkomentujte řádek s vaší strategií:
```csharp
RunSingleSimulation("MOJE STRATEGIE", new MojeStrategieStrategy(), building, seed: RandomSeed);
```

## Často kladené otázky

**Q: Jak zjistím, jestli jsem na správném patře pro vyzvednutí/vyložení?**  
A: Zavoláním `MoveResult.OpenDoors` se automaticky vyzvednou/vyloží všichni relevantní cestující na aktuálním patře.

**Q: Co se stane, když se pokusím jet mimo budovu?**  
A: Použijte `elevator.Building.IsValidFloor(floor)` pro kontrolu.

**Q: Můžu použít vlastní proměnné/stavy?**  
A: Ano! Strategie je třída, můžete mít vlastní fieldy pro sledování stavu.

**Q: Jak vím, jestli moje strategie je dobrá?**  
A: Porovnejte ji s ostatními v turnaji. Ideálně by měla být v TOP 3!

**Q: Kolik strategií můžu přidat?**  
A: Neomezeně! Všechny se automaticky objeví v turnaji.

## Důležité poznámky

- Každý krok (move/openDoors/noAction) trvá 1 časovou jednotku
- Požadavky přicházejí náhodně během prvních 20 kroků
- Simulace končí, když jsou všichni cestující doručeni
- Turnaj běží každou strategii 5x s různými seedy pro spravedlivé porovnání
- Název vaší strategie v turnaji bude název třídy bez přípony "Strategy" (např. `MojeStrategieStrategy` → `MOJESTRATEGIE`)

## Úspěch!

Hodně štěstí s vývojem vaší strategie! 🚀

Pokud máte otázky, koukněte do existujících strategií (`FifoStrategy.cs`, `NearestFirstStrategy.cs`) jako příklady.
