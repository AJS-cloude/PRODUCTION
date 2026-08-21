using GameForge.Spec;

namespace GameForge.Gen;

/// <summary>
/// 씬 생성과 리소스 점검을 에디터 스크립트로 자동화한다.
/// 사용자가 유니티에서 할 일은 Play 버튼을 누르는 것뿐이다.
/// </summary>
public static class EditorEmitter
{
    public static void Emit(Emitter e, GameSpec spec, string editorDir, string titleScenePath, string gameScenePath,
        string specPath)
    {
        var body = SceneBuilder
            .Replace("__NS__", spec.SafeName + ".Core")
            .Replace("__GAME__", spec.SafeName)
            .Replace("__DISPLAY__", spec.DisplayName)
            .Replace("__TITLESCENE__", titleScenePath)
            .Replace("__GAMESCENE__", gameScenePath)
            .Replace("__SPECPATH__", specPath)
            .Replace("__ICONS__", IconLoop(spec.Genre));

        e.Add($"{editorDir}/SceneBuilder.cs",
            UnityEmitter.WithNamespace(body, spec.SafeName + ".Editor"));
    }

    /// <summary>
    /// 장르마다 아이콘을 들고 있는 테이블이 다르다. 장르를 추가하면 여기에 한 줄 넣는다.
    /// (빼먹으면 없는 필드를 참조해 에디터 스크립트가 컴파일되지 않는다.)
    /// </summary>
    static string IconLoop(string genre)
    {
        const string Nl = "\n";
        return genre switch
        {
            "idle" =>
                "        foreach (var g in GameData.Generators) yield return g.Icon;" + Nl +
                "        foreach (var u in GameData.Upgrades) yield return u.Icon;",
            "towerdefense" =>
                "        foreach (var t in GameData.Towers) yield return t.Icon;" + Nl +
                "        foreach (var en in GameData.Enemies) yield return en.Icon;",
            "autobattler" =>
                "        foreach (var u in GameData.Units) yield return u.Icon;",
            "survivor" =>
                "        foreach (var w in GameData.Weapons) yield return w.Icon;",
            // merge / match3 은 자원 아이콘 외에 별도 테이블이 없다.
            _ => "        yield break;",
        };
    }

    const string SceneBuilder = """
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using __NS__;
using __GAME__.Unity;

/// <summary>메뉴 한 번(또는 배치모드 한 줄)으로 플레이 가능한 씬들을 만든다.
/// 타이틀 씬 → 게임 씬 2씬 구성. 둘 다 빌드 세팅에 자동 등록된다.</summary>
public static class SceneBuilder
{
    const string TitleScenePath = "__TITLESCENE__";
    const string GameScenePath = "__GAMESCENE__";

    /// <summary>이 게임을 만든 기획서. 단일 진실원본이므로, 없으면 어떤 메뉴도 실행하지 않는다.</summary>
    const string SpecPath = "__SPECPATH__";

    /// <summary>기획서가 없으면 메뉴 자체를 회색 처리한다(모든 메뉴가 공유).</summary>
    [MenuItem("GameForge/__DISPLAY__/씬 생성 및 열기 (타이틀부터)", true)]
    [MenuItem("GameForge/__DISPLAY__/게임 씬 열기", true)]
    [MenuItem("GameForge/__DISPLAY__/모바일 설정 적용 (안드로이드)", true)]
    [MenuItem("GameForge/__DISPLAY__/입력 설정을 Both 로 변경", true)]
    [MenuItem("GameForge/__DISPLAY__/리소스 점검", true)]
    [MenuItem("GameForge/__DISPLAY__/프로젝트 설정 점검", true)]
    static bool SpecExists() => File.Exists(SpecPath);

    /// <summary>실행 직전 한 번 더 막는다 — 배치모드나 다른 스크립트가 직접 호출하는 경로까지 방어.</summary>
    static bool SpecReady()
    {
        if (File.Exists(SpecPath)) return true;
        Debug.LogError("[GameForge] 기획서가 없습니다: " + SpecPath +
            " — 기획서 없이 메뉴를 실행할 수 없습니다. 기획서를 복구하거나, " +
            "이 게임을 지운 것이라면 Assets/Games/__GAME__ 폴더도 함께 지우세요.");
        return false;
    }

    [MenuItem("GameForge/__DISPLAY__/씬 생성 및 열기 (타이틀부터)")]
    public static void BuildAndOpen()
    {
        if (!SpecReady()) return;
        BuildAll();
        EditorSceneManager.OpenScene(TitleScenePath);
        Debug.Log($"[GameForge] 씬 생성 완료: {TitleScenePath}, {GameScenePath} (빌드 세팅 등록됨)");
    }

    [MenuItem("GameForge/__DISPLAY__/게임 씬 열기")]
    public static void OpenGameScene()
    {
        if (!SpecReady()) return;
        if (!File.Exists(GameScenePath)) BuildAll();
        EditorSceneManager.OpenScene(GameScenePath);
    }

    /// <summary>배치모드 진입점: Unity.exe -executeMethod __GAME__.Editor.SceneBuilder.BuildFromCommandLine</summary>
    public static void BuildFromCommandLine()
    {
        if (!SpecReady()) return;
        BuildAll();
        Debug.Log("[GameForge] 배치 씬 생성 완료");
    }

    static void BuildAll()
    {
        BuildTitleScene();
        BuildGameScene();
        // 씬 이름으로 LoadScene 하려면 에디터 Play 에서도 빌드 세팅 등록이 필요하다.
        AddToBuildSettings();
    }

    static void BuildTitleScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        NewCamera();
        new GameObject("TitleScreen").AddComponent<TitleScreen>();
        AddEventSystem();

        SaveScene(scene, TitleScenePath);
    }

    static void BuildGameScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        NewCamera();
        new GameObject("GameRunner").AddComponent<GameRunner>();
        AddEventSystem();

        SaveScene(scene, GameScenePath);
    }

    static void NewCamera()
    {
        var camera = new GameObject("Main Camera", typeof(Camera));
        camera.tag = "MainCamera";
        // 2D 표준 카메라 위치. z=0 이면 z=0 에 놓이는 월드 스프라이트가 near clip 에 잘려 전부 안 보인다.
        camera.transform.position = new Vector3(0f, 0f, -10f);
        var cam = camera.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.1f);
        cam.orthographic = true;
        cam.orthographicSize = ViewConfig.CameraSize;
    }

    static void AddEventSystem()
    {
        var events = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_LEGACY_INPUT_MANAGER
        events.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#else
        // 새 Input System 전용 설정에서는 StandaloneInputModule 이 런타임에 예외를 던진다.
        // 입력 모듈 없이 씬만 만들고, 무엇을 바꿔야 하는지 알린다.
        events.name = "EventSystem (입력 모듈 없음)";
        Debug.LogWarning("[GameForge] UI 입력 모듈을 넣지 못했습니다. " +
            "메뉴 > GameForge > __DISPLAY__ > 프로젝트 설정 점검 을 실행하세요.");
#endif
    }

    static void SaveScene(UnityEngine.SceneManagement.Scene scene, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.Refresh();
    }

    static void AddToBuildSettings()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool changed = false;
        // 타이틀이 0번(첫 로드), 게임이 그 뒤.
        foreach (var path in new[] { GameScenePath, TitleScenePath })
        {
            if (scenes.Exists(s => s.path == path)) continue;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            changed = true;
        }
        if (changed) EditorBuildSettings.scenes = scenes.ToArray();
    }

    [MenuItem("GameForge/__DISPLAY__/리소스 점검")]
    public static void VerifyAssets()
    {
        if (!SpecReady()) return;
        int missing = 0;
        foreach (var name in IconNames())
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (Found(name)) continue;
            Debug.LogWarning($"[GameForge] 이미지 없음: {name}  (플레이스홀더로 대체됨)");
            missing++;
        }
        Debug.Log(missing == 0
            ? "[GameForge] 리소스 점검 완료 — 빠진 이미지 없음"
            : $"[GameForge] 리소스 점검 완료 — {missing}개 누락, 플레이스홀더로 진행 가능");
    }

    /// <summary>
    /// 2D 개발에 필요한 프로젝트 설정을 점검한다.
    /// Active Input Handling 이 새 Input System 전용이면 UI 클릭과 화면 터치가 모두 죽는다.
    /// </summary>
    [MenuItem("GameForge/__DISPLAY__/프로젝트 설정 점검")]
    public static void CheckProjectSettings()
    {
        if (!SpecReady()) return;
        var handler = InputHandlerProperty();
        if (handler == null)
        {
            Debug.LogWarning("[GameForge] Active Input Handling 설정을 읽지 못했습니다. " +
                "Project Settings > Player 에서 직접 확인하세요.");
            return;
        }

        var target = EditorUserBuildSettings.activeBuildTarget;
        if (target != BuildTarget.Android)
            Debug.LogWarning($"[GameForge] 빌드 타겟이 {target} 입니다. 메인 타겟은 Android — " +
                "메뉴 > GameForge > __DISPLAY__ > 모바일 설정 적용 을 실행하세요.");

        string name = handler.intValue == 0 ? "Input Manager (Old)"
                    : handler.intValue == 1 ? "Input System Package (New)"
                    : "Both";

        if (handler.intValue == 1)
        {
            Debug.LogError($"[GameForge] Active Input Handling = {name}. " +
                "이 상태로는 UI 버튼과 화면 터치가 동작하지 않습니다. " +
                "메뉴 > GameForge > __DISPLAY__ > 입력 설정을 Both 로 변경 을 실행하세요(에디터 재시작 필요).");
        }
        else
        {
            Debug.Log($"[GameForge] Active Input Handling = {name} — 입력 정상.");
        }
    }

    /// <summary>
    /// 모바일(안드로이드 메인) 타겟 설정을 한 번에 적용한다.
    /// 이 프로젝트의 모든 게임은 세로 모바일 기준이므로, 프로젝트를 처음 받으면 이것부터 실행한다.
    /// </summary>
    [MenuItem("GameForge/__DISPLAY__/모바일 설정 적용 (안드로이드)")]
    public static void ApplyMobileSettings()
    {
        if (!SpecReady()) return;
        // 1) 화면: 세로 고정 (가로 자동회전 차단)
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        // 2) 안드로이드: 스토어 등록 가능한 표준 구성
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,
            ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

        AssetDatabase.SaveAssets();
        Debug.Log("[GameForge] 모바일 설정 적용: 세로 고정, IL2CPP, ARM64+ARMv7, minSdk 24");

        // 3) 빌드 타겟 전환 (이미 안드로이드면 건너뜀). 첫 전환은 리임포트로 시간이 걸린다.
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log("[GameForge] 빌드 타겟을 Android 로 전환합니다 — 첫 전환은 몇 분 걸릴 수 있습니다.");
            EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
        }
        else
        {
            Debug.Log("[GameForge] 빌드 타겟은 이미 Android 입니다.");
        }
    }

    [MenuItem("GameForge/__DISPLAY__/입력 설정을 Both 로 변경")]
    public static void SetInputHandlingBoth()
    {
        if (!SpecReady()) return;
        var handler = InputHandlerProperty();
        if (handler == null) { Debug.LogWarning("[GameForge] 설정을 읽지 못했습니다."); return; }

        if (handler.intValue == 2) { Debug.Log("[GameForge] 이미 Both 입니다."); return; }

        handler.intValue = 2;
        handler.serializedObject.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("[GameForge] Active Input Handling 을 Both 로 바꿨습니다. " +
                  "적용하려면 유니티를 재시작하세요.");
    }

    static SerializedProperty InputHandlerProperty()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets == null || assets.Length == 0 || assets[0] == null) return null;
        return new SerializedObject(assets[0]).FindProperty("activeInputHandler");
    }

    static readonly string[] SearchFolders = { "Icons/", "Sprites/", "Backgrounds/", "" };

    /// <summary>Resources 하위 폴더를 순서대로 뒤진다. 스프라이트/텍스처 어느 쪽으로 임포트됐든 찾는다.</summary>
    static bool Found(string name)
    {
        foreach (var folder in SearchFolders)
        {
            if (Resources.Load<Sprite>(folder + name) != null) return true;
            if (Resources.Load<Texture2D>(folder + name) != null) return true;
        }
        return false;
    }

    static System.Collections.Generic.IEnumerable<string> IconNames()
    {
        foreach (var r in GameData.Resources) yield return r.Icon;
__ICONS__
    }
}
""";
}
