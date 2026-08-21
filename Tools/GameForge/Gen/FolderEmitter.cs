using GameForge.Spec;

namespace GameForge.Gen;

/// <summary>
/// 리소스를 넣을 폴더를 만들고, 무엇을 어디에 넣는지 안내 문서를 붙인다.
/// 개발을 시작할 때 사람이 폴더를 파거나 규칙을 외울 필요가 없게 하는 것이 목적이다.
/// </summary>
public static class FolderEmitter
{
    /// <summary>
    /// Resources 아래 폴더만 코드가 이름으로 로드할 수 있다.
    /// PlaceholderArt 가 Icons -> Sprites -> Backgrounds -> 루트 순으로 뒤지므로,
    /// 기획서에는 폴더 없이 파일명만 적으면 된다.
    ///
    /// 안내 문서는 Resources **밖**에 둔다. Resources 안의 파일은 전부 빌드에 포함되기 때문이다.
    /// 빈 폴더는 git 이 추적하지 못하므로 .gitkeep 을 하나씩 넣어 둔다(유니티는 점으로 시작하는 파일을 무시한다).
    /// </summary>
    public static void Emit(Emitter e, GameSpec spec, string gameDir)
    {
        string res = $"{gameDir}/Resources";
        string[] folders = { "Icons", "Sprites", "Backgrounds", "Audio" };

        foreach (var folder in folders)
            e.Add($"{res}/{folder}/.gitkeep", "", header: false);

        e.Add($"{gameDir}/README.md", GameReadme(spec), header: false);
        e.Add($"{gameDir}/리소스-넣는-법.md", ResourceGuide(spec), header: false);
    }

    static string GameReadme(GameSpec spec)
    {
        string specFile = spec.Name.ToLowerInvariant() + ".json";
        string viewLine = spec.View == "ui"
            ? "전부 UI 캔버스로 그립니다."
            : "판과 경로는 월드에 2D 스프라이트로, 상단 정보와 하단 버튼은 UI 로 그립니다.";

        return $"""
# {spec.DisplayName}

`Specs/{specFile}` 에서 생성된 게임입니다.

## 폴더

| 폴더 | 성격 | 내용 |
|---|---|---|
| `Core/` | **자동 생성** | 게임 로직·밸런스 테이블. 직접 고치지 마세요 |
| `Unity/` | **자동 생성** | 화면 표시 코드. 직접 고치지 마세요 |
| `Editor/` | **자동 생성** | 씬 생성·점검 메뉴 |
| `Resources/` | **사람이 채움** | 이미지·사운드. 재생성해도 덮어써지지 않습니다 |

`Core` / `Unity` / `Editor` 안의 파일은 재생성 때마다 통째로 덮어써집니다.
수치를 바꾸려면 `Specs/{specFile}` 을, 동작을 바꾸려면 `Tools/GameForge/Gen/` 의 생성기를 고치세요.

## 리소스

[리소스-넣는-법.md](리소스-넣는-법.md) 참고.
요약하면 **이미지를 `Resources/Icons/` 에 넣고 기획서 `icon` 에 파일명만 적으면** 끝입니다.
비워두면 색 플레이스홀더가 자동 생성되므로 **이미지가 없어도 게임은 굴러갑니다.**

## 화면 모드

`view: "{spec.View}"` — {viewLine}

## 실행

```
메뉴 > GameForge > {spec.DisplayName} > 씬 생성 및 열기
Play
```

버튼이 안 눌리면 `메뉴 > GameForge > {spec.DisplayName} > 프로젝트 설정 점검` 을 먼저 실행하세요.
""";
    }

    static string ResourceGuide(GameSpec spec)
    {
        string worldNote = spec.View == "ui"
            ? "이 게임은 `view: \"ui\"` 라 월드 스프라이트를 쓰지 않습니다. `Sprites/` 는 비워둬도 됩니다.\n" +
              "2D 로 바꾸려면 기획서의 `view` 를 `sprite2d` 로 두고 재생성하세요."
            : "판의 각 칸, 경로 위 유닛, 배치 슬롯이 `Sprites/` 의 이미지를 씁니다.";

        return $"""
# 리소스 넣는 법 — {spec.DisplayName}

## 어디에 넣나

```
Assets/Games/{spec.SafeName}/Resources/
├─ Icons/         버튼 아이콘 (하단 목록에 뜨는 작은 그림)
├─ Sprites/       월드 스프라이트 (화면 가운데 실제로 그려지는 것)
├─ Backgrounds/   배경 이미지
└─ Audio/         사운드
```

`Resources` 폴더 안에 있어야 코드가 이름으로 찾을 수 있습니다. 다른 곳에 두면 안 잡힙니다.

## 어떻게 연결되나

**폴더 경로도, 확장자도 적지 않습니다.** 파일명만 적으면 코드가
`Icons/` → `Sprites/` → `Backgrounds/` → 루트 순으로 알아서 찾습니다.

예를 들어 `gold.png` 를 `Resources/Icons/` 에 넣었다면 기획서에는 이렇게 적습니다.

```json
{JsonSample}
```

그리고 재생성하면 끝입니다.

```bash
gameforge gen Specs/{spec.Name.ToLowerInvariant()}.json
```

빠진 이미지는 `메뉴 > GameForge > {spec.DisplayName} > 리소스 점검` 으로 한 번에 확인할 수 있습니다.
빠져 있어도 플레이스홀더로 계속 굴러가므로 **아트 없이 게임을 먼저 완성**할 수 있습니다.

---

## Icons/ — 버튼 아이콘

{IconTargets(spec)}

| 항목 | 값 |
|---|---|
| 권장 크기 | 128 x 128 px (정사각형) |
| 형식 | PNG, 투명 배경 |
| Texture Type | **Sprite (2D and UI)** |
| 파일명 | 기획서 `icon` 값과 동일. 영문·숫자·밑줄 권장 |

---

## Sprites/ — 월드 스프라이트

{worldNote}

| 항목 | 값 |
|---|---|
| 권장 크기 | 256 x 256 px 이하 (정사각형) |
| 형식 | PNG, 투명 배경 |
| Texture Type | **Sprite (2D and UI)** |
| Pixels Per Unit | **{spec.Art.PixelsPerUnit}** — 기획서 `art.pixelsPerUnit` 과 일치시킬 것 |
| Pivot | Center |

크기가 서로 달라도 코드가 칸 크기에 맞춰 자동으로 맞춥니다.
다만 Pixels Per Unit 이 어긋나면 의도한 것보다 크거나 작게 보입니다.

**픽셀아트라면** Filter Mode = `Point (no filter)`, Compression = `None` 으로 두세요. 안 그러면 뿌옇게 보입니다.

---

## Backgrounds/ — 배경

| 항목 | 값 |
|---|---|
| 권장 크기 | 1080 x 1920 px (세로 화면) |
| 형식 | PNG 또는 JPG |
| Texture Type | Sprite (2D and UI) |

> 현재 배경은 카메라 단색으로 칠합니다. 배경 이미지를 실제로 쓰려면 기획서에 배경 항목을
> 추가해야 하므로 요청해 주세요. 지금은 폴더만 준비된 상태입니다.

---

## Audio/ — 사운드

파일명 앞에 용도를 붙이면 관리가 쉽습니다.

| 접두사 | 용도 | 예 | 권장 형식 |
|---|---|---|---|
| `bgm_` | 배경음악 | `bgm_main.ogg` | OGG, Load Type = Streaming |
| `sfx_` | 효과음 | `sfx_merge.wav` | WAV, Decompress On Load |

> **사운드 재생은 아직 생성기에 없습니다.** 폴더만 미리 만들어 둔 상태입니다.

---

## 주의

- `Resources` 폴더 안의 파일은 **전부 빌드에 포함**됩니다. 안 쓰는 이미지는 넣어두지 마세요.
- `Core/` `Unity/` `Editor/` 에는 아무것도 넣지 마세요. 재생성 때 사라집니다.
""";
    }

    /// <summary>중괄호가 들어가는 예시는 원시 문자열 보간과 충돌하므로 밖으로 뺀다.</summary>
    const string JsonSample = """{ "id": "gold", "name": "골드", "icon": "gold" }""";

    static string IconTargets(GameSpec spec) => spec.Genre switch
    {
        "idle" => "넣을 것: 자원(`resources[].icon`), 시설(`generators[].icon`), 업그레이드(`upgrades[].icon`)",
        "towerdefense" => "넣을 것: 자원(`resources[].icon`), 타워(`towers[].icon`), 적(`enemies[].icon`)",
        "autobattler" => "넣을 것: 자원(`resources[].icon`), 유닛(`units[].icon`)",
        "survivor" => "넣을 것: 자원(`resources[].icon`), 무기(`weapons[].icon`)",
        "merge" => "넣을 것: 자원(`resources[].icon`). 판 위 아이템 이미지는 `Sprites/` 에 넣습니다.",
        "match3" => "넣을 것: 자원(`resources[].icon`). 판 위 블록 이미지는 `Sprites/` 에 넣습니다.",
        _ => "넣을 것: 자원(`resources[].icon`)",
    };
}
