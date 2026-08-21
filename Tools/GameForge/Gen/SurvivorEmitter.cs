using System.Globalization;
using System.Text;
using GameForge.Spec;

namespace GameForge.Gen;

/// <summary>서바이버(뱀서라이크, A급). 자동 공격으로 몰려오는 적을 버티며 레벨업 강화를 고른다.</summary>
public static class SurvivorEmitter
{
    public static void Emit(Emitter e, GameSpec spec, string coreDir)
    {
        var ns = spec.SafeName + ".Core";
        void Add(string file, string body) => e.Add($"{coreDir}/{file}", body.Replace("__NS__", ns));

        Add("GameData.cs", BuildData(spec));
        Add("ArenaSystem.cs", Arena);
        Add("LevelSystem.cs", Level);
        Add("AutoPlayer.cs", AutoPlayer);
        Add("GameUi.cs", GameUi);
        Add("GameFactory.cs", Factory);
    }

    static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string S(string v) => "\"" + (v ?? "").Replace("\"", "\\\"") + "\"";

    static string BuildData(GameSpec spec)
    {
        var sv = spec.Survivor;
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
        sb.AppendLine("    public readonly struct WeaponDef");
        sb.AppendLine("    {");
        sb.AppendLine("        public readonly string Id, Name, Icon;");
        sb.AppendLine("        public readonly double Damage, FireRate, LevelDamageMul;");
        sb.AppendLine("        public readonly int Targets, MaxLevel;");
        sb.AppendLine("        public WeaponDef(string id, string name, double damage, double fireRate, int targets,");
        sb.AppendLine("            double levelDamageMul, int maxLevel, string icon)");
        sb.AppendLine("        { Id = id; Name = name; Damage = damage; FireRate = fireRate; Targets = targets;");
        sb.AppendLine("          LevelDamageMul = levelDamageMul; MaxLevel = maxLevel; Icon = icon; }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>기획서에서 그대로 뽑아낸 밸런스 테이블.</summary>");
        sb.AppendLine("    public static class GameData");
        sb.AppendLine("    {");
        sb.AppendLine($"        public const string DisplayName = {S(spec.DisplayName)};");
        sb.AppendLine($"        public const int TickRate = {spec.TickRate};");
        sb.AppendLine();
        sb.AppendLine($"        public const double DurationMinutes = {N(sv.DurationMinutes)};");
        sb.AppendLine($"        public const double PlayerHp = {N(sv.PlayerHp)};");
        sb.AppendLine($"        public const double PlayerRegenPerSec = {N(sv.PlayerRegenPerSec)};");
        sb.AppendLine($"        public const double ContactDamagePerEnemy = {N(sv.ContactDamagePerEnemy)};");
        sb.AppendLine($"        public const double SpawnPerSecBase = {N(sv.SpawnPerSecBase)};");
        sb.AppendLine($"        public const double EnemyBaseHp = {N(sv.EnemyBaseHp)};");
        sb.AppendLine($"        public const double XpPerKill = {N(sv.XpPerKill)};");
        sb.AppendLine($"        public const double XpToLevelBase = {N(sv.XpToLevelBase)};");
        sb.AppendLine($"        public const double XpToLevelGrowth = {N(sv.XpToLevelGrowth)};");
        sb.AppendLine($"        public const int MaxAliveEnemies = {sv.MaxAliveEnemies};");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>튜너가 갈아끼우는 두 축: 적이 세지는 속도 / 몰리는 속도.</summary>");
        sb.AppendLine($"        public static double EnemyHpGrowth = {N(sv.EnemyHpGrowth)};");
        sb.AppendLine($"        public static double SpawnPerSecGrowth = {N(sv.SpawnPerSecGrowth)};");
        sb.AppendLine($"        const double OriginalHpGrowth = {N(sv.EnemyHpGrowth)};");
        sb.AppendLine($"        const double OriginalSpawnGrowth = {N(sv.SpawnPerSecGrowth)};");
        sb.AppendLine();
        sb.AppendLine("        public static void ApplyTuning(double hpGrowth, double spawnGrowth)");
        sb.AppendLine("        {");
        sb.AppendLine("            EnemyHpGrowth = hpGrowth > 0 ? hpGrowth : OriginalHpGrowth;");
        sb.AppendLine("            SpawnPerSecGrowth = spawnGrowth > 0 ? spawnGrowth : OriginalSpawnGrowth;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static void ResetTuning()");
        sb.AppendLine("        { EnemyHpGrowth = OriginalHpGrowth; SpawnPerSecGrowth = OriginalSpawnGrowth; }");
        sb.AppendLine();
        sb.AppendLine("        public static readonly ResourceDef[] Resources =");
        sb.AppendLine("        {");
        foreach (var r in spec.Resources)
            sb.AppendLine($"            new ResourceDef({S(r.Id)}, {S(r.Name)}, {N(r.Start)}, {(r.Premium ? "true" : "false")}, {S(r.Icon)}),");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        public static readonly WeaponDef[] Weapons =");
        sb.AppendLine("        {");
        foreach (var w in spec.Weapons)
            sb.AppendLine($"            new WeaponDef({S(w.Id)}, {S(w.Name)}, {N(w.Damage)}, {N(w.FireRate)}, {w.Targets}, " +
                          $"{N(w.LevelDamageMul)}, {w.MaxLevel}, {S(w.Icon)}),");
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>경과 분에 따른 적 1마리 체력 / 초당 등장 수.</summary>");
        sb.AppendLine("        public static double EnemyHpAt(double minutes)");
        sb.AppendLine("            => EnemyBaseHp * System.Math.Pow(EnemyHpGrowth, minutes);");
        sb.AppendLine("        public static double SpawnRateAt(double minutes)");
        sb.AppendLine("            => SpawnPerSecBase * System.Math.Pow(SpawnPerSecGrowth, minutes);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    const string Arena = """
using System;

namespace __NS__
{
    /// <summary>
    /// 아레나를 "적 유입량 vs 처치량" 의 흐름으로 푼다.
    /// 개별 적의 좌표를 굴리지 않는 이유는 이 장르의 밸런스가
    /// 결국 DPS 곡선과 스폰 곡선의 교차점에서 결정되기 때문이다.
    /// 처리 못 한 적이 쌓이면 그 수만큼 플레이어가 접촉 피해를 받는다.
    /// </summary>
    public sealed class ArenaSystem : ISimSystem
    {
        LevelSystem _level;

        public double Hp { get; private set; }
        public double Alive { get; private set; }
        public double Kills { get; private set; }
        public double Minutes { get; private set; }
        public bool Survived { get; private set; }

        /// <summary>적 체력을 못 깎고 남은 몫. 소수점 처치를 다음 틱으로 이월한다.</summary>
        double _damageCarry;

        public void Init(SimWorld world)
        {
            _level = world.Get<LevelSystem>();
            Hp = GameData.PlayerHp;
        }

        public void Tick(SimWorld world, Fx dt)
        {
            if (Survived || Hp <= 0) return;

            double seconds = dt.ToDouble();
            Minutes += seconds / 60.0;

            Alive = Math.Min(GameData.MaxAliveEnemies, Alive + GameData.SpawnRateAt(Minutes) * seconds);

            // 무기 화력으로 적을 녹인다. 한 마리 체력으로 나눠 처치 수를 구한다.
            double enemyHp = Math.Max(1, GameData.EnemyHpAt(Minutes));
            _damageCarry += _level.TotalDps() * seconds;

            double killable = Math.Min(Alive, _damageCarry / enemyHp);
            if (killable > 0)
            {
                Alive -= killable;
                Kills += killable;
                _damageCarry -= killable * enemyHp;
                _level.AddXp(world, killable * GameData.XpPerKill);
            }

            // 남아 있는 적이 곧 받는 피해량
            Hp += GameData.PlayerRegenPerSec * seconds;
            Hp -= Alive * GameData.ContactDamagePerEnemy * seconds;
            Hp = Math.Min(Hp, GameData.PlayerHp);

            if (Hp <= 0)
            {
                Hp = 0;
                world.Raise(new SimEvent(SimEventKind.GameOver, "", (int)(Minutes * 60)));
                world.End();
                return;
            }

            if (Minutes >= GameData.DurationMinutes)
            {
                Survived = true;
                world.Raise(new SimEvent(SimEventKind.Victory, "", (int)(Minutes * 60)));
                world.End();
            }
        }

        public double HpRatio => GameData.PlayerHp <= 0 ? 0 : Hp / GameData.PlayerHp;
    }
}
""";

    const string Level = """
using System;

namespace __NS__
{
    /// <summary>경험치와 무기 레벨. 레벨업 때마다 강화 선택지가 하나 열린다.</summary>
    public sealed class LevelSystem : ISimSystem
    {
        public readonly int[] WeaponLevels;

        public int Level { get; private set; } = 1;
        public double Xp { get; private set; }
        public int PendingChoices { get; private set; }

        public LevelSystem()
        {
            WeaponLevels = new int[GameData.Weapons.Length];
            if (WeaponLevels.Length > 0) WeaponLevels[0] = 1;   // 시작 무기 하나는 들고 시작한다
        }

        public void Init(SimWorld world) { }
        public void Tick(SimWorld world, Fx dt) { }

        public double XpToNext => GameData.XpToLevelBase * Math.Pow(GameData.XpToLevelGrowth, Level - 1);

        public void AddXp(SimWorld world, double amount)
        {
            Xp += amount;
            while (Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                PendingChoices++;
                world.Raise(new SimEvent(SimEventKind.UpgradeBought, "levelup", Level));
            }
        }

        public double DpsOf(int index)
        {
            int level = WeaponLevels[index];
            if (level <= 0) return 0;
            var def = GameData.Weapons[index];
            return def.Damage * Math.Pow(def.LevelDamageMul, level - 1) * def.FireRate * def.Targets;
        }

        public double TotalDps()
        {
            double sum = 0;
            for (int i = 0; i < WeaponLevels.Length; i++) sum += DpsOf(i);
            return sum;
        }

        public bool CanTake(int index)
            => PendingChoices > 0 && WeaponLevels[index] < GameData.Weapons[index].MaxLevel;

        /// <summary>레벨업 보상으로 무기 하나를 얻거나 강화한다.</summary>
        public bool Take(SimWorld world, int index)
        {
            if (!CanTake(index)) return false;
            WeaponLevels[index]++;
            PendingChoices--;
            return true;
        }
    }
}
""";

    const string AutoPlayer = """
namespace __NS__
{
    /// <summary>
    /// 밸런스 시뮬용 가상 플레이어. 레벨업 보상은 "지금 DPS 가 가장 많이 오르는 것"을 고른다.
    /// 선호 무기를 지정하면 그 무기를 우선해, 무기별 성능 편차를 측정할 수 있다.
    /// </summary>
    public sealed class AutoPlayer
    {
        readonly SimWorld _world;
        readonly LevelSystem _level;
        readonly int _favorite;

        public AutoPlayer(SimWorld world, int favorite = -1)
        {
            _world = world;
            _level = world.Get<LevelSystem>();
            _favorite = favorite;
        }

        public void Play()
        {
            while (_level.PendingChoices > 0)
            {
                if (_favorite >= 0 && _level.CanTake(_favorite)) { _level.Take(_world, _favorite); continue; }

                int best = -1;
                double bestGain = -1;
                for (int i = 0; i < _level.WeaponLevels.Length; i++)
                {
                    if (!_level.CanTake(i)) continue;

                    double before = _level.DpsOf(i);
                    _level.WeaponLevels[i]++;
                    double gain = _level.DpsOf(i) - before;
                    _level.WeaponLevels[i]--;

                    if (gain > bestGain) { bestGain = gain; best = i; }
                }

                if (best < 0) break;
                _level.Take(_world, best);
            }
        }
    }
}
""";

    const string GameUi = """
namespace __NS__
{
    /// <summary>서바이버 화면: 생존 상황 + 레벨업 시 무기 선택.</summary>
    public sealed class GameUi : IUiProvider
    {
        readonly SimWorld _world;
        readonly ArenaSystem _arena;
        readonly LevelSystem _level;

        public GameUi(SimWorld world)
        {
            _world = world;
            _arena = world.Get<ArenaSystem>();
            _level = world.Get<LevelSystem>();
        }

        public void BuildUi(UiModel ui)
        {
            ui.Title = GameData.DisplayName;

            ui.Stats.Add($"{_arena.Minutes:0.0}분 / {GameData.DurationMinutes:0}분");
            ui.Stats.Add($"체력 {_arena.Hp:0}/{GameData.PlayerHp:0}    Lv.{_level.Level}");
            ui.Stats.Add($"적 {_arena.Alive:0}마리    처치 {_arena.Kills:0}    DPS {_level.TotalDps():0}");

            if (_arena.Survived) ui.Banner = "생존 성공!";
            else if (_arena.Hp <= 0) ui.Banner = "쓰러졌다";

            ui.ActionHeader = _level.PendingChoices > 0
                ? $"강화를 고르세요 ({_level.PendingChoices}회 남음)"
                : "무기";

            // 체력바를 트랙으로 대신 보여준다 — 남은 시간이 한눈에 들어온다.
            ui.Track = new UiTrack { Length = 1 };
            ui.Track.Markers.Add((System.Math.Min(1.0, _arena.Minutes / GameData.DurationMinutes), 12));

            for (int i = 0; i < GameData.Weapons.Length; i++)
            {
                int index = i;
                var def = GameData.Weapons[i];
                int level = _level.WeaponLevels[i];

                ui.Actions.Add(new UiAction
                {
                    Label = level <= 0 ? $"{def.Name}  (미보유)" : $"{def.Name}  Lv.{level}",
                    Sub = level >= def.MaxLevel ? "MAX" : $"DPS {_level.DpsOf(i):0}   대상 {def.Targets}",
                    Icon = def.Icon,
                    PaletteIndex = i,
                    Selected = level > 0,
                    Enabled = _level.CanTake(i),
                    Execute = () => _level.Take(_world, index),
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
            world.Add(new LevelSystem());
            world.Add(new ArenaSystem());
            world.Init();
            return world;
        }

        public static IUiProvider CreateUi(SimWorld world) => new GameUi(world);
    }
}
""";
}
