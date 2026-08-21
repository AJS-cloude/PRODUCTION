using System.Globalization;
using System.Text;
using GameForge.Spec;

namespace GameForge.Gen;

/// <summary>
/// 싱글 타워디펜스 장르(S급). Core 는 경로를 1차원 거리로 추상화한다.
/// (Unity 뷰가 그 거리를 실제 폴리라인 위 좌표로 옮긴다 — 밸런스는 1D 로 충분하고, 시뮬이 수만 배 빨라진다.)
/// </summary>
public static class TowerDefenseEmitter
{
    public static void Emit(Emitter e, GameSpec spec, string coreDir)
    {
        var ns = spec.SafeName + ".Core";
        void Add(string file, string body) => e.Add($"{coreDir}/{file}", body.Replace("__NS__", ns));

        Add("GameData.cs", BuildData(spec));
        Add("CombatSystem.cs", CombatSystem);
        Add("WaveSystem.cs", WaveSystem);
        Add("AutoPlayer.cs", AutoPlayer);
        Add("GameUi.cs", GameUi);
        Add("GameFactory.cs", Factory);
    }

    static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string S(string v) => "\"" + (v ?? "").Replace("\"", "\\\"") + "\"";

    static string BuildData(GameSpec spec)
    {
        var w = spec.Waves;
        var sb = new StringBuilder();
        sb.AppendLine("namespace __NS__");
        sb.AppendLine("{");
        sb.AppendLine("    public readonly struct TowerDef");
        sb.AppendLine("    {");
        sb.AppendLine("        public readonly string Id, Name, Targeting, Icon;");
        sb.AppendLine("        public readonly double Damage, Range, FireRate, SplashRadius, SlowPercent, SlowDuration, Cost;");
        sb.AppendLine("        public TowerDef(string id, string name, double damage, double range, double fireRate,");
        sb.AppendLine("            double splashRadius, double slowPercent, double slowDuration, double cost, string targeting, string icon)");
        sb.AppendLine("        { Id = id; Name = name; Damage = damage; Range = range; FireRate = fireRate;");
        sb.AppendLine("          SplashRadius = splashRadius; SlowPercent = slowPercent; SlowDuration = slowDuration;");
        sb.AppendLine("          Cost = cost; Targeting = targeting; Icon = icon; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public readonly struct EnemyDef");
        sb.AppendLine("    {");
        sb.AppendLine("        public readonly string Id, Name, Icon;");
        sb.AppendLine("        public readonly double Hp, Speed, Armor, Reward; public readonly int Damage;");
        sb.AppendLine("        public EnemyDef(string id, string name, double hp, double speed, double armor, double reward, int damage, string icon)");
        sb.AppendLine("        { Id = id; Name = name; Hp = hp; Speed = speed; Armor = armor; Reward = reward; Damage = damage; Icon = icon; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public readonly struct ResourceDef");
        sb.AppendLine("    {");
        sb.AppendLine("        public readonly string Id, Name, Icon; public readonly double Start; public readonly bool Premium;");
        sb.AppendLine("        public ResourceDef(string id, string name, double start, bool premium, string icon)");
        sb.AppendLine("        { Id = id; Name = name; Start = start; Premium = premium; Icon = icon; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>기획서에서 그대로 뽑아낸 밸런스 테이블.</summary>");
        sb.AppendLine("    public static class GameData");
        sb.AppendLine("    {");
        sb.AppendLine($"        public const string DisplayName = {S(spec.DisplayName)};");
        sb.AppendLine($"        public const int TickRate = {spec.TickRate};");
        sb.AppendLine();
        sb.AppendLine($"        public const int WaveCount = {w.Count};");
        sb.AppendLine("        /// <summary>const 가 아닌 이유: 밸런스 튜너가 이 값을 갈아끼우며 최적 난이도를 찾는다.</summary>");
        sb.AppendLine($"        public static double HpGrowth = {N(w.HpGrowth)};");
        sb.AppendLine($"        public static double RewardGrowth = {N(w.RewardGrowth)};   // 웨이브당 처치 보상 배율");
        sb.AppendLine($"        const double OriginalHpGrowth = {N(w.HpGrowth)};");
        sb.AppendLine($"        const double OriginalRewardGrowth = {N(w.RewardGrowth)};");
        sb.AppendLine();
        sb.AppendLine("        public static void ApplyTuning(double hpGrowth, double rewardGrowth)");
        sb.AppendLine("        {");
        sb.AppendLine("            HpGrowth = hpGrowth > 0 ? hpGrowth : OriginalHpGrowth;");
        sb.AppendLine("            RewardGrowth = rewardGrowth > 0 ? rewardGrowth : OriginalRewardGrowth;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static void ResetTuning()");
        sb.AppendLine("        { HpGrowth = OriginalHpGrowth; RewardGrowth = OriginalRewardGrowth; }");
        sb.AppendLine($"        public const double CountGrowth = {N(w.CountGrowth)};");
        sb.AppendLine($"        public const int BaseEnemyCount = {w.BaseEnemyCount};");
        sb.AppendLine($"        public const double SpawnInterval = {N(w.SpawnInterval)};");
        sb.AppendLine($"        public const double PrepareTime = {N(w.PrepareTime)};");
        sb.AppendLine($"        public const int PathLength = {w.PathLength};");
        sb.AppendLine($"        public const int StartLives = {w.StartLives};");
        sb.AppendLine($"        public const double StartGold = {N(w.StartGold)};");
        sb.AppendLine($"        public const int BossEvery = {w.BossEvery};");
        sb.AppendLine($"        public const double BossHpMul = {N(w.BossHpMul)};");
        sb.AppendLine($"        public const int SlotCount = {Math.Max(4, w.PathLength)};");
        sb.AppendLine($"        public const int MaxTowerLevel = {spec.TowerUpgrade.MaxLevel};");
        sb.AppendLine($"        public const double UpgradeCostMul = {N(spec.TowerUpgrade.CostMul)};");
        sb.AppendLine($"        public const double UpgradeDamageMul = {N(spec.TowerUpgrade.DamageMul)};");
        sb.AppendLine();

        sb.AppendLine("        public static readonly ResourceDef[] Resources =");
        sb.AppendLine("        {");
        foreach (var r in spec.Resources)
            sb.AppendLine($"            new ResourceDef({S(r.Id)}, {S(r.Name)}, {N(r.Start)}, {(r.Premium ? "true" : "false")}, {S(r.Icon)}),");
        sb.AppendLine("        };");
        sb.AppendLine();

        sb.AppendLine("        public static readonly TowerDef[] Towers =");
        sb.AppendLine("        {");
        foreach (var t in spec.Towers)
            sb.AppendLine($"            new TowerDef({S(t.Id)}, {S(t.Name)}, {N(t.Damage)}, {N(t.Range)}, {N(t.FireRate)}, " +
                          $"{N(t.SplashRadius)}, {N(t.SlowPercent)}, {N(t.SlowDuration)}, {N(t.Cost)}, {S(t.Targeting)}, {S(t.Icon)}),");
        sb.AppendLine("        };");
        sb.AppendLine();

        sb.AppendLine("        public static readonly EnemyDef[] Enemies =");
        sb.AppendLine("        {");
        foreach (var en in spec.Enemies)
            sb.AppendLine($"            new EnemyDef({S(en.Id)}, {S(en.Name)}, {N(en.Hp)}, {N(en.Speed)}, {N(en.Armor)}, " +
                          $"{N(en.Reward)}, {en.Damage}, {S(en.Icon)}),");
        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    const string CombatSystem = """
using System;

namespace __NS__
{
    public struct Enemy
    {
        public int Def;
        public Fx Pos;          // 경로 시작점으로부터의 거리
        public Fx Hp;
        public Fx SlowTimer;
        public Fx SlowPercent;
        public bool Alive;
    }

    public struct Tower
    {
        public int Def;
        public int Slot;
        public int Level;       // 1부터. 강화할수록 공격력이 배율로 오른다.
        public Fx Pos;          // 경로 위 투영 위치
        public Fx Cooldown;
        public bool Active;
    }

    /// <summary>적 이동 + 타워 사격. 전부 고정소수점이라 몇 번을 돌려도 결과가 같다.</summary>
    public sealed class CombatSystem : ISimSystem
    {
        public const int MaxEnemies = 512;

        public readonly Enemy[] Enemies = new Enemy[MaxEnemies];
        public readonly Tower[] Slots = new Tower[GameData.SlotCount];

        /// <summary>현재 웨이브의 보상 배율. WaveSystem 이 웨이브 시작마다 갱신한다.</summary>
        public double RewardScale = 1;

        public int AliveCount { get; private set; }
        public int Leaked { get; private set; }
        public int Killed { get; private set; }

        readonly Fx _pathEnd = Fx.FromInt(GameData.PathLength);
        readonly Fx _slotSpacing;

        public CombatSystem()
        {
            _slotSpacing = Fx.FromInt(GameData.PathLength) / Fx.FromInt(GameData.SlotCount);
            for (int i = 0; i < Slots.Length; i++)
            {
                Slots[i].Slot = i;
                // 슬롯은 경로를 따라 균등 배치. 타워 사거리는 이 위치 기준으로 판정한다.
                Slots[i].Pos = _slotSpacing * (i + 1) - _slotSpacing / Fx.FromInt(2);
            }
        }

        public void Init(SimWorld world) { }

        public bool Spawn(int defIndex, Fx hp)
        {
            for (int i = 0; i < MaxEnemies; i++)
            {
                if (Enemies[i].Alive) continue;
                Enemies[i].Def = defIndex;
                Enemies[i].Pos = Fx.Zero;
                Enemies[i].Hp = hp;
                Enemies[i].SlowTimer = Fx.Zero;
                Enemies[i].SlowPercent = Fx.Zero;
                Enemies[i].Alive = true;
                AliveCount++;
                return true;
            }
            return false;   // 풀 고갈: 스펙의 웨이브 물량이 과하다는 신호
        }

        public bool TryBuild(SimWorld world, int slot, int towerDef)
        {
            if (slot < 0 || slot >= Slots.Length || Slots[slot].Active) return false;
            var def = GameData.Towers[towerDef];
            if (!world.Resources.TrySpend("gold", BigNumber.From(def.Cost))) return false;
            Slots[slot].Def = towerDef;
            Slots[slot].Level = 1;
            Slots[slot].Cooldown = Fx.Zero;
            Slots[slot].Active = true;
            world.Raise(new SimEvent(SimEventKind.TowerBuilt, def.Id, slot));
            return true;
        }

        /// <summary>강화 비용. 레벨이 오를수록 지수로 비싸져 골드 흡수처가 된다.</summary>
        public double UpgradeCostOf(int slot)
        {
            if (slot < 0 || slot >= Slots.Length || !Slots[slot].Active) return double.MaxValue;
            var def = GameData.Towers[Slots[slot].Def];
            return def.Cost * Math.Pow(GameData.UpgradeCostMul, Slots[slot].Level);
        }

        public bool CanUpgrade(SimWorld world, int slot)
            => slot >= 0 && slot < Slots.Length && Slots[slot].Active
               && Slots[slot].Level < GameData.MaxTowerLevel
               && world.Resources.CanAfford("gold", BigNumber.From(UpgradeCostOf(slot)));

        public bool TryUpgrade(SimWorld world, int slot)
        {
            if (!CanUpgrade(world, slot)) return false;
            if (!world.Resources.TrySpend("gold", BigNumber.From(UpgradeCostOf(slot)))) return false;
            Slots[slot].Level++;
            world.Raise(new SimEvent(SimEventKind.TowerBuilt, GameData.Towers[Slots[slot].Def].Id, slot));
            return true;
        }

        /// <summary>레벨이 반영된 실제 공격력.</summary>
        public Fx DamageOf(int slot)
        {
            var def = GameData.Towers[Slots[slot].Def];
            return Fx.FromDouble(def.Damage * Math.Pow(GameData.UpgradeDamageMul, Slots[slot].Level - 1));
        }

        public void Tick(SimWorld world, Fx dt)
        {
            MoveEnemies(world, dt);
            FireTowers(world, dt);
        }

        void MoveEnemies(SimWorld world, Fx dt)
        {
            for (int i = 0; i < MaxEnemies; i++)
            {
                if (!Enemies[i].Alive) continue;
                var def = GameData.Enemies[Enemies[i].Def];

                Fx speed = Fx.FromDouble(def.Speed);
                if (Enemies[i].SlowTimer > Fx.Zero)
                {
                    speed = speed * (Fx.One - Enemies[i].SlowPercent);
                    Enemies[i].SlowTimer = Fx.Max(Fx.Zero, Enemies[i].SlowTimer - dt);
                }

                Enemies[i].Pos = Enemies[i].Pos + speed * dt;
                if (Enemies[i].Pos < _pathEnd) continue;

                // 끝까지 통과 = 라이프 손실
                Enemies[i].Alive = false;
                AliveCount--;
                Leaked += def.Damage;
                world.Raise(new SimEvent(SimEventKind.EnemyLeaked, def.Id, def.Damage));
            }
        }

        void FireTowers(SimWorld world, Fx dt)
        {
            for (int s = 0; s < Slots.Length; s++)
            {
                if (!Slots[s].Active) continue;
                var def = GameData.Towers[Slots[s].Def];

                Slots[s].Cooldown = Slots[s].Cooldown - dt;
                if (Slots[s].Cooldown > Fx.Zero) continue;

                int target = FindTarget(Slots[s].Pos, Fx.FromDouble(def.Range), def.Targeting);
                if (target < 0) { Slots[s].Cooldown = Fx.Zero; continue; }

                Slots[s].Cooldown = Fx.One / Fx.FromDouble(def.FireRate <= 0 ? 1 : def.FireRate);
                Fx dmg = DamageOf(s);
                Hit(world, target, def, dmg);

                if (def.SplashRadius > 0) Splash(world, target, def, dmg);
            }
        }

        void Splash(SimWorld world, int center, TowerDef def, Fx dmg)
        {
            Fx radius = Fx.FromDouble(def.SplashRadius);
            Fx at = Enemies[center].Pos;
            for (int i = 0; i < MaxEnemies; i++)
            {
                if (i == center || !Enemies[i].Alive) continue;
                if (Fx.Abs(Enemies[i].Pos - at) > radius) continue;
                Hit(world, i, def, dmg);
            }
        }

        void Hit(SimWorld world, int index, TowerDef towerDef, Fx rawDamage)
        {
            if (!Enemies[index].Alive) return;
            var enemyDef = GameData.Enemies[Enemies[index].Def];

            // 방어력은 고정 감산이되 최소 1은 들어간다(무적 방지).
            Fx dmg = Fx.Max(Fx.One, rawDamage - Fx.FromDouble(enemyDef.Armor));
            Enemies[index].Hp = Enemies[index].Hp - dmg;

            if (towerDef.SlowPercent > 0)
            {
                Enemies[index].SlowPercent = Fx.FromDouble(Math.Min(0.9, towerDef.SlowPercent));
                Enemies[index].SlowTimer = Fx.FromDouble(towerDef.SlowDuration);
            }

            if (Enemies[index].Hp > Fx.Zero) return;

            Enemies[index].Alive = false;
            AliveCount--;
            Killed++;
            world.Resources.Add("gold", BigNumber.From(enemyDef.Reward * RewardScale));
            world.Raise(new SimEvent(SimEventKind.EnemyKilled, enemyDef.Id, index));
        }

        int FindTarget(Fx towerPos, Fx range, string mode)
        {
            int best = -1;
            Fx bestKey = Fx.Zero;
            bool has = false;

            for (int i = 0; i < MaxEnemies; i++)
            {
                if (!Enemies[i].Alive) continue;
                Fx dist = Fx.Abs(Enemies[i].Pos - towerPos);
                if (dist > range) continue;

                Fx key;
                switch (mode)
                {
                    case "last":      key = -Enemies[i].Pos; break;
                    case "nearest":   key = -dist; break;
                    case "strongest": key = Enemies[i].Hp; break;
                    case "weakest":   key = -Enemies[i].Hp; break;
                    default:          key = Enemies[i].Pos; break;   // first: 가장 앞선 적
                }

                if (!has || key > bestKey) { bestKey = key; best = i; has = true; }
            }
            return best;
        }

        public void ClearAll()
        {
            for (int i = 0; i < MaxEnemies; i++) Enemies[i].Alive = false;
            AliveCount = 0;
        }
    }
}
""";

    const string WaveSystem = """
using System;

namespace __NS__
{
    public enum WavePhase { Prepare, Spawning, Clearing, Finished }

    /// <summary>웨이브 진행과 라이프 관리. 곡선은 전부 기획서 수치에서 나온다.</summary>
    public sealed class WaveSystem : ISimSystem
    {
        public int WaveIndex { get; private set; }        // 0-based
        public int Lives { get; private set; }
        public WavePhase Phase { get; private set; }
        public bool Cleared { get; private set; }

        CombatSystem _combat;
        Fx _timer;
        int _toSpawn;
        int _lastLeaked;

        public int WaveNumber => WaveIndex + 1;
        public bool IsBossWave => GameData.BossEvery > 0 && WaveNumber % GameData.BossEvery == 0;

        public void Init(SimWorld world)
        {
            _combat = world.Get<CombatSystem>();
            Lives = GameData.StartLives;
            Phase = WavePhase.Prepare;
            _timer = Fx.FromDouble(GameData.PrepareTime);
        }

        /// <summary>
        /// 해당 웨이브 적 1마리의 체력. 보스 배율은 보스 한 마리에만 붙는다.
        /// (웨이브 전원에게 붙이면 보스 웨이브가 그냥 벽이 되어 진행이 막힌다.)
        /// </summary>
        public static double EnemyHpAt(int waveNumber, int enemyDef, bool isBoss = false)
        {
            double hp = GameData.Enemies[enemyDef].Hp * Math.Pow(GameData.HpGrowth, waveNumber - 1);
            return isBoss ? hp * GameData.BossHpMul : hp;
        }

        public static int EnemyCountAt(int waveNumber)
            => Math.Max(1, (int)Math.Round(GameData.BaseEnemyCount * Math.Pow(GameData.CountGrowth, waveNumber - 1)));

        public void Tick(SimWorld world, Fx dt)
        {
            if (Phase == WavePhase.Finished) return;

            // 누수분을 라이프에서 차감
            if (_combat.Leaked != _lastLeaked)
            {
                Lives -= _combat.Leaked - _lastLeaked;
                _lastLeaked = _combat.Leaked;
                if (Lives <= 0)
                {
                    Lives = 0;
                    Phase = WavePhase.Finished;
                    world.Raise(new SimEvent(SimEventKind.GameOver, "", WaveNumber));
                    world.End();
                    return;
                }
            }

            switch (Phase)
            {
                case WavePhase.Prepare:
                    _timer = _timer - dt;
                    if (_timer > Fx.Zero) break;
                    StartWave(world);
                    break;

                case WavePhase.Spawning:
                    _timer = _timer - dt;
                    if (_timer > Fx.Zero) break;
                    SpawnOne();
                    _timer = Fx.FromDouble(GameData.SpawnInterval);
                    if (_toSpawn <= 0) Phase = WavePhase.Clearing;
                    break;

                case WavePhase.Clearing:
                    if (_combat.AliveCount > 0) break;
                    world.Raise(new SimEvent(SimEventKind.WaveCleared, "", WaveNumber));
                    if (WaveNumber >= GameData.WaveCount)
                    {
                        Cleared = true;
                        Phase = WavePhase.Finished;
                        world.Raise(new SimEvent(SimEventKind.Victory, "", WaveNumber));
                        world.End();
                        break;
                    }
                    WaveIndex++;
                    Phase = WavePhase.Prepare;
                    _timer = Fx.FromDouble(GameData.PrepareTime);
                    break;
            }
        }

        void StartWave(SimWorld world)
        {
            _toSpawn = EnemyCountAt(WaveNumber);
            _combat.RewardScale = Math.Pow(GameData.RewardGrowth, WaveNumber - 1);
            Phase = WavePhase.Spawning;
            _timer = Fx.Zero;
            world.Raise(new SimEvent(SimEventKind.WaveStarted, "", WaveNumber));
        }

        void SpawnOne()
        {
            if (_toSpawn <= 0) return;
            // 웨이브가 오를수록 뒤쪽(더 강한) 적 종류가 섞인다.
            int pool = Math.Min(GameData.Enemies.Length, 1 + WaveIndex / 3);
            int def = (_toSpawn + WaveIndex) % pool;

            // 보스 웨이브의 마지막 한 마리만 보스로 등장한다.
            bool isBoss = IsBossWave && _toSpawn == 1;
            _combat.Spawn(def, Fx.FromDouble(EnemyHpAt(WaveNumber, def, isBoss)));
            _toSpawn--;
        }
    }
}
""";

    const string AutoPlayer = """
using System;

namespace __NS__
{
    /// <summary>
    /// 밸런스 시뮬용 가상 플레이어. 특정 타워를 선호하도록 설정할 수 있어서
    /// "이 타워만 계속 쓰면 몇 웨이브까지 가나" 같은 채용률/OP 검증에 쓴다.
    /// </summary>
    public sealed class AutoPlayer
    {
        readonly SimWorld _world;
        readonly CombatSystem _combat;
        readonly int[] _preference;
        readonly int[] _slotOrder;
        int _placed;

        /// <param name="preference">선호 타워 인덱스 순서. null 이면 싼 것부터.</param>
        public AutoPlayer(SimWorld world, int[] preference = null)
        {
            _world = world;
            _combat = world.Get<CombatSystem>();
            _preference = preference ?? DefaultPreference();
            _slotOrder = SpreadOrder(GameData.SlotCount);
        }

        /// <summary>
        /// 슬롯을 0번부터 차례로 채우면 타워가 경로 시작점에만 뭉쳐 사거리가 겹치고 뒤가 텅 빈다.
        /// 이미 놓인 타워들로부터 가장 멀리 떨어진 자리를 계속 고르는 순서를 미리 만들어 둔다.
        /// </summary>
        static int[] SpreadOrder(int count)
        {
            var order = new int[count];
            var taken = new bool[count];
            order[0] = count / 2;
            taken[order[0]] = true;

            for (int n = 1; n < count; n++)
            {
                int best = -1, bestDist = -1;
                for (int i = 0; i < count; i++)
                {
                    if (taken[i]) continue;
                    int nearest = int.MaxValue;
                    for (int j = 0; j < n; j++)
                    {
                        int d = Math.Abs(i - order[j]);
                        if (d < nearest) nearest = d;
                    }
                    if (nearest > bestDist) { bestDist = nearest; best = i; }
                }
                order[n] = best;
                taken[best] = true;
            }
            return order;
        }

        static int[] DefaultPreference()
        {
            var idx = new int[GameData.Towers.Length];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            Array.Sort(idx, (a, b) => GameData.Towers[a].Cost.CompareTo(GameData.Towers[b].Cost));
            return idx;
        }

        /// <summary>
        /// 선호 1순위 타워를 고수하고, 그게 아직 비싸면 돈을 모은다.
        /// (아무거나 즉시 사면 항상 최저가 타워만 지어져서 채용률 통계가 무의미해진다.)
        /// 단 한 기도 없는 상태에서는 첫 방어를 위해 살 수 있는 것 중 가장 싼 것을 허용한다.
        /// </summary>
        public void BuildGreedy()
        {
            bool built = true;
            while (built && _placed < GameData.SlotCount)
            {
                built = false;
                int slot = _slotOrder[_placed];
                int lead = _preference.Length > 0 ? _preference[0] : 0;

                if (_world.Resources.CanAfford("gold", BigNumber.From(GameData.Towers[lead].Cost)))
                {
                    if (_combat.TryBuild(_world, slot, lead)) { _placed++; built = true; continue; }
                }

                if (_placed > 0) break;   // 이미 방어선이 있으면 1순위를 기다린다

                for (int p = 1; p < _preference.Length; p++)
                {
                    int def = _preference[p];
                    if (!_world.Resources.CanAfford("gold", BigNumber.From(GameData.Towers[def].Cost))) continue;
                    if (!_combat.TryBuild(_world, slot, def)) continue;
                    _placed++;
                    built = true;
                    break;
                }
            }

            UpgradeGreedy();
        }

        /// <summary>남는 골드는 가장 싸게 올릴 수 있는 타워에 붓는다. 후반 화력은 여기서 나온다.</summary>
        void UpgradeGreedy()
        {
            bool upgraded = true;
            while (upgraded)
            {
                upgraded = false;
                int best = -1;
                double bestCost = double.MaxValue;

                for (int i = 0; i < _placed; i++)
                {
                    int s = _slotOrder[i];
                    if (!_combat.CanUpgrade(_world, s)) continue;
                    double cost = _combat.UpgradeCostOf(s);
                    if (cost < bestCost) { bestCost = cost; best = s; }
                }

                if (best >= 0) upgraded = _combat.TryUpgrade(_world, best);
            }
        }
    }
}
""";

    const string GameUi = """
namespace __NS__
{
    /// <summary>
    /// 타워디펜스 화면 구성. Core 의 1차원 경로 좌표를 그대로 UiTrack 으로 넘긴다.
    /// 슬롯 버튼은 비었으면 건설, 차 있으면 강화로 동작한다.
    /// </summary>
    public sealed class GameUi : IUiProvider
    {
        readonly SimWorld _world;
        readonly CombatSystem _combat;
        readonly WaveSystem _waves;
        int _selected;

        public GameUi(SimWorld world)
        {
            _world = world;
            _combat = world.Get<CombatSystem>();
            _waves = world.Get<WaveSystem>();
        }

        public void BuildUi(UiModel ui)
        {
            ui.Title = GameData.DisplayName;
            ui.ActionHeader = "타워 선택 후 경로의 슬롯을 누르세요";

            ui.Stats.Add($"웨이브 {_waves.WaveNumber}/{GameData.WaveCount}" + (_waves.IsBossWave ? "  [보스]" : ""));
            ui.Stats.Add($"라이프 {_waves.Lives}    골드 {_world.Resources.Get("gold")}");

            if (_waves.Cleared) ui.Banner = "클리어!";
            else if (_waves.Lives <= 0) ui.Banner = "게임 오버";

            var track = new UiTrack { Length = GameData.PathLength, OnSlot = TapSlot };

            for (int i = 0; i < CombatSystem.MaxEnemies; i++)
            {
                if (!_combat.Enemies[i].Alive) continue;
                track.Markers.Add((_combat.Enemies[i].Pos.ToDouble(), 20 + _combat.Enemies[i].Def));
            }

            for (int i = 0; i < _combat.Slots.Length; i++)
                track.Slots.Add((_combat.Slots[i].Pos.ToDouble(), _combat.Slots[i].Def, _combat.Slots[i].Active));

            ui.Track = track;

            for (int i = 0; i < GameData.Towers.Length; i++)
            {
                int index = i;
                var def = GameData.Towers[i];
                ui.Actions.Add(new UiAction
                {
                    Label = def.Name,
                    Sub = $"가격 {def.Cost}  공격 {def.Damage}  사거리 {def.Range}  연사 {def.FireRate}/s",
                    Icon = def.Icon,
                    PaletteIndex = i,
                    Selected = i == _selected,
                    Execute = () => _selected = index,
                });
            }
        }

        void TapSlot(int slot)
        {
            if (_combat.Slots[slot].Active) _combat.TryUpgrade(_world, slot);
            else _combat.TryBuild(_world, slot, _selected);
        }
    }
}
""";

    const string Factory = """
namespace __NS__
{
    /// <summary>게임 한 판을 조립한다. Unity 도 시뮬레이터도 이 함수를 통해서만 시작한다.</summary>
    public static class GameFactory
    {
        public static SimWorld Create(ulong seed = 12345)
        {
            var world = new SimWorld(GameData.TickRate, seed);
            foreach (var r in GameData.Resources) world.Resources.Define(r.Id, r.Start);
            world.Resources.Define("gold", GameData.StartGold);   // 웨이브 설정의 시작 골드가 우선
            world.Add(new CombatSystem());
            world.Add(new WaveSystem());
            world.Init();
            return world;
        }

        public static IUiProvider CreateUi(SimWorld world) => new GameUi(world);
    }
}
""";
}
