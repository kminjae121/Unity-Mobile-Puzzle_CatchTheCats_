using System;
using System.Linq;
using _Code.Manager;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class StageNavigationButtonBuilder
{
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Catch the Cats/Build Stage Select Buttons")]
    public static void BuildAll()
    {
        Scene sourceScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        try
        {
            Button sourceButton = FindButton(sourceScene, "MainSceneButton");
            if (sourceButton == null)
                throw new InvalidOperationException("GameScene requires a MainSceneButton to build stage navigation buttons.");

            for (int stageNumber = 1; stageNumber <= 10; stageNumber++)
                BuildStageButton(sourceButton, stageNumber);

            AssetDatabase.SaveAssets();
            Debug.Log("Stage-select buttons are ready in Stage1Scene through Stage10Scene.");
        }
        finally
        {
            if (sourceScene.IsValid() && sourceScene.isLoaded && SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(sourceScene, true);
        }
    }

    private static void BuildStageButton(Button sourceButton, int stageNumber)
    {
        string stagePath = "Assets/Scenes/Stage" + stageNumber + "Scene.unity";
        Scene stageScene = EditorSceneManager.OpenScene(stagePath, OpenSceneMode.Additive);
        try
        {
            Canvas canvas = FindRootCanvas(stageScene);
            GameManager gameManager = FindComponent<GameManager>(stageScene);
            if (canvas == null || gameManager == null)
                throw new InvalidOperationException(stagePath + " requires a root Canvas and GameManager.");

            Button existingButton = FindButton(stageScene, "StageSelectButton");
            if (existingButton != null)
                Object.DestroyImmediate(existingButton.gameObject);

            SceneManager.SetActiveScene(stageScene);
            Button stageButton = Object.Instantiate(sourceButton, canvas.transform, false);
            stageButton.gameObject.name = "StageSelectButton";
            stageButton.gameObject.SetActive(true);
            stageButton.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(stageButton.onClick, gameManager.ReturnToStageSelect);

            TextMeshProUGUI label = stageButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.gameObject.name = "StageSelectButtonLabelTxt";
                label.text = "스테이지 선택";
            }

            EditorSceneManager.MarkSceneDirty(stageScene);
            EditorSceneManager.SaveScene(stageScene);
        }
        finally
        {
            if (stageScene.IsValid() && stageScene.isLoaded)
                EditorSceneManager.CloseScene(stageScene, true);
        }
    }

    private static Button FindButton(Scene scene, string buttonName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Button>(true))
            .FirstOrDefault(button => button.name == buttonName);
    }

    private static Canvas FindRootCanvas(Scene scene)
    {
        return scene.GetRootGameObjects()
            .Select(root => root.GetComponent<Canvas>())
            .FirstOrDefault(canvas => canvas != null && canvas.isRootCanvas && canvas.isActiveAndEnabled);
    }

    private static T FindComponent<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .FirstOrDefault();
    }
}
