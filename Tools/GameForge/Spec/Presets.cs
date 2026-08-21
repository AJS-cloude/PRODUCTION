namespace GameForge.Spec;

/// <summary>
/// 장르별 기본 기획서. "요청 한 줄"을 여기 값들로 옮기면 곧바로 게임이 나온다.
/// 수치는 시작점일 뿐이고, 밸런스 시뮬이 실제 곡선을 보고 조정한다.
/// </summary>
public static class Presets
{
    public static GameSpec Idle(string name, string display) => new()
    {
        Name = name,
        DisplayName = display,
        Genre = "idle",
        View = "ui",   // 방치형은 원래 UI 게임이다
        Description = "대장간 제작 타이쿤. 시설을 늘려 초당 산출을 키우고 업그레이드로 배율을 올린다.",
        TickRate = 20,
        Resources =
        {
            new ResourceSpec { Id = "gold", Name = "골드", Start = 20 },
            new ResourceSpec { Id = "gem",  Name = "보석", Start = 0, Premium = true },
        },
        Generators =
        {
            Gen("apprentice", "견습공",   1,          10,        0),
            Gen("smith",      "대장장이", 8,          120,       1),
            Gen("furnace",    "화로",     47,         1_400,     1),
            Gen("anvil",      "마법 모루", 260,        20_000,    1),
            Gen("forge",      "용광로",   1_400,      330_000,   1),
            Gen("guild",      "장인 길드", 7_800,      5_100_000, 1),
            Gen("factory",    "제작소",   44_000,     75_000_000, 1),
            Gen("citadel",    "요새 공방", 260_000,    1_000_000_000, 1),
        },
        Upgrades =
        {
            new UpgradeSpec { Id = "sharp",   Name = "연마 기술",   Target = "*",          Stat = "rate", Mode = "mul", Value = 2, BaseCost = 1_000,   CostGrowth = 9,  MaxLevel = 12 },
            new UpgradeSpec { Id = "bellows", Name = "풀무 개량",   Target = "furnace",    Stat = "rate", Mode = "mul", Value = 3, BaseCost = 12_000,  CostGrowth = 12, MaxLevel = 8 },
            new UpgradeSpec { Id = "wage",    Name = "임금 인상",   Target = "apprentice", Stat = "rate", Mode = "mul", Value = 4, BaseCost = 500,     CostGrowth = 15, MaxLevel = 8 },
            new UpgradeSpec { Id = "master",  Name = "장인 정신",   Target = "*",          Stat = "rate", Mode = "mul", Value = 5, BaseCost = 2_000_000, CostGrowth = 20, MaxLevel = 6 },
        },
        Screens =
        {
            new ScreenSpec { Id = "main", Type = "main", Title = "공방", Elements = { "resourceBar", "generatorList", "upgradeList" } },
        },
        Balance = new BalanceTargets { SessionMinutes = 5, Day1Progress = 6, Day7Progress = 8 },
    };

    static GeneratorSpec Gen(string id, string name, double rate, double cost, int unlockAt) => new()
    {
        Id = id,
        Name = name,
        Produces = "gold",
        CostResource = "gold",
        BaseRate = rate,
        BaseCost = cost,
        CostGrowth = 1.07,      // 방치형 표준 곡선. 너무 높으면 후반이 벽이 된다.
        UnlockAtOwned = unlockAt,
    };

    public static GameSpec TowerDefense(string name, string display) => new()
    {
        Name = name,
        DisplayName = display,
        Genre = "towerdefense",
        Description = "경로 방어형 타워디펜스. 웨이브를 막아 골드를 벌고 타워를 늘린다.",
        TickRate = 20,
        Resources =
        {
            new ResourceSpec { Id = "gold", Name = "골드", Start = 320 },
            new ResourceSpec { Id = "gem",  Name = "보석", Start = 0, Premium = true },
        },
        Towers =
        {
            new TowerSpec { Id = "arrow",  Name = "화살탑", Damage = 8,  Range = 3,   FireRate = 1.6,  Cost = 50,  Targeting = "first" },
            new TowerSpec { Id = "cannon", Name = "대포탑", Damage = 22, Range = 2.5, FireRate = 0.6,  Cost = 120, Targeting = "first", SplashRadius = 1.2 },
            new TowerSpec { Id = "frost",  Name = "서리탑", Damage = 4,  Range = 3,   FireRate = 1.2,  Cost = 100, Targeting = "first", SlowPercent = 0.4, SlowDuration = 1.5 },
            new TowerSpec { Id = "mage",   Name = "마법탑", Damage = 30, Range = 3.5, FireRate = 0.8,  Cost = 200, Targeting = "strongest" },
            new TowerSpec { Id = "sniper", Name = "저격탑", Damage = 90, Range = 8,   FireRate = 0.35, Cost = 320, Targeting = "strongest" },
        },
        Enemies =
        {
            new EnemySpec { Id = "grunt",  Name = "졸개",   Hp = 60,  Speed = 1.0, Armor = 0, Reward = 6,  Damage = 1 },
            new EnemySpec { Id = "runner", Name = "질주병", Hp = 40,  Speed = 1.9, Armor = 0, Reward = 7,  Damage = 1 },
            new EnemySpec { Id = "brute",  Name = "중장병", Hp = 220, Speed = 0.7, Armor = 3, Reward = 20, Damage = 2 },
            new EnemySpec { Id = "flier",  Name = "비행체", Hp = 120, Speed = 1.4, Armor = 1, Reward = 14, Damage = 1 },
        },
        Waves = new WaveSpec
        {
            Count = 50,
            HpGrowth = 1.16,
            CountGrowth = 1.05,
            RewardGrowth = 1.12,
            BaseEnemyCount = 8,
            SpawnInterval = 0.6,
            PrepareTime = 6,
            PathLength = 20,
            StartLives = 20,
            StartGold = 320,
            BossEvery = 10,
            BossHpMul = 8,
        },
        Screens =
        {
            new ScreenSpec { Id = "main", Type = "main", Title = "전장", Elements = { "waveHud", "towerPicker", "slotGrid" } },
        },
        Balance = new BalanceTargets { SessionMinutes = 8, TargetClearRate = 0.75, MaxAdoptionRate = 0.45 },
    };

    public static GameSpec Merge(string name, string display) => new()
    {
        Name = name,
        DisplayName = display,
        Genre = "merge",
        Description = "같은 레벨을 합쳐 상위 아이템을 만들고, 아이템이 자원을 생산하는 머지 게임.",
        TickRate = 20,
        Resources =
        {
            new ResourceSpec { Id = "coin", Name = "코인", Start = 0 },
            new ResourceSpec { Id = "gem",  Name = "보석", Start = 0, Premium = true },
        },
        Merge = new MergeSpec
        {
            BoardWidth = 5, BoardHeight = 6, MaxLevel = 12,
            SpawnEnergyCost = 1, EnergyMax = 30, EnergyRegenPerSec = 0.25,
            ItemIncomeBase = 0.5, ItemIncomeGrowth = 2.2, SellValueMul = 60, MergeBonus = 5,
            LevelNamePrefix = "Lv",
        },
        Screens = { new ScreenSpec { Id = "main", Type = "main", Title = "작업대", Elements = { "resourceBar", "mergeBoard", "actionBar" } } },
        Balance = new BalanceTargets { SessionMinutes = 6, FullUnlockDays = 5 },
    };

    public static GameSpec AutoBattler(string name, string display) => new()
    {
        Name = name,
        DisplayName = display,
        Genre = "autobattler",
        Description = "팀을 꾸려 스테이지를 자동 전투로 돌파하고, 보상으로 유닛을 강화하는 방치형 RPG.",
        TickRate = 20,
        Resources =
        {
            new ResourceSpec { Id = "gold", Name = "골드", Start = 300 },
            new ResourceSpec { Id = "gem",  Name = "보석", Start = 0, Premium = true },
        },
        Units =
        {
            new UnitSpec { Id = "warrior", Name = "전사",   Hp = 220, Attack = 12, AttackSpeed = 1.0, Cost = 100, Role = "tank" },
            new UnitSpec { Id = "archer",  Name = "궁수",   Hp = 90,  Attack = 22, AttackSpeed = 1.3, Cost = 160, Role = "dps" },
            new UnitSpec { Id = "mage",    Name = "마법사", Hp = 80,  Attack = 34, AttackSpeed = 0.8, Cost = 240, Role = "dps" },
            new UnitSpec { Id = "priest",  Name = "사제",   Hp = 110, Attack = 8,  AttackSpeed = 1.0, Cost = 200, Role = "support" },
            new UnitSpec { Id = "knight",  Name = "기사",   Hp = 340, Attack = 16, AttackSpeed = 0.9, Cost = 320, Role = "tank" },
            new UnitSpec { Id = "assassin",Name = "암살자", Hp = 100, Attack = 40, AttackSpeed = 1.6, Cost = 480, Role = "dps" },
        },
        Stages = new StageSpec
        {
            Count = 100, HpGrowth = 1.12, AttackGrowth = 1.10,
            EnemyCount = 3, EnemyBaseHp = 120, EnemyBaseAttack = 12, EnemyAttackSpeed = 0.9,
            RewardBase = 40, RewardGrowth = 1.14, TeamSize = 5,
            UpgradeCostMul = 1.45, UpgradeStatMul = 1.28, MaxUnitLevel = 60,
            BattleTimeout = 60, IdleRewardPerSec = 0.5,
        },
        Screens = { new ScreenSpec { Id = "main", Type = "main", Title = "원정", Elements = { "battleHud", "unitList" } } },
        Balance = new BalanceTargets { SessionMinutes = 6, Day1Progress = 30, Day7Progress = 70 },
    };

    public static GameSpec Survivor(string name, string display) => new()
    {
        Name = name,
        DisplayName = display,
        Genre = "survivor",
        Description = "몰려오는 적을 자동 공격으로 버티며 레벨업 강화를 골라 성장하는 서바이버.",
        TickRate = 20,
        Resources =
        {
            new ResourceSpec { Id = "gold", Name = "골드", Start = 0 },
            new ResourceSpec { Id = "gem",  Name = "보석", Start = 0, Premium = true },
        },
        Weapons =
        {
            new WeaponSpec { Id = "blade",   Name = "회전 검", Damage = 8,  FireRate = 2.0, Targets = 2, LevelDamageMul = 1.35, MaxLevel = 8 },
            new WeaponSpec { Id = "bolt",    Name = "마법 화살", Damage = 14, FireRate = 1.2, Targets = 1, LevelDamageMul = 1.40, MaxLevel = 8 },
            new WeaponSpec { Id = "aura",    Name = "오라",     Damage = 4,  FireRate = 4.0, Targets = 4, LevelDamageMul = 1.30, MaxLevel = 8 },
            new WeaponSpec { Id = "bomb",    Name = "폭탄",     Damage = 30, FireRate = 0.5, Targets = 6, LevelDamageMul = 1.38, MaxLevel = 8 },
            new WeaponSpec { Id = "lightning", Name = "번개",   Damage = 22, FireRate = 0.9, Targets = 3, LevelDamageMul = 1.36, MaxLevel = 8 },
        },
        Survivor = new SurvivorSpec
        {
            DurationMinutes = 15, PlayerHp = 120, PlayerRegenPerSec = 0.3,
            ContactDamagePerEnemy = 1.5,
            SpawnPerSecBase = 1.2, SpawnPerSecGrowth = 1.09,
            EnemyBaseHp = 12, EnemyHpGrowth = 1.35,
            XpPerKill = 1, XpToLevelBase = 6, XpToLevelGrowth = 1.32,
            MaxAliveEnemies = 400,
        },
        Screens = { new ScreenSpec { Id = "main", Type = "main", Title = "생존", Elements = { "survivalHud", "weaponList" } } },
        Balance = new BalanceTargets { SessionMinutes = 15, TargetClearRate = 0.4 },
    };

    public static GameSpec Match3(string name, string display) => new()
    {
        Name = name,
        DisplayName = display,
        Genre = "match3",
        Description = "제한된 수 안에 목표 점수를 넘기는 매치3 퍼즐.",
        TickRate = 20,
        Resources =
        {
            new ResourceSpec { Id = "star", Name = "별", Start = 0 },
            new ResourceSpec { Id = "gem",  Name = "보석", Start = 0, Premium = true },
        },
        Match3 = new Match3Spec
        {
            BoardWidth = 8, BoardHeight = 8, ColorCount = 5,
            Moves = 25, TargetScore = 2000, ScorePerTile = 20, ComboBonus = 0.5,
            StageCount = 50, TargetGrowth = 1.08,
        },
        Screens = { new ScreenSpec { Id = "main", Type = "main", Title = "퍼즐", Elements = { "stageHud", "board" } } },
        Balance = new BalanceTargets { SessionMinutes = 4, TargetClearRate = 0.7 },
    };
}
