using System.Globalization;
using System.Text;
using GameForge.Spec;

namespace GameForge.Gen;

/// <summary>방치형/타이쿤 장르(S급). 생산 루프 + 업그레이드 + 오프라인 보상.</summary>
public static class IdleEmitter
{
    public static void Emit(Emitter e, GameSpec spec, string coreDir)
    {
        var ns = spec.SafeName + ".Core";
        void Add(string file, string body) => e.Add($"{coreDir}/{file}", body.Replace("__NS__", ns));

        Add("GameData.cs", BuildData(spec));
        Add("GeneratorSystem.cs", GeneratorSystem);
        Add("UpgradeSystem.cs", UpgradeSystem);
        Add("AutoPlayer.cs", AutoPlayer);
        Add("GameUi.cs", GameUi);
        Add("GameFactory.cs", Factory);
    }

    static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string S(string v) => "\"" + (v ?? "").Replace("\"", "\\\"") + "\"";

    static string BuildData(GameSpec spec)
    {
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
        sb.AppendLine("    public readonly struct GeneratorDef");
        sb.AppendLine("    {");
        sb.AppendLine("        public readonly string Id, Name, Produces, CostResource, Icon;");
        sb.AppendLine("        public readonly double BaseRate, BaseCost, CostGrowth; public readonly int UnlockAtOwned;");
        sb.AppendLine("        public GeneratorDef(string id, string name, string produces, string costResource,");
        sb.AppendLine("            double baseRate, double baseCost, double costGrowth, int unlockAtOwned, string icon)");
        sb.AppendLine("        { Id = id; Name = name; Produces = produces; CostResource = costResource;");
        sb.AppendLine("          BaseRate = baseRate; BaseCost = baseCost; CostGrowth = costGrowth;");
        sb.AppendLine("          UnlockAtOwned = unlockAtOwned; Icon = icon; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public readonly struct UpgradeDef");
        sb.AppendLine("    {");
        sb.AppendLine("        public readonly string Id, Name, Target, Stat, Mode, CostResource, Icon;");
        sb.AppendLine("        public readonly double Value, BaseCost, CostGrowth; public readonly int MaxLevel;");
        sb.AppendLine("        public UpgradeDef(string id, string name, string target, string stat, string mode,");
        sb.AppendLine("            double value, string costResource, double baseCost, double costGrowth, int maxLevel, string icon)");
        sb.AppendLine("        { Id = id; Name = name; Target = target; Stat = stat; Mode = mode; Value = value;");
        sb.AppendLine("          CostResource = costResource; BaseCost = baseCost; CostGrowth = costGrowth;");
        sb.AppendLine("          MaxLevel = maxLevel; Icon = icon; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>기획서에서 그대로 뽑아낸 밸런스 테이블.</summary>");
        sb.AppendLine("    public static class GameData");
        sb.AppendLine("    {");
        sb.AppendLine($"        public const string DisplayName = {S(spec.DisplayName)};");
        sb.AppendLine($"        public const int TickRate = {spec.TickRate};");
        sb.AppendLine();

        sb.AppendLine("        public static readonly ResourceDef[] Resources =");
        sb.AppendLine("        {");
        foreach (var r in spec.Resources)
            sb.AppendLine($"            new ResourceDef({S(r.Id)}, {S(r.Name)}, {N(r.Start)}, {(r.Premium ? "true" : "false")}, {S(r.Icon)}),");
        sb.AppendLine("        };");
        sb.AppendLine();

        sb.AppendLine("        /// <summary>readonly 가 아닌 이유: 밸런스 시뮬이 수치를 갈아끼우며 최적값을 찾는다.</summary>");
        sb.AppendLine("        public static GeneratorDef[] Generators =");
        sb.AppendLine("        {");
        foreach (var g in spec.Generators)
            sb.AppendLine($"            new GeneratorDef({S(g.Id)}, {S(g.Name)}, {S(g.Produces)}, {S(g.CostResource)}, " +
                          $"{N(g.BaseRate)}, {N(g.BaseCost)}, {N(g.CostGrowth)}, {g.UnlockAtOwned}, {S(g.Icon)}),");
        sb.AppendLine("        };");
        sb.AppendLine();

        sb.AppendLine("        public static readonly UpgradeDef[] Upgrades =");
        sb.AppendLine("        {");
        foreach (var u in spec.Upgrades)
            sb.AppendLine($"            new UpgradeDef({S(u.Id)}, {S(u.Name)}, {S(u.Target)}, {S(u.Stat)}, {S(u.Mode)}, " +
                          $"{N(u.Value)}, {S(u.CostResource)}, {N(u.BaseCost)}, {N(u.CostGrowth)}, {u.MaxLevel}, {S(u.Icon)}),");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        static readonly GeneratorDef[] OriginalGenerators = (GeneratorDef[])Generators.Clone();");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// 밸런스 튜너가 시험할 두 축을 한 번에 갈아끼운다.");
        sb.AppendLine("        ///   growth     : 같은 시설을 반복 구매할 때의 단가 상승률");
        sb.AppendLine("        ///   tierFactor : 상위 티어로 갈 때의 기본가 배율(티어 간 간격)");
        sb.AppendLine("        /// growth 만으로는 진행 속도가 거의 안 변한다 — 실제 지렛대는 tierFactor 다.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static void ApplyTuning(double growth, double tierFactor)");
        sb.AppendLine("        {");
        sb.AppendLine("            for (int i = 0; i < Generators.Length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                var d = OriginalGenerators[i];");
        sb.AppendLine("                double cost = d.BaseCost * System.Math.Pow(tierFactor, i);");
        sb.AppendLine("                Generators[i] = new GeneratorDef(d.Id, d.Name, d.Produces, d.CostResource,");
        sb.AppendLine("                    d.BaseRate, cost, growth > 0 ? growth : d.CostGrowth, d.UnlockAtOwned, d.Icon);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static void ResetTuning()");
        sb.AppendLine("            => System.Array.Copy(OriginalGenerators, Generators, Generators.Length);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    const string GeneratorSystem = """
using System;

namespace __NS__
{
    /// <summary>시설을 보유수만큼 굴려 자원을 생산한다. 구매할수록 단가가 지수로 오른다.</summary>
    public sealed class GeneratorSystem : ISimSystem
    {
        public readonly int[] Owned;
        UpgradeSystem _upgrades;

        public GeneratorSystem() { Owned = new int[GameData.Generators.Length]; }

        public int Count => Owned.Length;
        public int TotalOwned { get { int n = 0; for (int i = 0; i < Owned.Length; i++) n += Owned[i]; return n; } }
        public int UnlockedCount { get { int n = 0; for (int i = 0; i < Owned.Length; i++) if (IsUnlocked(i)) n++; return n; } }

        public void Init(SimWorld world) { _upgrades = world.Get<UpgradeSystem>(); }

        public void Tick(SimWorld world, Fx dt) => Produce(world, dt.ToDouble());

        /// <summary>
        /// 구매 사이에는 산출이 일정하므로 틱을 잘게 돌지 않고 한 번에 더해도 결과가 같다.
        /// 밸런스 시뮬이 30일치를 순식간에 도는 근거이자, 오프라인 보상 계산 경로이기도 하다.
        /// </summary>
        public void Produce(SimWorld world, double seconds)
        {
            for (int i = 0; i < Owned.Length; i++)
            {
                if (Owned[i] <= 0) continue;
                world.Resources.Add(GameData.Generators[i].Produces, RateOf(i) * seconds);
            }
        }

        /// <summary>해당 시설의 현재 초당 산출(업그레이드 반영).</summary>
        public BigNumber RateOf(int index)
        {
            var def = GameData.Generators[index];
            double mul = _upgrades == null ? 1 : _upgrades.Multiplier(def.Id, "rate");
            return BigNumber.From(def.BaseRate * Owned[index] * mul);
        }

        public BigNumber TotalRate(string resourceId)
        {
            var sum = BigNumber.Zero;
            for (int i = 0; i < Owned.Length; i++)
                if (GameData.Generators[i].Produces == resourceId) sum += RateOf(i);
            return sum;
        }

        /// <summary>다음 1개를 살 때의 가격. 보유수에 대해 지수 증가.</summary>
        public BigNumber CostOf(int index)
        {
            var def = GameData.Generators[index];
            return BigNumber.From(def.BaseCost) * BigNumber.Pow(def.CostGrowth, Owned[index]);
        }

        /// <summary>직전 시설을 일정 수 이상 사야 열린다. 첫 시설은 항상 열려 있다.</summary>
        public bool IsUnlocked(int index)
            => index <= 0 || Owned[index - 1] >= GameData.Generators[index].UnlockAtOwned;

        public bool CanBuy(SimWorld world, int index)
            => IsUnlocked(index) && world.Resources.CanAfford(GameData.Generators[index].CostResource, CostOf(index));

        public bool TryBuy(SimWorld world, int index)
        {
            if (!IsUnlocked(index)) return false;
            var def = GameData.Generators[index];
            if (!world.Resources.TrySpend(def.CostResource, CostOf(index))) return false;
            Owned[index]++;
            world.Raise(new SimEvent(SimEventKind.GeneratorBought, def.Id, Owned[index]));
            return true;
        }
    }
}
""";

    const string UpgradeSystem = """
namespace __NS__
{
    /// <summary>레벨당 곱/합으로 스탯을 올린다. 대상이 "*" 면 전역 적용.</summary>
    public sealed class UpgradeSystem : ISimSystem
    {
        public readonly int[] Levels;

        public UpgradeSystem() { Levels = new int[GameData.Upgrades.Length]; }

        public int Count => Levels.Length;

        public void Init(SimWorld world) { }
        public void Tick(SimWorld world, Fx dt) { }

        /// <summary>대상 id 의 stat 에 적용될 최종 배율.</summary>
        public double Multiplier(string targetId, string stat)
        {
            double mul = 1, add = 0;
            for (int i = 0; i < Levels.Length; i++)
            {
                if (Levels[i] <= 0) continue;
                var def = GameData.Upgrades[i];
                if (def.Stat != stat) continue;
                if (def.Target != "*" && def.Target != targetId) continue;
                if (def.Mode == "add") add += def.Value * Levels[i];
                else for (int l = 0; l < Levels[i]; l++) mul *= def.Value;
            }
            return mul + add;
        }

        public BigNumber CostOf(int index)
        {
            var def = GameData.Upgrades[index];
            return BigNumber.From(def.BaseCost) * BigNumber.Pow(def.CostGrowth, Levels[index]);
        }

        public bool IsMaxed(int index) => Levels[index] >= GameData.Upgrades[index].MaxLevel;

        public bool CanBuy(SimWorld world, int index)
            => !IsMaxed(index) && world.Resources.CanAfford(GameData.Upgrades[index].CostResource, CostOf(index));

        public bool TryBuy(SimWorld world, int index)
        {
            if (IsMaxed(index)) return false;
            var def = GameData.Upgrades[index];
            if (!world.Resources.TrySpend(def.CostResource, CostOf(index))) return false;
            Levels[index]++;
            world.Raise(new SimEvent(SimEventKind.UpgradeBought, def.Id, Levels[index]));
            return true;
        }
    }
}
""";

    const string AutoPlayer = """
namespace __NS__
{
    /// <summary>
    /// 밸런스 시뮬용 가상 플레이어. "투자 대비 회수가 가장 빠른 것"을 산다.
    /// 실제 플레이어의 합리적 행동을 근사하므로, 이 결과가 곧 진행 속도 추정치가 된다.
    /// </summary>
    public sealed class AutoPlayer
    {
        readonly SimWorld _world;
        readonly GeneratorSystem _gens;
        readonly UpgradeSystem _upgrades;

        public AutoPlayer(SimWorld world)
        {
            _world = world;
            _gens = world.Get<GeneratorSystem>();
            _upgrades = world.Get<UpgradeSystem>();
        }

        /// <summary>살 수 있는 것 중 가장 효율 좋은 것을 반복 구매한다.</summary>
        public void BuyGreedy()
        {
            bool bought = true;
            while (bought)
            {
                bought = false;
                int bestGen = -1;
                double bestScore = double.MaxValue;

                for (int i = 0; i < _gens.Count; i++)
                {
                    if (!_gens.CanBuy(_world, i)) continue;
                    var def = GameData.Generators[i];
                    double gain = def.BaseRate * (_upgrades == null ? 1 : _upgrades.Multiplier(def.Id, "rate"));
                    if (gain <= 0) continue;
                    // 회수 시간(초) = 가격 / 추가 산출. 작을수록 좋다.
                    var cost = _gens.CostOf(i);
                    double payback = cost.E > 200 ? double.MaxValue : cost.ToDouble() / gain;
                    if (payback < bestScore) { bestScore = payback; bestGen = i; }
                }

                // 업그레이드는 전체 산출을 몇 배로 올리므로 회수시간을 같은 기준으로 계산
                int bestUpg = -1;
                if (_upgrades != null)
                {
                    for (int i = 0; i < _upgrades.Count; i++)
                    {
                        if (!_upgrades.CanBuy(_world, i)) continue;
                        var def = GameData.Upgrades[i];
                        if (def.Stat != "rate") continue;
                        double factor = def.Mode == "add" ? 1 + def.Value : def.Value;
                        var current = _gens.TotalRate(def.CostResource);
                        double gain = current.ToDouble() * (factor - 1);
                        if (gain <= 0) continue;
                        var cost = _upgrades.CostOf(i);
                        double payback = cost.E > 200 ? double.MaxValue : cost.ToDouble() / gain;
                        if (payback < bestScore) { bestScore = payback; bestUpg = i; bestGen = -1; }
                    }
                }

                if (bestUpg >= 0) bought = _upgrades.TryBuy(_world, bestUpg);
                else if (bestGen >= 0) bought = _gens.TryBuy(_world, bestGen);
            }
        }
    }
}
""";

    const string GameUi = """
namespace __NS__
{
    /// <summary>방치형 화면 구성: 재화바 + 시설 목록 + 업그레이드 목록.</summary>
    public sealed class GameUi : IUiProvider
    {
        readonly SimWorld _world;
        readonly GeneratorSystem _gens;
        readonly UpgradeSystem _upgrades;

        public GameUi(SimWorld world)
        {
            _world = world;
            _gens = world.Get<GeneratorSystem>();
            _upgrades = world.Get<UpgradeSystem>();
        }

        public void BuildUi(UiModel ui)
        {
            ui.Title = GameData.DisplayName;
            ui.ActionHeader = "시설 / 업그레이드";

            foreach (var r in GameData.Resources)
                ui.Stats.Add(r.Name + "  " + _world.Resources.Get(r.Id));

            if (GameData.Resources.Length > 0)
                ui.Stats.Add("초당 +" + _gens.TotalRate(GameData.Resources[0].Id));

            for (int i = 0; i < _gens.Count; i++)
            {
                if (!_gens.IsUnlocked(i)) continue;
                int index = i;
                var def = GameData.Generators[i];
                ui.Actions.Add(new UiAction
                {
                    Label = def.Name + "  x" + _gens.Owned[i],
                    Sub = "가격 " + _gens.CostOf(i) + "   초당 +" + _gens.RateOf(i),
                    Icon = def.Icon,
                    PaletteIndex = i,
                    Enabled = _gens.CanBuy(_world, i),
                    Execute = () => _gens.TryBuy(_world, index),
                });
            }

            for (int i = 0; i < _upgrades.Count; i++)
            {
                int index = i;
                var def = GameData.Upgrades[i];
                bool maxed = _upgrades.IsMaxed(i);
                ui.Actions.Add(new UiAction
                {
                    Label = maxed ? def.Name + "  MAX" : def.Name + "  Lv." + _upgrades.Levels[i],
                    Sub = maxed ? "" : "가격 " + _upgrades.CostOf(i) + "   " + def.Stat + " x" + def.Value,
                    Icon = def.Icon,
                    PaletteIndex = 40 + i,
                    Enabled = _upgrades.CanBuy(_world, i),
                    Execute = () => _upgrades.TryBuy(_world, index),
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
            world.Add(new UpgradeSystem());
            world.Add(new GeneratorSystem());
            world.Init();
            return world;
        }

        public static IUiProvider CreateUi(SimWorld world) => new GameUi(world);
    }
}
""";
}
