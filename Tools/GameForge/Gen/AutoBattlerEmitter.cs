using System.Globalization;
using System.Text;
using GameForge.Spec;

namespace GameForge.Gen;

/// <summary>오토배틀러 / 방치형 RPG(S급). 팀을 꾸려 스테이지를 자동 전투로 밀고, 보상으로 유닛을 강화한다.</summary>
public static class AutoBattlerEmitter
{
    public static void Emit(Emitter e, GameSpec spec, string coreDir)
    {
        var ns = spec.SafeName + ".Core";
        void Add(string file, string body) => e.Add($"{coreDir}/{file}", body.Replace("__NS__", ns));

        Add("GameData.cs", BuildData(spec));
        Add("Roster.cs", Roster);
        Add("BattleSystem.cs", Battle);
        Add("AutoPlayer.cs", AutoPlayer);
        Add("GameUi.cs", GameUi);
        Add("GameFactory.cs", Factory);
    }

    static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string S(string v) => "\"" + (v ?? "").Replace("\"", "\\\"") + "\"";

    static string BuildData(GameSpec spec)
    {
        var st = spec.Stages;
        var sb = new StringBuilder();
        sb.AppendLine("namespace __NS__");
        sb.AppendLine("{");
        sb.AppendLine("    public readonly struct ResourceDef");
        sb.AppendLine("    {");
        sb.AppendLine("        public readonly string Id, Name, Icon; public readonly double Start; public readonly bool Premium;");
        sb.AppendLine("        public ResourceDef(string id, string name, double start, bool premium, string icon)");
        sb.AppendLine("        { Id = id; Name = name; Start = start; Premium = premium; Icon = icon; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public readonly struct UnitDef");
        sb.AppendLine("    {");
        sb.AppendLine("        public readonly string Id, Name, Role, Icon;");
        sb.AppendLine("        public readonly double Hp, Attack, AttackSpeed, Cost;");
        sb.AppendLine("        public UnitDef(string id, string name, double hp, double attack, double attackSpeed,");
        sb.AppendLine("            double cost, string role, string icon)");
        sb.AppendLine("        { Id = id; Name = name; Hp = hp; Attack = attack; AttackSpeed = attackSpeed;");
        sb.AppendLine("          Cost = cost; Role = role; Icon = icon; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>기획서에서 그대로 뽑아낸 밸런스 테이블.</summary>");
        sb.AppendLine("    public static class GameData");
        sb.AppendLine("    {");
        sb.AppendLine($"        public const string DisplayName = {S(spec.DisplayName)};");
        sb.AppendLine($"        public const int TickRate = {spec.TickRate};");
        sb.AppendLine();
        sb.AppendLine($"        public const int StageCount = {st.Count};");
        sb.AppendLine($"        public const int EnemyCount = {st.EnemyCount};");
        sb.AppendLine($"        public const double EnemyBaseHp = {N(st.EnemyBaseHp)};");
        sb.AppendLine($"        public const double EnemyBaseAttack = {N(st.EnemyBaseAttack)};");
        sb.AppendLine($"        public const double EnemyAttackSpeed = {N(st.EnemyAttackSpeed)};");
        sb.AppendLine($"        public const double RewardBase = {N(st.RewardBase)};");
        sb.AppendLine($"        public const int TeamSize = {st.TeamSize};");
        sb.AppendLine($"        public const double UpgradeCostMul = {N(st.UpgradeCostMul)};");
        sb.AppendLine($"        public const int MaxUnitLevel = {st.MaxUnitLevel};");
        sb.AppendLine($"        public const double BattleTimeout = {N(st.BattleTimeout)};");
        sb.AppendLine($"        public const double IdleRewardPerSec = {N(st.IdleRewardPerSec)};");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>튜너가 갈아끼우는 세 축.</summary>");
        sb.AppendLine($"        public static double HpGrowth = {N(st.HpGrowth)};");
        sb.AppendLine($"        public static double AttackGrowth = {N(st.AttackGrowth)};");
        sb.AppendLine($"        public static double RewardGrowth = {N(st.RewardGrowth)};");
        sb.AppendLine($"        public static double UpgradeStatMul = {N(st.UpgradeStatMul)};");
        sb.AppendLine($"        const double OriginalHpGrowth = {N(st.HpGrowth)};");
        sb.AppendLine($"        const double OriginalAttackGrowth = {N(st.AttackGrowth)};");
        sb.AppendLine($"        const double OriginalRewardGrowth = {N(st.RewardGrowth)};");
        sb.AppendLine($"        const double OriginalUpgradeStatMul = {N(st.UpgradeStatMul)};");
        sb.AppendLine();
        sb.AppendLine("        public static void ApplyTuning(double stageGrowth, double rewardGrowth)");
        sb.AppendLine("        {");
        sb.AppendLine("            HpGrowth = stageGrowth > 0 ? stageGrowth : OriginalHpGrowth;");
        sb.AppendLine("            AttackGrowth = stageGrowth > 0 ? 1 + (stageGrowth - 1) * 0.8 : OriginalAttackGrowth;");
        sb.AppendLine("            RewardGrowth = rewardGrowth > 0 ? rewardGrowth : OriginalRewardGrowth;");
        sb.AppendLine("            UpgradeStatMul = OriginalUpgradeStatMul;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static void ResetTuning()");
        sb.AppendLine("        {");
        sb.AppendLine("            HpGrowth = OriginalHpGrowth; AttackGrowth = OriginalAttackGrowth;");
        sb.AppendLine("            RewardGrowth = OriginalRewardGrowth; UpgradeStatMul = OriginalUpgradeStatMul;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static readonly ResourceDef[] Resources =");
        sb.AppendLine("        {");
        foreach (var r in spec.Resources)
            sb.AppendLine($"            new ResourceDef({S(r.Id)}, {S(r.Name)}, {N(r.Start)}, {(r.Premium ? "true" : "false")}, {S(r.Icon)}),");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        public static readonly UnitDef[] Units =");
        sb.AppendLine("        {");
        foreach (var u in spec.Units)
            sb.AppendLine($"            new UnitDef({S(u.Id)}, {S(u.Name)}, {N(u.Hp)}, {N(u.Attack)}, {N(u.AttackSpeed)}, " +
                          $"{N(u.Cost)}, {S(u.Role)}, {S(u.Icon)}),");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        public static double EnemyHpAt(int stage)");
        sb.AppendLine("            => EnemyBaseHp * System.Math.Pow(HpGrowth, stage - 1);");
        sb.AppendLine("        public static double EnemyAttackAt(int stage)");
        sb.AppendLine("            => EnemyBaseAttack * System.Math.Pow(AttackGrowth, stage - 1);");
        sb.AppendLine("        public static double RewardAt(int stage)");
        sb.AppendLine("            => RewardBase * System.Math.Pow(RewardGrowth, stage - 1);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    const string Roster = """
using System;

namespace __NS__
{
    /// <summary>보유 유닛과 그 레벨. 레벨이 오르면 체력/공격이 같은 배율로 오른다.</summary>
    public sealed class Roster : ISimSystem
    {
        public readonly int[] Levels;   // 0 = 미보유

        public Roster() { Levels = new int[GameData.Units.Length]; }

        public int Count => Levels.Length;

        public void Init(SimWorld world) { }
        public void Tick(SimWorld world, Fx dt) { }

        public bool Owns(int index) => Levels[index] > 0;

        public int OwnedCount
        {
            get { int n = 0; for (int i = 0; i < Levels.Length; i++) if (Levels[i] > 0) n++; return n; }
        }

        public double StatMul(int index)
            => Math.Pow(GameData.UpgradeStatMul, Math.Max(0, Levels[index] - 1));

        public double HpOf(int index) => GameData.Units[index].Hp * StatMul(index);
        public double AttackOf(int index) => GameData.Units[index].Attack * StatMul(index);

        /// <summary>미보유면 영입 비용, 보유 중이면 다음 레벨 비용.</summary>
        public double CostOf(int index)
            => GameData.Units[index].Cost * Math.Pow(GameData.UpgradeCostMul, Levels[index]);

        public bool IsMaxed(int index) => Levels[index] >= GameData.MaxUnitLevel;

        public bool CanBuy(SimWorld world, int index)
            => !IsMaxed(index)
               && (Owns(index) || OwnedCount < GameData.TeamSize)
               && world.Resources.CanAfford(GameData.Resources[0].Id, BigNumber.From(CostOf(index)));

        public bool TryBuy(SimWorld world, int index)
        {
            if (!CanBuy(world, index)) return false;
            if (!world.Resources.TrySpend(GameData.Resources[0].Id, BigNumber.From(CostOf(index)))) return false;
            Levels[index]++;
            world.Raise(new SimEvent(SimEventKind.UpgradeBought, GameData.Units[index].Id, Levels[index]));
            return true;
        }

        /// <summary>팀 전체의 공격력 합과 체력 합. 전투는 이 두 값으로 판정한다.</summary>
        public void TeamPower(out double attack, out double hp)
        {
            attack = 0; hp = 0;
            for (int i = 0; i < Levels.Length; i++)
            {
                if (Levels[i] <= 0) continue;
                attack += AttackOf(i) * GameData.Units[i].AttackSpeed;
                hp += HpOf(i);
            }
        }
    }
}
""";

    const string Battle = """
using System;

namespace __NS__
{
    public enum BattleState { Fighting, Won, Lost }

    /// <summary>
    /// 스테이지 자동 전투. 양쪽을 "총 DPS vs 총 체력" 으로 압축해 푼다.
    /// 개별 유닛 위치·타겟팅까지 굴리지 않는 이유는, 방치형 RPG 의 밸런스가
    /// 사실상 이 두 값의 비로 결정되기 때문이다. 시뮬이 수만 판을 돌 수 있는 것도 이 덕분이다.
    /// </summary>
    public sealed class BattleSystem : ISimSystem
    {
        Roster _roster;

        public int Stage { get; private set; } = 1;
        public int BestStage { get; private set; } = 1;
        public BattleState State { get; private set; } = BattleState.Fighting;

        public double TeamHp { get; private set; }
        public double EnemyHp { get; private set; }
        public double BattleSeconds { get; private set; }
        public int Wins { get; private set; }
        public int Losses { get; private set; }

        double _teamDps, _enemyDps, _teamHpMax, _enemyHpMax;

        public void Init(SimWorld world)
        {
            _roster = world.Get<Roster>();
            StartStage(world);
        }

        public void StartStage(SimWorld world)
        {
            _roster.TeamPower(out double attack, out double hp);
            _teamDps = attack;
            _teamHpMax = hp;
            TeamHp = hp;

            _enemyHpMax = GameData.EnemyHpAt(Stage) * GameData.EnemyCount;
            EnemyHp = _enemyHpMax;
            _enemyDps = GameData.EnemyAttackAt(Stage) * GameData.EnemyAttackSpeed * GameData.EnemyCount;

            BattleSeconds = 0;
            State = BattleState.Fighting;
            world.Raise(new SimEvent(SimEventKind.WaveStarted, "", Stage));
        }

        public void Tick(SimWorld world, Fx dt)
        {
            double seconds = dt.ToDouble();

            if (GameData.IdleRewardPerSec > 0)
                world.Resources.Add(GameData.Resources[0].Id,
                    BigNumber.From(GameData.IdleRewardPerSec * BestStage * seconds));

            if (State != BattleState.Fighting) return;

            BattleSeconds += seconds;

            // 팀에 유닛이 하나도 없으면 전투가 성립하지 않는다. 영입할 때까지 기다린다.
            if (_teamDps <= 0) { if (_roster.OwnedCount > 0) StartStage(world); return; }

            EnemyHp -= _teamDps * seconds;
            TeamHp -= _enemyDps * seconds;

            if (EnemyHp <= 0) { Win(world); return; }
            if (TeamHp <= 0 || BattleSeconds >= GameData.BattleTimeout) Lose(world);
        }

        void Win(SimWorld world)
        {
            Wins++;
            State = BattleState.Won;
            world.Resources.Add(GameData.Resources[0].Id, BigNumber.From(GameData.RewardAt(Stage)));
            world.Raise(new SimEvent(SimEventKind.WaveCleared, "", Stage));

            if (Stage >= GameData.StageCount)
            {
                world.Raise(new SimEvent(SimEventKind.Victory, "", Stage));
                world.End();
                return;
            }

            Stage++;
            if (Stage > BestStage) BestStage = Stage;
            StartStage(world);
        }

        /// <summary>
        /// 패배해도 게임이 끝나지는 않는다. 같은 스테이지를 다시 시도하며,
        /// 그동안 번 골드로 유닛을 올려 뚫는 것이 이 장르의 진행 방식이다.
        /// </summary>
        void Lose(SimWorld world)
        {
            Losses++;
            State = BattleState.Lost;
            // 재도전 보상: 실패해도 최소한의 수입은 준다(완전 정체 방지)
            world.Resources.Add(GameData.Resources[0].Id, BigNumber.From(GameData.RewardAt(Stage) * 0.25));
            world.Raise(new SimEvent(SimEventKind.GameOver, "", Stage));
            StartStage(world);
        }

        public double TeamHpRatio => _teamHpMax <= 0 ? 0 : TeamHp / _teamHpMax;
        public double EnemyHpRatio => _enemyHpMax <= 0 ? 0 : EnemyHp / _enemyHpMax;
    }
}
""";

    const string AutoPlayer = """
namespace __NS__
{
    /// <summary>
    /// 밸런스 시뮬용 가상 플레이어.
    /// 팀이 안 찼으면 영입을, 찼으면 "가장 싸게 올릴 수 있는 유닛"을 올린다.
    /// </summary>
    public sealed class AutoPlayer
    {
        readonly SimWorld _world;
        readonly Roster _roster;
        readonly int[] _preference;

        /// <param name="preference">영입 우선순위. null 이면 싼 유닛부터.</param>
        public AutoPlayer(SimWorld world, int[] preference = null)
        {
            _world = world;
            _roster = world.Get<Roster>();
            _preference = preference ?? DefaultPreference();
        }

        static int[] DefaultPreference()
        {
            var idx = new int[GameData.Units.Length];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            System.Array.Sort(idx, (a, b) => GameData.Units[a].Cost.CompareTo(GameData.Units[b].Cost));
            return idx;
        }

        public void Play()
        {
            bool acted = true;
            while (acted)
            {
                acted = false;

                if (_roster.OwnedCount < GameData.TeamSize)
                {
                    for (int p = 0; p < _preference.Length; p++)
                    {
                        int i = _preference[p];
                        if (_roster.Owns(i) || !_roster.CanBuy(_world, i)) continue;
                        acted = _roster.TryBuy(_world, i);
                        if (acted) break;
                    }
                    if (acted) continue;
                }

                int best = -1;
                double bestCost = double.MaxValue;
                for (int i = 0; i < _roster.Count; i++)
                {
                    if (!_roster.Owns(i) || !_roster.CanBuy(_world, i)) continue;
                    double cost = _roster.CostOf(i);
                    if (cost < bestCost) { bestCost = cost; best = i; }
                }
                if (best >= 0) acted = _roster.TryBuy(_world, best);
            }
        }
    }
}
""";

    const string GameUi = """
namespace __NS__
{
    /// <summary>오토배틀러 화면: 전투 상황 + 유닛 영입/강화 목록.</summary>
    public sealed class GameUi : IUiProvider
    {
        readonly SimWorld _world;
        readonly Roster _roster;
        readonly BattleSystem _battle;

        public GameUi(SimWorld world)
        {
            _world = world;
            _roster = world.Get<Roster>();
            _battle = world.Get<BattleSystem>();
        }

        public void BuildUi(UiModel ui)
        {
            ui.Title = GameData.DisplayName;
            ui.ActionHeader = $"유닛 (팀 {_roster.OwnedCount}/{GameData.TeamSize})";

            var main = GameData.Resources[0];
            ui.Stats.Add(main.Name + "  " + _world.Resources.Get(main.Id));
            ui.Stats.Add($"스테이지 {_battle.Stage}/{GameData.StageCount}   최고 {_battle.BestStage}");
            ui.Stats.Add($"아군 {_battle.TeamHpRatio * 100:0}%   적 {_battle.EnemyHpRatio * 100:0}%   " +
                         $"{_battle.Wins}승 {_battle.Losses}패");

            if (_roster.OwnedCount == 0) ui.Banner = "유닛을 영입하세요";

            for (int i = 0; i < _roster.Count; i++)
            {
                int index = i;
                var def = GameData.Units[i];
                bool owned = _roster.Owns(i);
                bool maxed = _roster.IsMaxed(i);

                ui.Actions.Add(new UiAction
                {
                    Label = owned ? $"{def.Name}  Lv.{_roster.Levels[i]}" : $"{def.Name}  (영입)",
                    Sub = maxed
                        ? "MAX"
                        : $"비용 {_roster.CostOf(i):0}   공격 {_roster.AttackOf(i):0}   체력 {_roster.HpOf(i):0}",
                    Icon = def.Icon,
                    PaletteIndex = i,
                    Selected = owned,
                    Enabled = _roster.CanBuy(_world, i),
                    Execute = () => _roster.TryBuy(_world, index),
                });
            }
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
            world.Add(new Roster());
            world.Add(new BattleSystem());
            world.Init();
            return world;
        }

        public static IUiProvider CreateUi(SimWorld world) => new GameUi(world);
    }
}
""";
}
