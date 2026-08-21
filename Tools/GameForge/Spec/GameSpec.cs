using System.Text.Json.Serialization;

namespace GameForge.Spec;

/// <summary>
/// 기획서를 기계가 읽는 형태로 옮긴 단일 진실원본.
/// 이 파일 하나만 고치면 코드/데이터/씬이 전부 다시 생성된다.
/// </summary>
public sealed class GameSpec
{
    public string Name { get; set; } = "UntitledGame";
    public string DisplayName { get; set; } = "Untitled";
    public string Genre { get; set; } = "idle";          // idle | towerdefense | autobattler
    public string Description { get; set; } = "";
    public int TickRate { get; set; } = 20;               // 시뮬 틱/초 (결정론 고정 스텝)

    /// <summary>
    /// 화면을 어떻게 그릴지. 기본은 2D 스프라이트.
    ///   sprite2d : 판/경로를 월드에 스프라이트로 그리고, 상단 정보와 버튼만 UI 로 올린다 (2D 게임 표준)
    ///   ui       : 전부 UI 캔버스로만 그린다 (방치형처럼 원래 UI 게임인 경우)
    /// </summary>
    public string View { get; set; } = "sprite2d";

    public List<ResourceSpec> Resources { get; set; } = new();
    public List<GeneratorSpec> Generators { get; set; } = new();
    public List<UpgradeSpec> Upgrades { get; set; } = new();
    public List<TowerSpec> Towers { get; set; } = new();
    public List<EnemySpec> Enemies { get; set; } = new();
    public WaveSpec Waves { get; set; } = new();
    public TowerUpgradeSpec TowerUpgrade { get; set; } = new();
    public MergeSpec Merge { get; set; } = new();
    public List<UnitSpec> Units { get; set; } = new();
    public StageSpec Stages { get; set; } = new();
    public SurvivorSpec Survivor { get; set; } = new();
    public List<WeaponSpec> Weapons { get; set; } = new();
    public Match3Spec Match3 { get; set; } = new();
    public List<ScreenSpec> Screens { get; set; } = new();
    public BalanceTargets Balance { get; set; } = new();
    public ArtSpec Art { get; set; } = new();

    [JsonIgnore] public string SafeName => new string(Name.Where(char.IsLetterOrDigit).ToArray());
}

public sealed class ResourceSpec
{
    public string Id { get; set; } = "gold";
    public string Name { get; set; } = "Gold";
    public double Start { get; set; }
    public bool Premium { get; set; }
    public string Icon { get; set; } = "";               // 스프라이트 파일명(확장자 제외). 비면 플레이스홀더
}

/// <summary>방치형: 자원을 초당 생산하는 시설/일꾼. 구매할수록 단가가 오른다.</summary>
public sealed class GeneratorSpec
{
    public string Id { get; set; } = "gen";
    public string Name { get; set; } = "Generator";
    public string Produces { get; set; } = "gold";
    public string CostResource { get; set; } = "gold";
    public double BaseRate { get; set; } = 1;             // 개당 초당 산출
    public double BaseCost { get; set; } = 10;
    public double CostGrowth { get; set; } = 1.15;        // 구매 1회당 비용 배율
    public int UnlockAtOwned { get; set; }                // 직전 시설 보유수 조건
    public string Icon { get; set; } = "";
}

/// <summary>공용: 특정 대상(시설/타워/전역)의 수치를 곱연산/합연산으로 올린다.</summary>
public sealed class UpgradeSpec
{
    public string Id { get; set; } = "upg";
    public string Name { get; set; } = "Upgrade";
    public string Target { get; set; } = "*";             // 제너레이터/타워 id, 또는 "*" 전역
    public string Stat { get; set; } = "rate";            // rate | damage | range | firerate
    public string Mode { get; set; } = "mul";             // mul | add
    public double Value { get; set; } = 2;
    public string CostResource { get; set; } = "gold";
    public double BaseCost { get; set; } = 100;
    public double CostGrowth { get; set; } = 3;
    public int MaxLevel { get; set; } = 20;
    public string Icon { get; set; } = "";
}

public sealed class TowerSpec
{
    public string Id { get; set; } = "tower";
    public string Name { get; set; } = "Tower";
    public double Damage { get; set; } = 10;
    public double Range { get; set; } = 3;
    public double FireRate { get; set; } = 1;             // 초당 발사수
    public double SplashRadius { get; set; }
    public double SlowPercent { get; set; }
    public double SlowDuration { get; set; }
    public double Cost { get; set; } = 50;
    public string Targeting { get; set; } = "first";      // first | last | nearest | strongest | weakest
    public string Icon { get; set; } = "";
}

public sealed class EnemySpec
{
    public string Id { get; set; } = "enemy";
    public string Name { get; set; } = "Enemy";
    public double Hp { get; set; } = 100;
    public double Speed { get; set; } = 1;                // 타일/초
    public double Armor { get; set; }                     // 고정 감산
    public double Reward { get; set; } = 5;
    public int Damage { get; set; } = 1;                  // 관통 시 잃는 라이프
    public string Icon { get; set; } = "";
}

public sealed class WaveSpec
{
    public int Count { get; set; } = 50;
    public double HpGrowth { get; set; } = 1.18;          // 웨이브당 체력 배율
    public double CountGrowth { get; set; } = 1.06;
    /// <summary>웨이브당 처치 보상 배율. 체력만 오르고 보상이 고정이면 수입이 난이도를 못 따라간다.</summary>
    public double RewardGrowth { get; set; } = 1.1;
    public int BaseEnemyCount { get; set; } = 8;
    public double SpawnInterval { get; set; } = 0.6;
    public double PrepareTime { get; set; } = 5;
    public int PathLength { get; set; } = 20;             // 타일 수
    public int StartLives { get; set; } = 20;
    public double StartGold { get; set; } = 150;
    public int BossEvery { get; set; } = 10;
    public double BossHpMul { get; set; } = 8;
}

/// <summary>
/// 지은 타워를 강화하는 규칙. 이게 없으면 슬롯을 다 채운 뒤 골드가 갈 곳이 없어져
/// 후반 화력이 원천적으로 모자라게 된다(밸런스 시뮬이 클리어율 0%로 잡아낸 문제).
/// </summary>
public sealed class TowerUpgradeSpec
{
    public int MaxLevel { get; set; } = 12;
    public double CostMul { get; set; } = 1.7;      // 레벨당 강화 비용 배율
    public double DamageMul { get; set; } = 1.55;   // 레벨당 공격력 배율
}

public sealed class ScreenSpec
{
    public string Id { get; set; } = "main";
    public string Type { get; set; } = "main";            // main | shop | upgrade | collection | settings
    public string Title { get; set; } = "";
    public List<string> Elements { get; set; } = new();   // resourceBar, generatorList, upgradeList, waveHud ...
}

public sealed class BalanceTargets
{
    public double SessionMinutes { get; set; } = 5;
    public double Day1Progress { get; set; } = 8;         // 방치형: 1일차 도달 시설 수 / TD: 도달 웨이브
    public double Day7Progress { get; set; } = 25;
    public double FullUnlockDays { get; set; } = 7;       // 방치형: 마지막 시설이 열릴 때까지의 목표 일수
    public double TargetClearRate { get; set; } = 0.75;   // TD: 목표 클리어율
    public double MaxAdoptionRate { get; set; } = 0.45;   // TD: 특정 타워 채용률 상한(메타 고착 방지)
}

public sealed class ArtSpec
{
    public string Style { get; set; } = "placeholder";    // placeholder | folder
    public string PaletteSeed { get; set; } = "forge";

    /// <summary>월드 스프라이트 1유닛이 몇 픽셀인지. 임포트 설정과 맞춰야 크기가 어긋나지 않는다.</summary>
    public int PixelsPerUnit { get; set; } = 100;

    /// <summary>2D 카메라의 세로 절반 크기. 판이 잘리면 키운다.</summary>
    public double CameraSize { get; set; } = 5;
}


// ─────────────────────────────  머지  ─────────────────────────────

public sealed class MergeSpec
{
    public int BoardWidth { get; set; } = 5;
    public int BoardHeight { get; set; } = 6;
    public int MaxLevel { get; set; } = 12;
    public double SpawnEnergyCost { get; set; } = 1;
    public double EnergyMax { get; set; } = 30;
    public double EnergyRegenPerSec { get; set; } = 0.25;
    public double ItemIncomeBase { get; set; } = 0.5;    // 1레벨 아이템의 초당 산출
    public double ItemIncomeGrowth { get; set; } = 2.2;  // 레벨당 산출 배율
    public double SellValueMul { get; set; } = 60;       // 판매가 = 초당산출 x 이 값
    public double MergeBonus { get; set; } = 5;          // 합성 1회당 즉시 보상
    public string LevelNamePrefix { get; set; } = "Lv";
}

// ───────────────────────  오토배틀러 / 방치형 RPG  ───────────────────────

public sealed class UnitSpec
{
    public string Id { get; set; } = "unit";
    public string Name { get; set; } = "Unit";
    public double Hp { get; set; } = 100;
    public double Attack { get; set; } = 10;
    public double AttackSpeed { get; set; } = 1;      // 초당 공격 횟수
    public double Cost { get; set; } = 100;
    public string Role { get; set; } = "dps";         // dps | tank | support
    public string Icon { get; set; } = "";
}

public sealed class StageSpec
{
    public int Count { get; set; } = 100;
    public double HpGrowth { get; set; } = 1.12;
    public double AttackGrowth { get; set; } = 1.10;
    public int EnemyCount { get; set; } = 3;
    public double EnemyBaseHp { get; set; } = 120;
    public double EnemyBaseAttack { get; set; } = 12;
    public double EnemyAttackSpeed { get; set; } = 0.9;
    public double RewardBase { get; set; } = 40;
    public double RewardGrowth { get; set; } = 1.14;
    public int TeamSize { get; set; } = 5;
    public double UpgradeCostMul { get; set; } = 1.45;
    public double UpgradeStatMul { get; set; } = 1.28;
    public int MaxUnitLevel { get; set; } = 60;
    public double BattleTimeout { get; set; } = 60;   // 초. 넘으면 패배 처리
    public double IdleRewardPerSec { get; set; } = 0; // 0 이면 스테이지 보상만
}

// ─────────────────────────────  서바이버  ─────────────────────────────

public sealed class WeaponSpec
{
    public string Id { get; set; } = "weapon";
    public string Name { get; set; } = "Weapon";
    public double Damage { get; set; } = 10;
    public double FireRate { get; set; } = 2;         // 초당 발사
    public int Targets { get; set; } = 1;             // 한 번에 때리는 적 수
    public double LevelDamageMul { get; set; } = 1.35;
    public int MaxLevel { get; set; } = 8;
    public string Icon { get; set; } = "";
}

public sealed class SurvivorSpec
{
    public double DurationMinutes { get; set; } = 15;
    public double PlayerHp { get; set; } = 120;
    public double PlayerRegenPerSec { get; set; }
    public double ContactDamagePerEnemy { get; set; } = 1.5;  // 몰린 적 1마리가 초당 주는 피해
    public double SpawnPerSecBase { get; set; } = 1.2;
    public double SpawnPerSecGrowth { get; set; } = 1.09;     // 분당 배율
    public double EnemyBaseHp { get; set; } = 12;
    public double EnemyHpGrowth { get; set; } = 1.35;         // 분당 배율
    public double XpPerKill { get; set; } = 1;
    public double XpToLevelBase { get; set; } = 6;
    public double XpToLevelGrowth { get; set; } = 1.32;
    public int MaxAliveEnemies { get; set; } = 400;
}

// ─────────────────────────────  매치3  ─────────────────────────────

public sealed class Match3Spec
{
    public int BoardWidth { get; set; } = 8;
    public int BoardHeight { get; set; } = 8;
    public int ColorCount { get; set; } = 5;
    public int Moves { get; set; } = 25;
    public int TargetScore { get; set; } = 2000;
    public int ScorePerTile { get; set; } = 20;
    public double ComboBonus { get; set; } = 0.5;    // 연쇄 1단계당 점수 배율 증가
    public int StageCount { get; set; } = 50;
    public double TargetGrowth { get; set; } = 1.08; // 스테이지당 목표 점수 배율
}
