using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Universal VR Scene Transition Fader.
/// Renders a camera-parented black overlay that fades in (black -> clear) on scene start
/// and fades out (clear -> black) before loading target scenes, ensuring zero jarring cuts in VR.
/// </summary>
public class VRSceneTransitionFader : MonoBehaviour
{
    public static VRSceneTransitionFader Instance { get; private set; }

    [Header("Fade Settings")]
    public float defaultFadeDuration = 0.65f;
    public Color fadeColor = Color.black;

    private Canvas fadeCanvas;
    private CanvasGroup canvasGroup;
    private Image fadeImage;
    private Camera targetCamera;
    private Coroutine activeFadeCo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoEnsureInstance()
    {
        if (Instance == null)
        {
            var go = new GameObject("VRSceneTransitionFader");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<VRSceneTransitionFader>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureCanvasExists();
    }

    private void Start()
    {
        // Fade in from black on initial scene load
        FadeIn();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureCanvasExists();
        FadeIn();
    }

    private void EnsureCanvasExists()
    {
        targetCamera = Camera.main;

        if (fadeCanvas == null)
        {
            var canvasObj = new GameObject("VR_Fade_Overlay");
            canvasObj.transform.SetParent(transform, false);

            fadeCanvas = canvasObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.WorldSpace;
            fadeCanvas.sortingOrder = 32767;

            canvasGroup = canvasObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f; // start black
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var rt = canvasObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(3000f, 3000f);
            rt.localScale = Vector3.one * 0.001f;

            var imgObj = new GameObject("FadeImage");
            imgObj.transform.SetParent(canvasObj.transform, false);

            var imgRt = imgObj.AddComponent<RectTransform>();
            imgRt.anchorMin = Vector2.zero;
            imgRt.anchorMax = Vector2.one;
            imgRt.sizeDelta = Vector2.zero;

            fadeImage = imgObj.AddComponent<Image>();
            fadeImage.color = fadeColor;
            fadeImage.raycastTarget = false;

            var shader = Shader.Find("UI/NoZTest");
            if (shader != null)
            {
                fadeImage.material = new Material(shader);
            }
        }

        AttachToCamera();
    }

    private void AttachToCamera()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null && fadeCanvas != null)
        {
            fadeCanvas.transform.SetParent(targetCamera.transform, false);
            fadeCanvas.transform.localPosition = new Vector3(0f, 0f, 0.10f);
            fadeCanvas.transform.localRotation = Quaternion.identity;
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null && fadeCanvas != null && fadeCanvas.transform.parent != targetCamera.transform)
        {
            AttachToCamera();
        }
    }

    public static void FadeToScene(string sceneName, float duration = 0.65f)
    {
        if (Instance == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        Instance.StartFadeAndLoad(sceneName, duration);
    }

    public void StartFadeAndLoad(string sceneName, float duration)
    {
        if (activeFadeCo != null) StopCoroutine(activeFadeCo);
        activeFadeCo = StartCoroutine(FadeAndLoadRoutine(sceneName, duration));
    }

    public void FadeIn(float duration = 0.65f)
    {
        if (activeFadeCo != null) StopCoroutine(activeFadeCo);
        activeFadeCo = StartCoroutine(FadeRoutine(1f, 0f, duration, null));
    }

    private IEnumerator FadeAndLoadRoutine(string sceneName, float duration)
    {
        yield return FadeRoutine(0f, 1f, duration, null);
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration, Action onComplete)
    {
        EnsureCanvasExists();
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = toAlpha;
        onComplete?.Invoke();
        activeFadeCo = null;
    }
}
