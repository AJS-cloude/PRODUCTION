using System.Text;

namespace GameForge.Spec;

/// <summary>
/// 기획서를 코드로 만들기 전에 걸러낸다.
/// 사람이 쓴 문서를 옮긴 스펙에는 빠진 필드나 말이 안 되는 수치가 섞이기 마련이라,
/// 컴파일 에러로 만나기 전에 여기서 잡는 편이 훨씬 싸다.
/// </summary>
public static class Validator
{
    public sealed class Report
    {
        public readonly List<string> Errors = new();
        public readonly List<string> Warnings = new();
        public bool Ok => Errors.Count == 0;
    }

    public static readonly string[] Genres =
        { "match3", "merge", "idle", "survivor", "towerdefense", "autobattler" };

    public static Report Check(GameSpec spec)
    {
        var r = new Report();

        if (string.IsNullOrWhiteSpace(spec.Name)) r.Errors.Add("name 이 비어 있습니다.");
        if (spec.SafeName.Length == 0) r.Errors.Add("name 에 영문/숫자가 하나도 없습니다. 폴더와 네임스페이스에 쓰입니다.");
        if (string.IsNullOrWhiteSpace(spec.DisplayName)) r.Warnings.Add("displayName 이 비어 있습니다. 화면 제목에 쓰입니다.");

        if (Array.IndexOf(Genres, spec.Genre) < 0)
            r.Errors.Add($"genre '{spec.Genre}' 는 지원하지 않습니다. 가능: {string.Join(", ", Genres)}");

        if (spec.View != "sprite2d" && spec.View != "ui")
            r.Errors.Add($"view '{spec.View}' 는 지원하지 않습니다. 가능: sprite2d(기본, 2D), ui");

        if (spec.Art.PixelsPerUnit <= 0)
            r.Errors.Add("art.pixelsPerUnit 은 0보다 커야 합니다.");
        if (spec.Art.CameraSize <= 0)
            r.Errors.Add("art.cameraSize 는 0보다 커야 합니다.");

        if (spec.View == "sprite2d" && spec.Genre == "idle")
            r.Warnings.Add("방치형은 원래 UI 게임이라 view 를 \"ui\" 로 두는 편이 자연스럽습니다.");

        if (spec.TickRate < 1 || spec.TickRate > 120)
            r.Errors.Add($"tickRate 는 1~120 이어야 합니다 (현재 {spec.TickRate}).");

        CheckResources(spec, r);

        switch (spec.Genre)
        {
            case "idle": CheckIdle(spec, r); break;
            case "towerdefense": CheckTowerDefense(spec, r); break;
            case "merge": CheckMerge(spec, r); break;
            case "autobattler": CheckAutoBattler(spec, r); break;
            case "survivor": CheckSurvivor(spec, r); break;
            case "match3": CheckMatch3(spec, r); break;
        }

        return r;
    }

    static void CheckResources(GameSpec spec, Report r)
    {
        if (spec.Resources.Count == 0)
        {
            r.Errors.Add("resources 가 비어 있습니다. 최소 한 종류(주 재화)는 필요합니다.");
            return;
        }

        var seen = new HashSet<string>();
        foreach (var res in spec.Resources)
        {
            if (string.IsNullOrWhiteSpace(res.Id)) r.Errors.Add("resources 에 id 가 빈 항목이 있습니다.");
            else if (!seen.Add(res.Id)) r.Errors.Add($"resources id 중복: '{res.Id}'");
            if (res.Start < 0) r.Errors.Add($"resources '{res.Id}' 의 start 가 음수입니다.");
        }

        if (spec.Genre == "towerdefense" && !seen.Contains("gold"))
            r.Errors.Add("타워디펜스는 'gold' 자원이 반드시 있어야 합니다. 타워 비용과 처치 보상이 이 id 를 씁니다.");
    }

    static void CheckIdle(GameSpec spec, Report r)
    {
        if (spec.Generators.Count == 0) { r.Errors.Add("idle 장르인데 generators 가 비어 있습니다."); return; }

        var ids = new HashSet<string>(spec.Resources.ConvertAll(x => x.Id));
        for (int i = 0; i < spec.Generators.Count; i++)
        {
            var g = spec.Generators[i];
            string at = $"generators[{i}] '{g.Id}'";

            if (g.BaseRate <= 0) r.Errors.Add($"{at}: baseRate 는 0보다 커야 합니다.");
            if (g.BaseCost <= 0) r.Errors.Add($"{at}: baseCost 는 0보다 커야 합니다.");
            if (g.CostGrowth <= 1) r.Errors.Add($"{at}: costGrowth 는 1보다 커야 합니다 (보통 1.07~1.25).");
            if (g.CostGrowth > 2) r.Warnings.Add($"{at}: costGrowth {g.CostGrowth} 는 매우 가파릅니다. 후반이 벽이 될 수 있습니다.");
            if (!ids.Contains(g.Produces)) r.Errors.Add($"{at}: produces '{g.Produces}' 가 resources 에 없습니다.");
            if (!ids.Contains(g.CostResource)) r.Errors.Add($"{at}: costResource '{g.CostResource}' 가 resources 에 없습니다.");
            if (i > 0 && g.BaseCost <= spec.Generators[i - 1].BaseCost)
                r.Warnings.Add($"{at}: 앞 시설보다 baseCost 가 크지 않습니다. 티어 순서를 확인하세요.");
        }

        foreach (var u in spec.Upgrades)
        {
            if (u.Value <= 0) r.Errors.Add($"upgrades '{u.Id}': value 는 0보다 커야 합니다.");
            if (u.MaxLevel <= 0) r.Errors.Add($"upgrades '{u.Id}': maxLevel 은 1 이상이어야 합니다.");
            if (u.Target != "*" && !spec.Generators.Exists(g => g.Id == u.Target))
                r.Warnings.Add($"upgrades '{u.Id}': target '{u.Target}' 에 해당하는 시설이 없습니다.");
        }
    }

    static void CheckTowerDefense(GameSpec spec, Report r)
    {
        if (spec.Towers.Count == 0) r.Errors.Add("towerdefense 장르인데 towers 가 비어 있습니다.");
        if (spec.Enemies.Count == 0) r.Errors.Add("towerdefense 장르인데 enemies 가 비어 있습니다.");

        var w = spec.Waves;
        if (w.Count <= 0) r.Errors.Add("waves.count 는 1 이상이어야 합니다.");
        if (w.HpGrowth <= 1) r.Errors.Add("waves.hpGrowth 는 1보다 커야 합니다.");
        if (w.RewardGrowth <= 1)
            r.Warnings.Add("waves.rewardGrowth 가 1 이하입니다. 적 체력만 오르고 수입이 고정이면 후반에 반드시 막힙니다.");
        if (w.StartLives <= 0) r.Errors.Add("waves.startLives 는 1 이상이어야 합니다.");
        if (w.PathLength < 4) r.Errors.Add("waves.pathLength 는 4 이상이어야 합니다.");

        foreach (var t in spec.Towers)
        {
            if (t.Damage <= 0) r.Errors.Add($"towers '{t.Id}': damage 는 0보다 커야 합니다.");
            if (t.FireRate <= 0) r.Errors.Add($"towers '{t.Id}': fireRate 는 0보다 커야 합니다.");
            if (t.Range <= 0) r.Errors.Add($"towers '{t.Id}': range 는 0보다 커야 합니다.");
        }

        // 방어력이 초기 공격력을 압도하면 그 타워로는 사실상 딜이 안 들어간다.
        double minDamage = double.MaxValue;
        foreach (var t in spec.Towers) if (t.Damage < minDamage) minDamage = t.Damage;
        foreach (var en in spec.Enemies)
        {
            if (en.Hp <= 0) r.Errors.Add($"enemies '{en.Id}': hp 는 0보다 커야 합니다.");
            if (en.Speed <= 0) r.Errors.Add($"enemies '{en.Id}': speed 는 0보다 커야 합니다.");
            if (en.Armor >= minDamage * 0.5)
                r.Warnings.Add($"enemies '{en.Id}': armor {en.Armor} 가 최저 타워 공격력 {minDamage} 의 절반 이상입니다. " +
                               "초반에 그 타워로는 딜이 거의 안 들어갑니다.");
        }
    }

    static void CheckMerge(GameSpec spec, Report r)
    {
        var m = spec.Merge;
        if (m.BoardWidth < 3 || m.BoardHeight < 3) r.Errors.Add("merge.board 는 최소 3x3 이어야 합니다.");
        if (m.BoardWidth * m.BoardHeight > 100) r.Warnings.Add("merge 판이 100칸을 넘습니다. 모바일 화면에서 조작이 어렵습니다.");
        if (m.MaxLevel < 3) r.Errors.Add("merge.maxLevel 은 3 이상이어야 합니다.");
        if (m.ItemIncomeGrowth <= 1) r.Errors.Add("merge.itemIncomeGrowth 는 1보다 커야 합니다.");
        if (m.EnergyRegenPerSec <= 0) r.Errors.Add("merge.energyRegenPerSec 는 0보다 커야 합니다.");
        if (m.EnergyMax < m.SpawnEnergyCost) r.Errors.Add("merge.energyMax 가 소환 비용보다 작습니다. 소환이 불가능합니다.");
    }

    static void CheckAutoBattler(GameSpec spec, Report r)
    {
        if (spec.Units.Count == 0) { r.Errors.Add("autobattler 장르인데 units 가 비어 있습니다."); return; }

        var st = spec.Stages;
        if (st.Count <= 0) r.Errors.Add("stages.count 는 1 이상이어야 합니다.");
        if (st.TeamSize <= 0) r.Errors.Add("stages.teamSize 는 1 이상이어야 합니다.");
        if (st.TeamSize > spec.Units.Count)
            r.Warnings.Add($"stages.teamSize({st.TeamSize}) 가 유닛 종류({spec.Units.Count})보다 많습니다. 팀을 다 못 채웁니다.");
        if (st.HpGrowth <= 1) r.Errors.Add("stages.hpGrowth 는 1보다 커야 합니다.");
        if (st.RewardGrowth <= 1)
            r.Warnings.Add("stages.rewardGrowth 가 1 이하입니다. 적만 세지고 수입이 고정이면 진행이 멈춥니다.");
        if (st.UpgradeStatMul <= 1) r.Errors.Add("stages.upgradeStatMul 은 1보다 커야 합니다.");
        if (st.UpgradeCostMul <= 1) r.Errors.Add("stages.upgradeCostMul 은 1보다 커야 합니다.");
        if (st.UpgradeStatMul >= st.UpgradeCostMul)
            r.Warnings.Add("강화 능력치 배율이 비용 배율보다 큽니다. 올릴수록 이득이 커져 밸런스가 발산합니다.");

        foreach (var u in spec.Units)
        {
            if (u.Hp <= 0) r.Errors.Add($"units '{u.Id}': hp 는 0보다 커야 합니다.");
            if (u.Attack <= 0) r.Errors.Add($"units '{u.Id}': attack 은 0보다 커야 합니다.");
            if (u.Cost <= 0) r.Errors.Add($"units '{u.Id}': cost 는 0보다 커야 합니다.");
        }
    }

    static void CheckSurvivor(GameSpec spec, Report r)
    {
        if (spec.Weapons.Count == 0) { r.Errors.Add("survivor 장르인데 weapons 가 비어 있습니다."); return; }

        var sv = spec.Survivor;
        if (sv.DurationMinutes <= 0) r.Errors.Add("survivor.durationMinutes 는 0보다 커야 합니다.");
        if (sv.PlayerHp <= 0) r.Errors.Add("survivor.playerHp 는 0보다 커야 합니다.");
        if (sv.EnemyHpGrowth <= 1) r.Errors.Add("survivor.enemyHpGrowth 는 1보다 커야 합니다.");
        if (sv.SpawnPerSecBase <= 0) r.Errors.Add("survivor.spawnPerSecBase 는 0보다 커야 합니다.");
        if (sv.MaxAliveEnemies < 10) r.Warnings.Add("survivor.maxAliveEnemies 가 너무 작아 압박이 생기지 않습니다.");

        foreach (var w in spec.Weapons)
        {
            if (w.Damage <= 0) r.Errors.Add($"weapons '{w.Id}': damage 는 0보다 커야 합니다.");
            if (w.FireRate <= 0) r.Errors.Add($"weapons '{w.Id}': fireRate 는 0보다 커야 합니다.");
            if (w.Targets <= 0) r.Errors.Add($"weapons '{w.Id}': targets 는 1 이상이어야 합니다.");
            if (w.LevelDamageMul <= 1) r.Warnings.Add($"weapons '{w.Id}': levelDamageMul 이 1 이하라 강화해도 세지지 않습니다.");
        }
    }

    static void CheckMatch3(GameSpec spec, Report r)
    {
        var m = spec.Match3;
        if (m.BoardWidth < 5 || m.BoardHeight < 5) r.Errors.Add("match3 판은 최소 5x5 여야 매치가 성립합니다.");
        if (m.ColorCount < 3) r.Errors.Add("match3.colorCount 는 3 이상이어야 합니다.");
        if (m.ColorCount > 8) r.Warnings.Add("match3.colorCount 가 8을 넘으면 매치가 거의 안 나옵니다.");
        if (m.Moves <= 0) r.Errors.Add("match3.moves 는 1 이상이어야 합니다.");
        if (m.TargetScore <= 0) r.Errors.Add("match3.targetScore 는 1 이상이어야 합니다.");
        if (m.StageCount <= 0) r.Errors.Add("match3.stageCount 는 1 이상이어야 합니다.");
        if (m.TargetGrowth <= 1) r.Warnings.Add("match3.targetGrowth 가 1 이하면 난이도가 오르지 않습니다.");

        // 한 수로 3칸을 지운다고 볼 때 이론상 최대 점수와 목표를 비교한다.
        double optimistic = m.Moves * 3.0 * m.ScorePerTile * (1 + m.ComboBonus);
        if (m.TargetScore > optimistic)
            r.Errors.Add($"match3: 목표 점수 {m.TargetScore} 가 {m.Moves}수로 낼 수 있는 낙관적 상한 {optimistic:0} 을 넘습니다. " +
                         "1스테이지부터 클리어가 불가능합니다.");
    }

    /// <summary>사람이 읽는 필드 안내. 기획서를 스펙으로 옮길 때 참고한다.</summary>
    public static string Schema()
    {
        var sb = new StringBuilder();
        sb.AppendLine("GameForge 기획서(spec) 필드 안내");
        sb.AppendLine();
        sb.AppendLine("[공통]");
        sb.AppendLine("  name          영문 식별자. 폴더/네임스페이스에 쓰인다 (예: MyPuzzle)");
        sb.AppendLine("  displayName   화면에 나올 이름 (한글 가능)");
        sb.AppendLine("  genre         match3 | merge | idle | survivor | towerdefense | autobattler");
        sb.AppendLine("  description   한 줄 설명");
        sb.AppendLine("  tickRate      시뮬 초당 틱 수 (기본 20)");
        sb.AppendLine("  view          sprite2d(기본, 2D 스프라이트) | ui(전부 UI 캔버스)");
        sb.AppendLine("  art           pixelsPerUnit(기본 100), cameraSize(기본 5), paletteSeed");
        sb.AppendLine("  resources[]   id / name / start / premium / icon");
        sb.AppendLine("  balance       sessionMinutes, day1Progress, day7Progress,");
        sb.AppendLine("                fullUnlockDays, targetClearRate, maxAdoptionRate");
        sb.AppendLine();
        sb.AppendLine("[genre = idle]  방치형/타이쿤");
        sb.AppendLine("  generators[]  id name produces costResource baseRate baseCost costGrowth unlockAtOwned");
        sb.AppendLine("  upgrades[]    id name target(*=전역) stat mode(mul|add) value baseCost costGrowth maxLevel");
        sb.AppendLine();
        sb.AppendLine("[genre = towerdefense]  타워디펜스   * resources 에 'gold' 필수");
        sb.AppendLine("  towers[]      id name damage range fireRate splashRadius slowPercent slowDuration cost targeting");
        sb.AppendLine("                targeting: first | last | nearest | strongest | weakest");
        sb.AppendLine("  enemies[]     id name hp speed armor reward damage");
        sb.AppendLine("  waves         count hpGrowth countGrowth rewardGrowth baseEnemyCount spawnInterval");
        sb.AppendLine("                prepareTime pathLength startLives startGold bossEvery bossHpMul");
        sb.AppendLine("  towerUpgrade  maxLevel costMul damageMul");
        sb.AppendLine();
        sb.AppendLine("[genre = merge]  머지");
        sb.AppendLine("  merge         boardWidth boardHeight maxLevel spawnEnergyCost energyMax energyRegenPerSec");
        sb.AppendLine("                itemIncomeBase itemIncomeGrowth sellValueMul mergeBonus levelNamePrefix");
        sb.AppendLine();
        sb.AppendLine("[genre = autobattler]  오토배틀러/방치형 RPG");
        sb.AppendLine("  units[]       id name hp attack attackSpeed cost role(dps|tank|support)");
        sb.AppendLine("  stages        count hpGrowth attackGrowth enemyCount enemyBaseHp enemyBaseAttack");
        sb.AppendLine("                enemyAttackSpeed rewardBase rewardGrowth teamSize upgradeCostMul");
        sb.AppendLine("                upgradeStatMul maxUnitLevel battleTimeout idleRewardPerSec");
        sb.AppendLine();
        sb.AppendLine("[genre = survivor]  서바이버");
        sb.AppendLine("  weapons[]     id name damage fireRate targets levelDamageMul maxLevel");
        sb.AppendLine("  survivor      durationMinutes playerHp playerRegenPerSec contactDamagePerEnemy");
        sb.AppendLine("                spawnPerSecBase spawnPerSecGrowth enemyBaseHp enemyHpGrowth");
        sb.AppendLine("                xpPerKill xpToLevelBase xpToLevelGrowth maxAliveEnemies");
        sb.AppendLine();
        sb.AppendLine("[genre = match3]  매치3");
        sb.AppendLine("  match3        boardWidth boardHeight colorCount moves targetScore scorePerTile");
        sb.AppendLine("                comboBonus stageCount targetGrowth");
        sb.AppendLine();
        sb.AppendLine("icon 은 Assets/Resources/ 안의 파일명(확장자 제외). 비우면 색 플레이스홀더가 자동 생성된다.");
        return sb.ToString();
    }
}
