# GameForge

기획서(JSON) 하나에서 유니티 게임 프로젝트를 통째로 생성하는 도구.
코드·데이터·씬·UI 배선·밸런스 시뮬레이터까지 자동으로 나온다.

## 왜 이렇게 만들었나

두 가지 결정이 이 도구의 전부다.

**1. Core(순수 C#) / Unity(뷰) 분리**
게임 로직에 `UnityEngine` 참조가 하나도 없다. 그래서 유니티를 켜지 않고
`dotnet`만으로 컴파일·실행·검증이 된다. 밸런스 시뮬레이터가 같은 코드를
수만 번 돌릴 수 있는 것도 이 덕분이다.

**2. UI를 런타임에 코드로 생성**
프리팹을 만들거나 인스펙터에 드래그로 참조를 꽂는 작업이 없다.
씬에 `GameRunner` 컴포넌트 하나만 있으면 게임이 성립한다.

## 사용 흐름

```bash
# 0) 도구 빌드 (최초 1회)
dotnet build Tools/GameForge/GameForge.csproj

# 1) 기획서 만들기
dotnet Tools/GameForge/bin/Debug/net9.0/gameforge.dll \
    scaffold --genre idle --name ForgeTycoon --display "대장간 타이쿤"
#   -> Specs/forgetycoon.json

# 2) 코드 생성
dotnet .../gameforge.dll gen Specs/forgetycoon.json
#   -> Assets/Games/ForgeTycoon/{Core,Unity,Editor}/
#   -> Sim/ForgeTycoon/

# 3) 밸런스 진단
dotnet run --project Sim/ForgeTycoon/ForgeTycoon.Sim.csproj

# 4) 수치 자동 탐색
dotnet run --project Sim/ForgeTycoon/ForgeTycoon.Sim.csproj -- --tune

# 5) 찾은 수치를 기획서에 되먹이고 재생성
dotnet .../gameforge.dll apply Specs/forgetycoon.json --cost-growth 1.20 --tier-factor 32
dotnet .../gameforge.dll gen Specs/forgetycoon.json

# 6) 유니티에서 메뉴 > GameForge > <게임 이름> > 씬 생성 및 열기
```

3~5번이 닫힌 루프다. 사람이 수치를 손으로 고칠 필요가 없다.

## 생성물

| 경로 | 내용 |
|---|---|
| `Assets/Games/<Name>/Core/` | 순수 C# 게임 로직 + 밸런스 테이블. `UnityEngine` 참조 없음 |
| `Assets/Games/<Name>/Unity/` | `GameRunner`(고정 스텝 루프), `Hud`(런타임 UI), 플레이스홀더 아트 |
| `Assets/Games/<Name>/Editor/` | 씬 생성 메뉴, 리소스 점검 |
| `Sim/<Name>/` | 밸런스 시뮬레이터. Core의 `.cs`를 **사본이 아니라 링크로** 참조 |
| `Specs/<name>.json` | 단일 진실원본. 이것만 고치면 위 전부가 다시 나온다 |

생성 파일은 전부 덮어쓰므로 **직접 수정하지 말 것**. 스펙을 고치고 재생성한다.

## 지원 장르

### idle — 방치형 / 타이쿤
생산 시설 + 업그레이드 + 오프라인 보상.
시뮬 축: `costGrowth`(같은 시설 반복 구매 단가), `tierFactor`(티어 간 기본가 배율).

> 실측 교훈: `costGrowth`만으로는 진행 속도가 거의 안 변한다(1.04→1.46이 3.5분→7분).
> 실제 지렛대는 티어 간 기본가 배율이다.

### towerdefense — 싱글 타워디펜스
웨이브 + 타워 배치 + 타워 강화.
시뮬 축: `hpGrowth`(웨이브당 적 체력), `rewardGrowth`(웨이브당 처치 보상).

> Core는 경로를 **1차원 거리**로 다룬다. 밸런스 판정에는 그것으로 충분하고
> 시뮬이 수만 배 빨라진다. 화면에 그릴 때만 Unity가 그 거리를 실제 경로 위 좌표로 옮긴다.

## 결정론

전투 수치는 `Fx`(Q16.16 고정소수점), 난수는 `DetRandom`(xorshift, 시드 고정).
같은 시드 + 같은 입력이면 언제 어디서 돌려도 결과가 같다.

지금은 싱글 게임에만 쓰지만, 나중에 협동 모드를 붙일 때
"입력만 주고받는" 구조로 확장할 수 있게 미리 깔아둔 것이다.
나중에 개조하려면 전투 코드를 갈아엎어야 한다.

## 아트

`icon` 필드를 비워두면 색상 플레이스홀더가 코드로 생성된다.
나중에 `Assets/Resources/`에 같은 이름의 이미지를 넣으면 자동으로 그쪽이 쓰인다.
게임을 먼저 완성하고 아트는 나중에 부으면 된다.

## 장르 추가하기

`Gen/` 아래에 `<장르>Emitter.cs`를 만들고 `Program.Generate`의 switch에 등록한다.
각 이미터는 `GameData`(밸런스 테이블), 시스템 클래스들, `AutoPlayer`(가상 플레이어),
`GameFactory`(조립)를 내보내면 된다. 나머지(런타임 공통, Unity 뷰, 시뮬 골격)는 공용이다.
