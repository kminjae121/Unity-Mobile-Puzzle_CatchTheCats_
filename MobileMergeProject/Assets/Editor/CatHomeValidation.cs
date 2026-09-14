using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using _Code.Block;
using _Code.CatHome;
using _Code.Field;
using _Code.Manager;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CatHomeValidation
{
    private static int _checks;
    private const string Output = "Library/CatHomeValidation";

    [MenuItem("Tools/Catch the Cats/Validate Cat Growth")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Run validation outside Play mode.");
        _checks = 0;
        Directory.CreateDirectory(Output);
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            CatGrowthCatalog catalog = AssetDatabase.LoadAssetAtPath<CatGrowthCatalog>(CatHomeAssetBuilder.CatalogPath);
            Check(catalog != null && catalog.cats.Length == 4, "Four cat definitions");
            foreach (CatGrowthCatalog.CatDefinition cat in catalog.cats)
                Check(cat.blockSprite != null && cat.kitten != null && cat.juvenile != null && cat.adult != null,
                    "All sprites present: " + cat.type);
            Check(catalog.GetStage(0) == 0, "Kitten threshold");
            Check(catalog.GetStage(1) == 1 && catalog.GetStage(2) == 1, "Juvenile threshold");
            Check(catalog.GetStage(3) == 2 && catalog.GetStage(int.MaxValue) == 2, "Adult threshold and cap");
            for (int i = 0; i < 3; i++)
                Check(catalog.GetSize(i) < catalog.GetSize(i + 1), "Gradual size increase " + i);
            Check(Mathf.Approximately(catalog.GetSize(3), catalog.GetSize(4)), "Adult size cap");

            BlockField board = BuildBoard();
            ValidateLines(board, catalog);
            ValidateSingleCellOutlineSprite(board, catalog);
            ValidateTutorialGrowthStep();
            JsonManager save = BuildSave();
            ValidateSave(save);
            Image background = BuildPreviewBackground();
            CatHomeGrowthController controller = new GameObject("GrowthControllerTest").AddComponent<CatHomeGrowthController>();
            controller.Initialize(board, save);
            CatResidentView[] views = background.GetComponentsInChildren<CatResidentView>();
            Check(views.Length == 4, "Residents instantiated once");
            foreach (Image graphic in background.GetComponentsInChildren<Image>())
                if (graphic != background)
                    Check(!graphic.raycastTarget, "Resident does not intercept dragging or swipes");

            Render(background, "kittens-720x1080.png", 720, 1080);
            Render(background, "kittens-540x1200.png", 540, 1200);
            for (int i = 0; i < 3; i++)
            {
                board.ClearAll();
                for (int x = 0; x < 6; x++)
                    board.Installables[x, 0].SetObject(board.gameObject, Color.white, catalog.cats[0].blockSprite, x + 1);
                board.ClearCompletedLines();
            }
            Check(save.GetCatGrowthLines(0) == 3, "Three matching black rows grow a black cat to adult");
            Check(save.GetCatGrowthLines(1) == 0 && save.GetCatGrowthLines(2) == 0 && save.GetCatGrowthLines(3) == 0,
                "Other cats do not grow");
            Check(views.Single(view => view.CatType == HomeCatType.Black).Portrait.sprite == catalog.cats[0].adult,
                "Correct cat changes to adult sprite");
            save.Load();
            Check(save.GetCatGrowthLines(0) == 3, "Growth survives a reload during the current game");
            controller.Initialize(board, save);
            controller.Initialize(board, save);
            board.ClearAll();
            for (int x = 0; x < 6; x++)
                board.Installables[x, 0].SetObject(board.gameObject, Color.white, catalog.cats[0].blockSprite, x + 1);
            board.ClearCompletedLines();
            Check(save.GetCatGrowthLines(0) == 4, "Reinitializing does not duplicate event subscriptions");
            Check(background.GetComponentsInChildren<CatResidentView>().Length == 4, "Reinitializing does not duplicate residents");
            controller.enabled = false;
            // Edit-mode checks invoke lifecycle methods explicitly; Unity dispatches them in Play mode.
            typeof(CatHomeGrowthController).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            board.ClearAll();
            for (int x = 0; x < 6; x++)
                board.Installables[x, 0].SetObject(board.gameObject, Color.white, catalog.cats[0].blockSprite, x + 1);
            board.ClearCompletedLines();
            Check(save.GetCatGrowthLines(0) == 4, "Disabled controller unsubscribes");

            foreach (CatResidentView view in views)
            {
                view.SetProgress(catalog, catalog.cats[(int)view.CatType], 3, false);
                MethodInfo pose = typeof(CatResidentView).GetMethod("ApplyPose", BindingFlags.Instance | BindingFlags.NonPublic);
                pose.Invoke(view, new object[] { 0f });
                Vector3 before = view.Portrait.transform.localScale;
                pose.Invoke(view, new object[] { 1f });
                Check((view.Portrait.transform.localScale - before).sqrMagnitude > 0.000001f,
                    "Idle animation changes pose: " + view.CatType);
                pose.Invoke(view, new object[] { 0f });
            }
            Render(background, "adults-720x1080.png", 720, 1080);
            Render(background, "adults-540x1200.png", 540, 1200);
            Object.DestroyImmediate(background.transform.GetChild(0).gameObject);
            Object.DestroyImmediate(controller.gameObject);
            board.ClearAll();
            ValidateGameplayScenes();
            string report = "PASS: " + _checks + " checks. Includes 100 seeded board cases, mixed/cheese/cross lines, " +
                "growth thresholds, independent types, old saves, reload, duplicate subscriptions, animation and 11 gameplay scenes.";
            File.WriteAllText(Output + "/report.txt", report);
            Debug.Log(report);
        }
        finally
        {
            SceneManager.SetActiveScene(previousScene);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static BlockField BuildBoard()
    {
        GameObject root = new GameObject("CatGrowthTestBoard");
        for (int y = 0; y < 6; y++)
        for (int x = 0; x < 6; x++)
        {
            GameObject cell = new GameObject("Cell");
            cell.transform.SetParent(root.transform);
            cell.AddComponent<Field>().Configure(new Vector2Int(x, y), null, null);
        }
        BlockField board = root.AddComponent<BlockField>();
        board.Rebuild();
        return board;
    }

    private static void ValidateLines(BlockField board, CatGrowthCatalog catalog)
    {
        List<Sprite> actual = new List<Sprite>();
        Action<IReadOnlyList<Sprite>> listener = sprites => actual.AddRange(sprites);
        board.MatchingCatLinesCleared += listener;
        System.Random random = new System.Random(91827);
        for (int test = 0; test < 100; test++)
        {
            int[,] cells = new int[6, 6];
            for (int y = 0; y < 6; y++)
            for (int x = 0; x < 6; x++)
                cells[x, y] = test < 4 ? test : random.Next(-2, 4);
            if (test >= 4 && test % 2 == 0)
                for (int x = 0; x < 6; x++) cells[x, test % 6] = test % 4;
            if (test >= 4 && test % 3 == 0)
                for (int y = 0; y < 6; y++) cells[test % 6, y] = test % 4;
            CheckBoard(board, catalog, cells, actual, "seeded board " + test);
        }
        int[,] cross = new int[6, 6];
        for (int y = 0; y < 6; y++)
        for (int x = 0; x < 6; x++) cross[x, y] = x == 2 || y == 2 ? 2 : -1;
        CheckBoard(board, catalog, cross, actual, "intersecting same-type row and column");
        Check(actual.Count == 2, "Intersection counts as two lines, not one and not eleven blocks");
        board.ClearAll();
        actual.Clear();
        for (int x = 0; x < 6; x++)
            board.Installables[x, 0].SetObject(board.gameObject, Color.white, catalog.cats[0].blockSprite, x + 1);
        board.Installables[2, 0].SetStageCheeseObject(board.gameObject, catalog.cats[0].blockSprite, 99);
        Check(board.ClearCompletedLines() == 1 && actual.Count == 0, "Cheese never counts as a same-cat line");
        Check(board.ClearCompletedLines() == 0 && actual.Count == 0, "Repeated clearing awards nothing");
        board.MatchingCatLinesCleared -= listener;
    }

    private static void ValidateTutorialGrowthStep()
    {
        GameObject root = new GameObject("TutorialGrowthStepTest");
        TutorialController tutorial = root.AddComponent<TutorialController>();
        try
        {
            typeof(TutorialController).GetMethod("BuildSteps", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(tutorial, null);
            System.Collections.IList steps = typeof(TutorialController)
                .GetField("_steps", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(tutorial) as System.Collections.IList;
            bool foundGrowthStep = false;

            foreach (object step in steps)
            {
                Type stepType = step.GetType();
                string title = (string)stepType.GetProperty("Title").GetValue(step);
                if (title != "고양이 성장")
                    continue;

                foundGrowthStep = true;
                string body = (string)stepType.GetProperty("Body").GetValue(step);
                Check(stepType.GetProperty("Kind").GetValue(step).ToString() == "Message",
                    "Cat growth tutorial is a touch-through explanation");
                Check(body.Contains("같은 고양이") && body.Contains("3번") && body.Contains("성체"),
                    "Cat growth tutorial explains the three-clear adult goal");
            }

            Check(foundGrowthStep, "Tutorial contains a cat growth explanation");

            typeof(TutorialController).GetField("_isRunning", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(tutorial, true);
            typeof(TutorialController).GetField("_isMissionActive", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(tutorial, false);
            Check(tutorial.IsGameplayInputLocked, "Speech bubble locks gameplay input");
            Check(!tutorial.CanShiftBoard() && !tutorial.CanInteractWithPiece(null),
                "Speech bubble allows only dialogue advancement");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void ValidateSingleCellOutlineSprite(BlockField board, CatGrowthCatalog catalog)
    {
        Sprite originalSprite = catalog.cats[0].blockSprite;
        Sprite outlinedSprite = BlockBlastSpriteLibrary.GetSingleCellCatSprite(originalSprite);
        Check(outlinedSprite != null && outlinedSprite != originalSprite,
            "Single-cell cat uses a dedicated outlined sprite");

        board.ClearAll();
        board.Installables[0, 0].SetObject(board.gameObject, Color.white, originalSprite, 501);
        board.Installables[0, 1].SetObject(board.gameObject, Color.white, originalSprite, 501);
        for (int x = 1; x < board.Width; x++)
            board.Installables[x, 0].SetStageCheeseObject(board.gameObject, BlockBlastSpriteLibrary.CheeseBlockSprite, 700 + x);

        Check(board.ClearCompletedLines() == 1, "Partial block test clears its completed row");
        Field remainingField = board.Installables[0, 1];
        Check(remainingField.CurrentSprite == originalSprite,
            "Single-cell outline keeps the original cat identity for matching lines");
        Check(remainingField.VisualSprite == outlinedSprite,
            "Surviving single cell switches to the outlined cat sprite");
        board.ClearAll();
    }

    private static void CheckBoard(BlockField board, CatGrowthCatalog catalog, int[,] cells, List<Sprite> actual, string name)
    {
        board.ClearAll();
        actual.Clear();
        int[] expectedGrowth = new int[4];
        int expectedLines = 0;
        for (int axis = 0; axis < 2; axis++)
        for (int line = 0; line < 6; line++)
        {
            int[] values = Enumerable.Range(0, 6).Select(i => axis == 0 ? cells[i, line] : cells[line, i]).ToArray();
            if (values.Any(value => value < 0)) continue;
            expectedLines++;
            if (values.Distinct().Count() == 1) expectedGrowth[values[0]]++;
        }
        for (int y = 0; y < 6; y++)
        for (int x = 0; x < 6; x++)
            if (cells[x, y] >= 0)
                board.Installables[x, y].SetObject(board.gameObject, Color.white, catalog.cats[cells[x, y]].blockSprite, x + y * 6 + 1);
        Check(board.ClearCompletedLines() == expectedLines, name + ": line count");
        int[] actualGrowth = new int[4];
        foreach (Sprite sprite in actual) actualGrowth[(int)catalog.Find(sprite).type]++;
        Check(actualGrowth.SequenceEqual(expectedGrowth), name + ": exact per-cat growth count");
    }

    private static JsonManager BuildSave()
    {
        GameObject root = new GameObject("IsolatedCatGrowthSave");
        root.SetActive(false);
        JsonManager save = root.AddComponent<JsonManager>();
        SerializedObject serialized = new SerializedObject(save);
        serialized.FindProperty("_fileName").stringValue = Path.GetFullPath(Output + "/test-save.json");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return save;
    }

    private static void ValidateSave(JsonManager save)
    {
        File.WriteAllText(save.SavePath, "{\"MaxScore\":250,\"Gold\":90}");
        save.Load();
        Check(save.MaxScore == 250 && save.Gold == 90 && save.GetCatGrowthLines(0) == 0, "Older saves migrate without losing scores");
        File.WriteAllText(save.SavePath, "{\"CatGrowthLines\":[-1,2]}");
        save.Load();
        Check(save.GetCatGrowthLines(0) == 0 && save.GetCatGrowthLines(1) == 2 && save.GetCatGrowthLines(3) == 0,
            "Short or negative growth data normalizes");
        save.ResetSaveData();
        save.AddCatGrowthLines(new[] { 1, 2, 3, 4 });
        save.MergePlayerData(500, 40, "2026-09-10");
        save.Load();
        Check(Enumerable.Range(0, 4).Select(save.GetCatGrowthLines).SequenceEqual(new[] { 1, 2, 3, 4 }),
            "Server score merge preserves local cat growth");
        save.AddCatGrowthLines(new[] { int.MaxValue, 0, -2, 0 });
        Check(save.GetCatGrowthLines(0) == int.MaxValue && save.GetCatGrowthLines(2) == 3, "Growth cannot overflow or decrease");
        save.SetMaxScore(777);
        save.SetGold(88);
        save.ResetCatGrowthLines();
        Check(Enumerable.Range(0, 4).Select(save.GetCatGrowthLines).All(lines => lines == 0),
            "A new game resets every cat's growth");
        Check(save.MaxScore == 777 && save.Gold == 88, "A new game keeps score and gold data");
        save.ResetSaveData();
    }

    private static Image BuildPreviewBackground()
    {
        GameObject root = new GameObject("CatHomePreview", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        GameObject room = new GameObject("Room", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        room.transform.SetParent(root.transform, false);
        CatHomeAssetBuilder.Stretch((RectTransform)room.transform);
        Image background = room.GetComponent<Image>();
        background.sprite = BlockBlastSpriteLibrary.NightCatHomeBackgroundSprite;
        return background;
    }

    private static void Render(Image background, string file, int width, int height)
    {
        RenderPipelineAsset oldPipeline = GraphicsSettings.defaultRenderPipeline;
        RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
        GraphicsSettings.defaultRenderPipeline = null;
        QualitySettings.renderPipeline = null;
        GameObject cameraObject = new GameObject("PreviewCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        RenderTexture target = new RenderTexture(width, height, 24);
        Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            RectTransform canvas = (RectTransform)background.canvas.transform;
            canvas.sizeDelta = new Vector2(width, height);
            canvas.position = new Vector3(10000f, 0f, 0f);
            camera.transform.position = new Vector3(10000f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = height * 0.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();
            Check(result.GetPixel(width / 2, height / 2) != Color.magenta, "Rendered background " + file);
            File.WriteAllBytes(Output + "/" + file, result.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previousTarget;
            camera.targetTexture = null;
            Object.DestroyImmediate(result);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
            GraphicsSettings.defaultRenderPipeline = oldPipeline;
            QualitySettings.renderPipeline = oldQuality;
        }
    }

    private static void ValidateGameplayScenes()
    {
        string[] paths = Enumerable.Range(1, 10).Select(i => "Assets/Scenes/Stage" + i + "Scene.unity")
            .Prepend("Assets/Scenes/GameScene.unity").ToArray();
        Vector2 infiniteTutorialAnchorMin = Vector2.zero;
        Vector2 infiniteTutorialAnchorMax = Vector2.zero;
        Vector2 infiniteTutorialPosition = Vector2.zero;
        Vector2 infiniteTutorialSize = Vector2.zero;
        Vector2 infiniteTutorialPivot = Vector2.zero;
        bool foundInfiniteTutorialButton = false;
        Vector2 infiniteMainButtonAnchorMin = Vector2.zero;
        Vector2 infiniteMainButtonAnchorMax = Vector2.zero;
        Vector2 infiniteMainButtonPosition = Vector2.zero;
        Vector2 infiniteMainButtonSize = Vector2.zero;
        Vector2 infiniteMainButtonPivot = Vector2.zero;
        bool foundInfiniteMainButton = false;
        foreach (string path in paths)
        {
            Scene loaded = SceneManager.GetSceneByPath(path);
            bool alreadyLoaded = loaded.IsValid() && loaded.isLoaded;
            if (!alreadyLoaded) loaded = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = loaded.GetRootGameObjects();
                Check(roots.Any(root => root.GetComponentInChildren<GameManager>(true) != null), path + ": GameManager hook");
                Check(roots.SelectMany(root => root.GetComponentsInChildren<Image>()).Any(image => image.isActiveAndEnabled &&
                    image.sprite == BlockBlastSpriteLibrary.NightCatHomeBackgroundSprite), path + ": active matching room background");

                Button tutorialButton = roots.SelectMany(root => root.GetComponentsInChildren<Button>(true))
                    .FirstOrDefault(button => button.name == "TutorialButton");
                if (path == "Assets/Scenes/GameScene.unity")
                {
                    CheckTutorialButtonConnection(tutorialButton, path);
                    RectTransform rectTransform = (RectTransform)tutorialButton.transform;
                    infiniteTutorialAnchorMin = rectTransform.anchorMin;
                    infiniteTutorialAnchorMax = rectTransform.anchorMax;
                    infiniteTutorialPosition = rectTransform.anchoredPosition;
                    infiniteTutorialSize = rectTransform.sizeDelta;
                    infiniteTutorialPivot = rectTransform.pivot;
                    foundInfiniteTutorialButton = true;

                    Button mainButton = roots.SelectMany(root => root.GetComponentsInChildren<Button>(true))
                        .FirstOrDefault(button => button.name == "MainSceneButton");
                    CheckButtonConnection(mainButton, path, "main button", "ReturnToMainScene");
                    RectTransform mainButtonRect = (RectTransform)mainButton.transform;
                    infiniteMainButtonAnchorMin = mainButtonRect.anchorMin;
                    infiniteMainButtonAnchorMax = mainButtonRect.anchorMax;
                    infiniteMainButtonPosition = mainButtonRect.anchoredPosition;
                    infiniteMainButtonSize = mainButtonRect.sizeDelta;
                    infiniteMainButtonPivot = mainButtonRect.pivot;
                    foundInfiniteMainButton = true;
                }
                else if (path == "Assets/Scenes/Stage1Scene.unity")
                {
                    Check(foundInfiniteTutorialButton, "Infinite-mode tutorial button was read before Stage 1");
                    CheckTutorialButtonConnection(tutorialButton, path);
                    RectTransform rectTransform = (RectTransform)tutorialButton.transform;
                    Check(Vector2.Distance(rectTransform.anchorMin, infiniteTutorialAnchorMin) < 0.001f &&
                        Vector2.Distance(rectTransform.anchorMax, infiniteTutorialAnchorMax) < 0.001f &&
                        Vector2.Distance(rectTransform.anchoredPosition, infiniteTutorialPosition) < 0.001f &&
                        Vector2.Distance(rectTransform.sizeDelta, infiniteTutorialSize) < 0.001f &&
                        Vector2.Distance(rectTransform.pivot, infiniteTutorialPivot) < 0.001f,
                        "Stage 1 tutorial button matches the infinite-mode position and size");
                }

                if (path.StartsWith("Assets/Scenes/Stage", StringComparison.Ordinal))
                {
                    Check(foundInfiniteMainButton, "Infinite-mode main button was read before stage scenes");
                    Button stageSelectButton = roots.SelectMany(root => root.GetComponentsInChildren<Button>(true))
                        .FirstOrDefault(button => button.name == "StageSelectButton");
                    CheckButtonConnection(stageSelectButton, path, "stage-select button", "ReturnToStageSelect");
                    RectTransform stageButtonRect = (RectTransform)stageSelectButton.transform;
                    Check(MatchesLayout(
                            stageButtonRect,
                            infiniteMainButtonAnchorMin,
                            infiniteMainButtonAnchorMax,
                            infiniteMainButtonPosition,
                            infiniteMainButtonSize,
                            infiniteMainButtonPivot),
                        path + ": stage-select button matches the infinite-mode main button");
                }
            }
            finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(loaded, true); }
        }
    }

    private static void CheckTutorialButtonConnection(Button tutorialButton, string scenePath)
    {
        CheckButtonConnection(tutorialButton, scenePath, "tutorial button", "StartTutorial");
    }

    private static void CheckButtonConnection(Button button, string scenePath, string buttonDescription, string expectedMethodName)
    {
        Check(button != null && button.isActiveAndEnabled, scenePath + ": active " + buttonDescription);
        Check(button.onClick.GetPersistentEventCount() == 1 &&
            button.onClick.GetPersistentMethodName(0) == expectedMethodName &&
            button.onClick.GetPersistentTarget(0) is GameManager,
            scenePath + ": " + buttonDescription + " calls " + expectedMethodName);
    }

    private static bool MatchesLayout(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Vector2 pivot)
    {
        return Vector2.Distance(rectTransform.anchorMin, anchorMin) < 0.001f &&
               Vector2.Distance(rectTransform.anchorMax, anchorMax) < 0.001f &&
               Vector2.Distance(rectTransform.anchoredPosition, anchoredPosition) < 0.001f &&
               Vector2.Distance(rectTransform.sizeDelta, sizeDelta) < 0.001f &&
               Vector2.Distance(rectTransform.pivot, pivot) < 0.001f;
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Cat home validation failed: " + label);
        _checks++;
    }
}
