using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.UI;
using RedRunner;
using RedRunner.UI;
using RedRunner.Networking;

/// <summary>
/// One-click setup for Web3Auth login integration.
/// Run via Unity menu: Tools > Web3Auth > Setup Scene.
/// This creates the LoginScreen UI, animation controller, Web3AuthManager, ApiManager,
/// and wires everything into the existing UIManager.
///
/// After running, you still need to:
/// 1. Set the WebGL template to "Web3Auth" in Player Settings > WebGL > Resolution and Presentation
/// 2. Optionally adjust LoginScreen and StartScreen layout in the Scene view
/// </summary>
public class Web3AuthSceneSetup
{
    [MenuItem("Tools/Web3Auth/Setup Scene")]
    public static void SetupScene()
    {
        // --- 1. Create Login Screen Animator Controller ---
        string animFolder = "Assets/Animations/Screens/Login Screen";
        if (!AssetDatabase.IsValidFolder(animFolder))
        {
            AssetDatabase.CreateFolder("Assets/Animations/Screens", "Login Screen");
        }

        string controllerPath = animFolder + "/Login Screen.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Add "Open" bool parameter
            controller.AddParameter("Open", AnimatorControllerParameterType.Bool);

            var rootStateMachine = controller.layers[0].stateMachine;

            // Create Closed state (default)
            var closedClip = CreateSimpleClip(animFolder + "/Closed.anim", 0f);
            var closedState = rootStateMachine.AddState("Closed");
            closedState.motion = closedClip;
            rootStateMachine.defaultState = closedState;

            // Create Open state
            var openClip = CreateSimpleClip(animFolder + "/Open.anim", 1f);
            var openState = rootStateMachine.AddState("Open");
            openState.motion = openClip;

            // Transition: Closed -> Open when Open = true
            var toOpen = closedState.AddTransition(openState);
            toOpen.AddCondition(AnimatorConditionMode.If, 0, "Open");
            toOpen.hasExitTime = false;
            toOpen.duration = 0.25f;

            // Transition: Open -> Closed when Open = false
            var toClosed = openState.AddTransition(closedState);
            toClosed.AddCondition(AnimatorConditionMode.IfNot, 0, "Open");
            toClosed.hasExitTime = false;
            toClosed.duration = 0.25f;

            AssetDatabase.SaveAssets();
            Debug.Log("[Web3Auth Setup] Created Login Screen animator controller");
        }

        // --- 2. Find UIManager in scene ---
        var uiManager = Object.FindFirstObjectByType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogError("[Web3Auth Setup] UIManager not found in scene. Open Play.unity first.");
            return;
        }

        // --- 3. Find the main Canvas ---
        Canvas mainCanvas = null;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay || c.renderMode == RenderMode.ScreenSpaceCamera)
            {
                mainCanvas = c;
                break;
            }
        }
        if (mainCanvas == null && canvases.Length > 0)
            mainCanvas = canvases[0];

        if (mainCanvas == null)
        {
            Debug.LogError("[Web3Auth Setup] No Canvas found in scene.");
            return;
        }

        // --- 4. Check if LoginScreen already exists ---
        var existingLogin = Object.FindFirstObjectByType<LoginScreen>();
        if (existingLogin != null)
        {
            Debug.LogWarning("[Web3Auth Setup] LoginScreen already exists in scene. Skipping creation.");
        }
        else
        {
            CreateLoginScreen(mainCanvas, controller);
        }

        // --- 5. Create Web3AuthManager GameObject ---
        if (Object.FindFirstObjectByType<Web3AuthManager>() == null)
        {
            var web3AuthGO = new GameObject("Web3AuthManager");
            web3AuthGO.AddComponent<Web3AuthManager>();
            Undo.RegisterCreatedObjectUndo(web3AuthGO, "Create Web3AuthManager");
            Debug.Log("[Web3Auth Setup] Created Web3AuthManager GameObject");
        }
        else
        {
            Debug.Log("[Web3Auth Setup] Web3AuthManager already exists");
        }

        // --- 6. Create ApiManager GameObject ---
        if (Object.FindFirstObjectByType<ApiManager>() == null)
        {
            var apiManagerGO = new GameObject("ApiManager");
            apiManagerGO.AddComponent<ApiManager>();
            Undo.RegisterCreatedObjectUndo(apiManagerGO, "Create ApiManager");
            Debug.Log("[Web3Auth Setup] Created ApiManager GameObject");
        }
        else
        {
            Debug.Log("[Web3Auth Setup] ApiManager already exists");
        }

        // --- 6b. Create SessionManager GameObject ---
        if (Object.FindFirstObjectByType<SessionManager>() == null)
        {
            var sessionManagerGO = new GameObject("SessionManager");
            sessionManagerGO.AddComponent<SessionManager>();
            Undo.RegisterCreatedObjectUndo(sessionManagerGO, "Create SessionManager");
            Debug.Log("[Web3Auth Setup] Created SessionManager GameObject");
        }
        else
        {
            Debug.Log("[Web3Auth Setup] SessionManager already exists");
        }

        // --- 7. Wire LoginScreen into UIManager's screen list ---
        WireLoginScreenToUIManager(uiManager);

        // --- 8. Add user info fields to StartScreen ---
        SetupStartScreenUserInfo();

        EditorUtility.SetDirty(uiManager);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[Web3Auth Setup] Scene setup complete! Save the scene, then set WebGL template to 'Web3Auth' in Player Settings.");
    }

    private static AnimationClip CreateSimpleClip(string path, float alphaValue)
    {
        var clip = new AnimationClip();
        clip.name = System.IO.Path.GetFileNameWithoutExtension(path);

        // Animate CanvasGroup alpha
        var alphaCurve = new AnimationCurve(new Keyframe(0f, alphaValue));
        clip.SetCurve("", typeof(CanvasGroup), "m_Alpha", alphaCurve);

        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static void CreateLoginScreen(Canvas parentCanvas, AnimatorController controller)
    {
        // Create Login Screen root
        var loginScreenGO = new GameObject("Login Screen");
        loginScreenGO.transform.SetParent(parentCanvas.transform, false);

        // RectTransform - full screen
        var rect = loginScreenGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // CanvasGroup
        var canvasGroup = loginScreenGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Animator
        var animator = loginScreenGO.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        // Background panel
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(loginScreenGO.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.02f, 0.12f, 0.92f); // Dark purple overlay, menu visible behind

        // Title text
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(loginScreenGO.transform, false);
        var titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.7f);
        titleRect.anchorMax = new Vector2(0.5f, 0.7f);
        titleRect.sizeDelta = new Vector2(400f, 60f);
        var titleText = titleGO.AddComponent<Text>();
        titleText.text = "RED RUNNER";
        titleText.fontSize = 42;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Status text
        var statusGO = new GameObject("StatusText");
        statusGO.transform.SetParent(loginScreenGO.transform, false);
        var statusRect = statusGO.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0.55f);
        statusRect.anchorMax = new Vector2(0.5f, 0.55f);
        statusRect.sizeDelta = new Vector2(400f, 40f);
        var statusText = statusGO.AddComponent<Text>();
        statusText.text = "Sign in to play";
        statusText.fontSize = 20;
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.color = new Color(0.8f, 0.8f, 0.8f);
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Login Button
        var buttonGO = new GameObject("LoginButton");
        buttonGO.transform.SetParent(loginScreenGO.transform, false);
        var buttonRect = buttonGO.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.4f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.4f);
        buttonRect.sizeDelta = new Vector2(250f, 55f);
        var buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = new Color(0.22f, 0.01f, 0.23f, 1f); // Xertra purple #38023b
        var button = buttonGO.AddComponent<Button>();
        var buttonColors = button.colors;
        buttonColors.highlightedColor = new Color(0.35f, 0.05f, 0.37f);
        buttonColors.pressedColor = new Color(0.15f, 0.01f, 0.16f);
        button.colors = buttonColors;

        // Button label
        var buttonLabelGO = new GameObject("Label");
        buttonLabelGO.transform.SetParent(buttonGO.transform, false);
        var labelRect = buttonLabelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var labelText = buttonLabelGO.AddComponent<Text>();
        labelText.text = "Sign In";
        labelText.fontSize = 24;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Add LoginScreen component and wire references
        var loginScreen = loginScreenGO.AddComponent<LoginScreen>();

        // Use SerializedObject to set private/serialized fields
        var so = new SerializedObject(loginScreen);
        so.FindProperty("ScreenInfo").enumValueIndex = (int)UIScreenInfo.LOGIN_SCREEN;
        so.FindProperty("m_Animator").objectReferenceValue = animator;
        so.FindProperty("m_CanvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("LoginButton").objectReferenceValue = button;
        so.FindProperty("StatusText").objectReferenceValue = statusText;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(loginScreenGO, "Create Login Screen");
        Debug.Log("[Web3Auth Setup] Created Login Screen UI");
    }

    private static void WireLoginScreenToUIManager(UIManager uiManager)
    {
        var loginScreen = Object.FindFirstObjectByType<LoginScreen>();
        if (loginScreen == null) return;

        var so = new SerializedObject(uiManager);
        var screensProp = so.FindProperty("m_Screens");

        // Check if already in list
        for (int i = 0; i < screensProp.arraySize; i++)
        {
            var element = screensProp.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == loginScreen)
            {
                Debug.Log("[Web3Auth Setup] LoginScreen already in UIManager.m_Screens");
                return;
            }
        }

        // Add LoginScreen to the list (insert at index 1, after LoadingScreen)
        int insertIndex = Mathf.Min(1, screensProp.arraySize);
        screensProp.InsertArrayElementAtIndex(insertIndex);
        screensProp.GetArrayElementAtIndex(insertIndex).objectReferenceValue = loginScreen;
        so.ApplyModifiedProperties();

        Debug.Log("[Web3Auth Setup] Added LoginScreen to UIManager.m_Screens list");
    }

    private static void SetupStartScreenUserInfo()
    {
        var startScreen = Object.FindFirstObjectByType<StartScreen>();
        if (startScreen == null)
        {
            Debug.LogWarning("[Web3Auth Setup] StartScreen not found, skipping user info setup");
            return;
        }

        var so = new SerializedObject(startScreen);

        // Check if WalletAddressText is already assigned
        if (so.FindProperty("WalletAddressText").objectReferenceValue != null)
        {
            Debug.Log("[Web3Auth Setup] StartScreen user info already configured");
            return;
        }

        var startScreenTransform = startScreen.transform;

        // --- Top-right: [WalletAddress] [Logout] vertically aligned ---

        // Username (hidden, kept for serialized field compatibility)
        var usernameGO = new GameObject("UsernameText");
        usernameGO.transform.SetParent(startScreenTransform, false);
        usernameGO.SetActive(false);
        usernameGO.AddComponent<RectTransform>();
        var usernameText = usernameGO.AddComponent<Text>();
        usernameText.text = "";
        usernameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Logout button (top-right corner) — 80x30, 10px from edges
        var logoutGO = new GameObject("LogoutButton");
        logoutGO.transform.SetParent(startScreenTransform, false);
        logoutGO.SetActive(false);
        var logoutRect = logoutGO.AddComponent<RectTransform>();
        logoutRect.anchorMin = new Vector2(1f, 1f);
        logoutRect.anchorMax = new Vector2(1f, 1f);
        logoutRect.pivot = new Vector2(1f, 1f);
        logoutRect.anchoredPosition = new Vector2(-10f, -10f);
        logoutRect.sizeDelta = new Vector2(80f, 30f);
        var logoutImage = logoutGO.AddComponent<Image>();
        logoutImage.color = new Color(0.22f, 0.01f, 0.23f, 0.9f);
        var logoutButton = logoutGO.AddComponent<Button>();
        var logoutColors = logoutButton.colors;
        logoutColors.highlightedColor = new Color(0.35f, 0.05f, 0.37f);
        logoutColors.pressedColor = new Color(0.15f, 0.01f, 0.16f);
        logoutButton.colors = logoutColors;

        // Logout button label
        var logoutLabelGO = new GameObject("Label");
        logoutLabelGO.transform.SetParent(logoutGO.transform, false);
        var logoutLabelRect = logoutLabelGO.AddComponent<RectTransform>();
        logoutLabelRect.anchorMin = Vector2.zero;
        logoutLabelRect.anchorMax = Vector2.one;
        logoutLabelRect.offsetMin = Vector2.zero;
        logoutLabelRect.offsetMax = Vector2.zero;
        var logoutLabelText = logoutLabelGO.AddComponent<Text>();
        logoutLabelText.text = "Logout";
        logoutLabelText.fontSize = 14;
        logoutLabelText.alignment = TextAnchor.MiddleCenter;
        logoutLabelText.color = Color.white;
        logoutLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Wallet address text — to the LEFT of logout button, vertically centered with it
        // Logout is at x=-10, width=80, so its left edge is at x=-90. Wallet sits to its left with 8px gap.
        var walletGO = new GameObject("WalletAddressText");
        walletGO.transform.SetParent(startScreenTransform, false);
        walletGO.SetActive(false);
        var walletRect = walletGO.AddComponent<RectTransform>();
        walletRect.anchorMin = new Vector2(1f, 1f);
        walletRect.anchorMax = new Vector2(1f, 1f);
        walletRect.pivot = new Vector2(1f, 0.5f);
        // Vertically centered with logout: logout top=-10, height=30, so center is at y=-25
        walletRect.anchoredPosition = new Vector2(-98f, -25f);
        walletRect.sizeDelta = new Vector2(160f, 30f);
        var walletShadow = walletGO.AddComponent<Shadow>();
        walletShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        walletShadow.effectDistance = new Vector2(1f, -1f);
        var walletText = walletGO.AddComponent<Text>();
        walletText.text = "";
        walletText.fontSize = 14;
        walletText.alignment = TextAnchor.MiddleRight;
        walletText.color = new Color(0.9f, 0.85f, 0.92f);
        walletText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Wire references
        so.FindProperty("WalletAddressText").objectReferenceValue = walletText;
        so.FindProperty("UsernameText").objectReferenceValue = usernameText;
        so.FindProperty("LogoutButton").objectReferenceValue = logoutButton;
        so.ApplyModifiedProperties();

        Debug.Log("[Web3Auth Setup] Added user info elements to StartScreen");
    }

    [MenuItem("Tools/Web3Auth/Refresh User Info Layout")]
    public static void RefreshUserInfoLayout()
    {
        var startScreen = Object.FindFirstObjectByType<StartScreen>();
        if (startScreen == null)
        {
            Debug.LogError("[Web3Auth Setup] StartScreen not found");
            return;
        }

        // Clear existing references and destroy old GameObjects
        var so = new SerializedObject(startScreen);
        string[] fields = { "WalletAddressText", "UsernameText", "LogoutButton" };
        foreach (var field in fields)
        {
            var prop = so.FindProperty(field);
            if (prop.objectReferenceValue != null)
            {
                var comp = prop.objectReferenceValue as Component;
                if (comp != null)
                    Undo.DestroyObjectImmediate(comp.gameObject);
                prop.objectReferenceValue = null;
            }
        }
        so.ApplyModifiedProperties();

        // Re-create with updated layout
        SetupStartScreenUserInfo();

        EditorUtility.SetDirty(startScreen);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[Web3Auth Setup] User info layout refreshed! Save the scene.");
    }

    [MenuItem("Tools/Web3Auth/Verify Setup")]
    public static void VerifySetup()
    {
        bool allGood = true;

        var web3Auth = Object.FindFirstObjectByType<Web3AuthManager>();
        if (web3Auth == null) { Debug.LogError("[Verify] Web3AuthManager missing"); allGood = false; }
        else Debug.Log("[Verify] Web3AuthManager: OK");

        var apiManager = Object.FindFirstObjectByType<ApiManager>();
        if (apiManager == null) { Debug.LogError("[Verify] ApiManager missing"); allGood = false; }
        else Debug.Log("[Verify] ApiManager: OK");

        var sessionManager = Object.FindFirstObjectByType<SessionManager>();
        if (sessionManager == null) { Debug.LogError("[Verify] SessionManager missing"); allGood = false; }
        else Debug.Log("[Verify] SessionManager: OK");

        var loginScreen = Object.FindFirstObjectByType<LoginScreen>();
        if (loginScreen == null) { Debug.LogError("[Verify] LoginScreen missing"); allGood = false; }
        else Debug.Log("[Verify] LoginScreen: OK");

        var uiManager = Object.FindFirstObjectByType<UIManager>();
        if (uiManager != null)
        {
            var screen = uiManager.GetUIScreen(UIScreenInfo.LOGIN_SCREEN);
            if (screen == null) { Debug.LogError("[Verify] LOGIN_SCREEN not in UIManager.m_Screens"); allGood = false; }
            else Debug.Log("[Verify] LOGIN_SCREEN in UIManager: OK");
        }

        var startScreen = Object.FindFirstObjectByType<StartScreen>();
        if (startScreen != null)
        {
            var so = new SerializedObject(startScreen);
            if (so.FindProperty("LogoutButton").objectReferenceValue == null)
            { Debug.LogWarning("[Verify] StartScreen.LogoutButton not wired"); allGood = false; }
            else Debug.Log("[Verify] StartScreen user info: OK");
        }

        if (allGood)
            Debug.Log("[Verify] All checks passed! Don't forget to set WebGL template to 'Web3Auth' in Player Settings.");
        else
            Debug.LogWarning("[Verify] Some checks failed. Run Tools > Web3Auth > Setup Scene to fix.");
    }
}
