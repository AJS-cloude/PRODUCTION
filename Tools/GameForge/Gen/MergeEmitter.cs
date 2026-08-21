using System.Globalization;
using System.Text;
using GameForge.Spec;

namespace GameForge.Gen;

/// <summary>머지 장르(S급). 판 위에 같은 레벨을 합쳐 상위 아이템을 만들고, 아이템이 자원을 생산한다.</summary>
public static class MergeEmitter
{
    public static void Emit(Emitter e, GameSpec spec, string coreDir)
    {
        var ns = spec.SafeName + ".Core";
        void Add(string file, string body) => e.Add($"{coreDir}/{file}", body.Replace("__NS__", ns));

        Add("GameData.cs", BuildData(spec));
        Add("MergeBoard.cs", Board);
        Add("EnergySystem.cs", Energy);
        Add("AutoPlayer.cs", AutoPlayer);
        Add("GameUi.cs", GameUi);
        Add("GameFactory.cs", Factory);
    }

    static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string S(string v) => "\"" + (v ?? "").Replace("\"", "\\\"") + "\"";

    static string BuildData(GameSpec spec)
    {
        var m = spec.Merge;
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
        sb.AppendLine("    /// <summary>기획서에서 그대로 뽑아낸 밸런스 테이블.</summary>");
        sb.AppendLine("    public static class GameData");
        sb.AppendLine("    {");
        sb.AppendLine($"        public const string DisplayName = {S(spec.DisplayName)};");
        sb.AppendLine($"        public const int TickRate = {spec.TickRate};");
        sb.AppendLine($"        public const string LevelNamePrefix = {S(m.LevelNamePrefix)};");
        sb.AppendLine();
        sb.AppendLine($"        public const int BoardWidth = {m.BoardWidth};");
        sb.AppendLine($"        public const int BoardHeight = {m.BoardHeight};");
        sb.AppendLine("        public const int CellCount = BoardWidth * BoardHeight;");
        sb.AppendLine($"        public const int MaxLevel = {m.MaxLevel};");
        sb.AppendLine($"        public const double SpawnEnergyCost = {N(m.SpawnEnergyCost)};");
        sb.AppendLine($"        public const double MergeBonus = {N(m.MergeBonus)};");
        sb.AppendLine($"        public const double SellValueMul = {N(m.SellValueMul)};");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>튜너가 갈아끼우는 두 축.</summary>");
        sb.AppendLine($"        public static double EnergyRegenPerSec = {N(m.EnergyRegenPerSec)};");
        sb.AppendLine($"        public static double ItemIncomeGrowth = {N(m.ItemIncomeGrowth)};");
        sb.AppendLine($"        const double OriginalRegen = {N(m.EnergyRegenPerSec)};");
        sb.AppendLine($"        const double OriginalIncomeGrowth = {N(m.ItemIncomeGrowth)};");
        sb.AppendLine();
        sb.AppendLine("        public static void ApplyTuning(double regen, double incomeGrowth)");
        sb.AppendLine("        {");
        sb.AppendLine("            EnergyRegenPerSec = regen > 0 ? regen : OriginalRegen;");
        sb.AppendLine("            ItemIncomeGrowth = incomeGrowth > 0 ? incomeGrowth : OriginalIncomeGrowth;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static void ResetTuning()");
        sb.AppendLine("        { EnergyRegenPerSec = OriginalRegen; ItemIncomeGrowth = OriginalIncomeGrowth; }");
        sb.AppendLine();
        sb.AppendLine($"        public const double EnergyMax = {N(m.EnergyMax)};");
        sb.AppendLine($"        public const double ItemIncomeBase = {N(m.ItemIncomeBase)};");
        sb.AppendLine();
        sb.AppendLine("        public static readonly ResourceDef[] Resources =");
        sb.AppendLine("        {");
        foreach (var r in spec.Resources)
            sb.AppendLine($"            new ResourceDef({S(r.Id)}, {S(r.Name)}, {N(r.Start)}, {(r.Premium ? "true" : "false")}, {S(r.Icon)}),");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>레벨 n 아이템의 초당 산출. 레벨이 오를수록 지수로 커진다.</summary>");
        sb.AppendLine("        public static double IncomeOf(int level)");
        sb.AppendLine("            => level <= 0 ? 0 : ItemIncomeBase * System.Math.Pow(ItemIncomeGrowth, level - 1);");
        sb.AppendLine();
        sb.AppendLine("        public static double SellValueOf(int level) => IncomeOf(level) * SellValueMul;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    const string Board = """
namespace __NS__
{
    /// <summary>
    /// 머지 판. 셀 값은 아이템 레벨(0 = 빈 칸).
    /// 아이템은 놓여 있는 것만으로 자원을 생산하므로, 판이 곧 경제다.
    /// </summary>
    public sealed class MergeBoard : ISimSystem
    {
        public readonly int[] Cells = new int[GameData.CellCount];
        EnergySystem _energy;

        public int Highest { get; private set; }
        public int MergeCount { get; private set; }

        public void Init(SimWorld world) { _energy = world.Get<EnergySystem>(); }

        public void Tick(SimWorld world, Fx dt) => Produce(world, dt.ToDouble());

        /// <summary>판 전체 산출을 한 번에 더한다. 시뮬이 긴 시간을 건너뛸 때 쓰는 경로이기도 하다.</summary>
        public void Produce(SimWorld world, double seconds)
        {
            double rate = TotalIncome();
            if (rate > 0) world.Resources.Add(GameData.Resources[0].Id, BigNumber.From(rate * seconds));
        }

        public double TotalIncome()
        {
            double sum = 0;
            for (int i = 0; i < Cells.Length; i++) sum += GameData.IncomeOf(Cells[i]);
            return sum;
        }

        public int FirstEmpty()
        {
            for (int i = 0; i < Cells.Length; i++) if (Cells[i] == 0) return i;
            return -1;
        }

        public bool IsFull => FirstEmpty() < 0;

        public bool CanSpawn() => !IsFull && _energy != null && _energy.Has(GameData.SpawnEnergyCost);

        /// <summary>가장 낮은 레벨의 아이템을 빈 칸에 하나 놓는다.</summary>
        public bool TrySpawn(SimWorld world)
        {
            int slot = FirstEmpty();
            if (slot < 0) return false;
            if (!_energy.TrySpend(GameData.SpawnEnergyCost)) return false;

            Cells[slot] = 1;
            if (Highest < 1) Highest = 1;
            world.Raise(new SimEvent(SimEventKind.GeneratorBought, "spawn", slot));
            return true;
        }

        public bool CanMerge(int a, int b)
            => a != b
               && InRange(a) && InRange(b)
               && Cells[a] > 0 && Cells[a] == Cells[b]
               && Cells[a] < GameData.MaxLevel;

        /// <summary>a 를 b 자리에 합친다. 결과는 b 에 남는다.</summary>
        public bool TryMerge(SimWorld world, int a, int b)
        {
            if (!CanMerge(a, b)) return false;

            Cells[b] = Cells[a] + 1;
            Cells[a] = 0;
            MergeCount++;
            if (Cells[b] > Highest) Highest = Cells[b];

            world.Resources.Add(GameData.Resources[0].Id, BigNumber.From(GameData.MergeBonus));
            world.Raise(new SimEvent(SimEventKind.UpgradeBought, "merge", Cells[b]));
            return true;
        }

        public bool TrySell(SimWorld world, int index)
        {
            if (!InRange(index) || Cells[index] <= 0) return false;
            world.Resources.Add(GameData.Resources[0].Id, BigNumber.From(GameData.SellValueOf(Cells[index])));
            Cells[index] = 0;
            return true;
        }

        static bool InRange(int i) => i >= 0 && i < GameData.CellCount;
    }
}
""";

    const string Energy = """
using System;

namespace __NS__
{
    /// <summary>소환에 쓰는 에너지. 차오르는 속도가 곧 진행 속도의 상한이다.</summary>
    public sealed class EnergySystem : ISimSystem
    {
        public double Current { get; private set; }
        public double Max => GameData.EnergyMax;

        public void Init(SimWorld world) { Current = GameData.EnergyMax; }

        public void Tick(SimWorld world, Fx dt) => Regen(dt.ToDouble());

        public void Regen(double seconds)
            => Current = Math.Min(GameData.EnergyMax, Current + GameData.EnergyRegenPerSec * seconds);

        public bool Has(double amount) => Current >= amount;

        public bool TrySpend(double amount)
        {
            if (Current < amount) return false;
            Current -= amount;
            return true;
        }
    }
}
""";

    const string AutoPlayer = """
namespace __NS__
{
    /// <summary>
    /// 밸런스 시뮬용 가상 플레이어.
    /// 합칠 수 있으면 무조건 합치고(낮은 레벨부터), 자리가 남으면 소환한다.
    /// 판이 꽉 차면 가장 낮은 아이템을 팔아 숨통을 틔운다 — 실제 플레이어의 기본 동작이다.
    /// </summary>
    public sealed class AutoPlayer
    {
        readonly SimWorld _world;
        readonly MergeBoard _board;

        public AutoPlayer(SimWorld world)
        {
            _world = world;
            _board = world.Get<MergeBoard>();
        }

        public void Play()
        {
            while (MergeOnce()) { }
            while (_board.CanSpawn()) { _board.TrySpawn(_world); while (MergeOnce()) { } }
            if (_board.IsFull) SellLowest();
        }

        /// <summary>가장 낮은 레벨의 짝을 찾아 하나 합친다.</summary>
        bool MergeOnce()
        {
            int bestA = -1, bestB = -1, bestLevel = int.MaxValue;

            for (int a = 0; a < GameData.CellCount; a++)
            {
                int level = _board.Cells[a];
                if (level <= 0 || level >= GameData.MaxLevel || level >= bestLevel) continue;

                for (int b = a + 1; b < GameData.CellCount; b++)
                {
                    if (_board.Cells[b] != level) continue;
                    bestA = a; bestB = b; bestLevel = level;
                    break;
                }
            }

            return bestA >= 0 && _board.TryMerge(_world, bestA, bestB);
        }

        void SellLowest()
        {
            int target = -1, lowest = int.MaxValue;
            for (int i = 0; i < GameData.CellCount; i++)
            {
                int level = _board.Cells[i];
                if (level > 0 && level < lowest) { lowest = level; target = i; }
            }
            if (target >= 0) _board.TrySell(_world, target);
        }
    }
}
""";

    const string GameUi = """
namespace __NS__
{
    /// <summary>머지 화면: 판 + 소환/판매 버튼. 셀을 두 번 눌러 합친다.</summary>
    public sealed class GameUi : IUiProvider
    {
        readonly SimWorld _world;
        readonly MergeBoard _board;
        readonly EnergySystem _energy;
        int _selected = -1;
        bool _sellMode;

        public GameUi(SimWorld world)
        {
            _world = world;
            _board = world.Get<MergeBoard>();
            _energy = world.Get<EnergySystem>();
        }

        public void BuildUi(UiModel ui)
        {
            ui.Title = GameData.DisplayName;
            ui.ActionHeader = _sellMode ? "판매할 아이템을 고르세요" : "같은 레벨 둘을 눌러 합치세요";

            var main = GameData.Resources[0];
            ui.Stats.Add(main.Name + "  " + _world.Resources.Get(main.Id));
            ui.Stats.Add($"초당 +{_board.TotalIncome():0.##}    에너지 {_energy.Current:0}/{_energy.Max:0}");
            ui.Stats.Add($"최고 {GameData.LevelNamePrefix}.{_board.Highest}    합성 {_board.MergeCount}회");

            var grid = new UiGrid
            {
                Width = GameData.BoardWidth,
                Height = GameData.BoardHeight,
                Cells = new int[GameData.CellCount],
                Labels = new string[GameData.CellCount],
                OnCell = TapCell,
            };

            for (int i = 0; i < GameData.CellCount; i++)
            {
                int level = _board.Cells[i];
                grid.Cells[i] = level <= 0 ? -1 : level;
                grid.Labels[i] = level <= 0 ? "" : GameData.LevelNamePrefix + "." + level;
                if (i == _selected) grid.Labels[i] = "[ " + grid.Labels[i] + " ]";
            }
            ui.Grid = grid;

            ui.Actions.Add(new UiAction
            {
                Label = "소환",
                Sub = $"에너지 {GameData.SpawnEnergyCost:0.##} 소모",
                PaletteIndex = 3,
                Enabled = _board.CanSpawn(),
                Execute = () => _board.TrySpawn(_world),
            });

            ui.Actions.Add(new UiAction
            {
                Label = _sellMode ? "판매 모드 끄기" : "판매 모드",
                Sub = "아이템을 눌러 즉시 골드로 바꿉니다",
                PaletteIndex = 9,
                Selected = _sellMode,
                Execute = () => { _sellMode = !_sellMode; _selected = -1; },
            });
        }

        void TapCell(int index)
        {
            if (_sellMode) { _board.TrySell(_world, index); return; }

            if (_selected < 0) { _selected = _board.Cells[index] > 0 ? index : -1; return; }
            if (_selected == index) { _selected = -1; return; }

            if (_board.TryMerge(_world, _selected, index)) _selected = -1;
            else _selected = _board.Cells[index] > 0 ? index : -1;
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
            world.Add(new EnergySystem());
            world.Add(new MergeBoard());
            world.Init();
            return world;
        }

        public static IUiProvider CreateUi(SimWorld world) => new GameUi(world);
    }
}
""";
}
