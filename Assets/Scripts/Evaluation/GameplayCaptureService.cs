using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

/// <summary>
/// Captures a small player-POV screenshot for the first occurrence of each mistake type per step
/// (capped), for the Mistake Gallery and instructor report. Purely observational: it listens to
/// EvaluationLogger and never touches gameplay or scoring.
///
/// Renders through a hidden mono camera that copies the HMD camera pose each capture, so the XR
/// stereo camera is never re-targeted. Uses AsyncGPUReadback where supported (PC Link, Quest Vulkan)
/// and falls back to ReadPixels otherwise.
/// </summary>
public class GameplayCaptureService : MonoBehaviour
{
    public static GameplayCaptureService Instance { get; private set; }

    [Header("Capture Settings")]
    public int captureWidth = 640;
    public int captureHeight = 360;
    [Tooltip("Maximum screenshots per session.")]
    public int maxCaptures = 30;
    [Tooltip("Seconds to wait after the mistake so the red vignette (0.2 s fade-in) / HUD is visible in the shot.")]
    public float delaySeconds = 0.25f;
    [Tooltip("Adds a red edge tint to Severe screenshots so they read as Severe in the gallery / report (image only).")]
    public bool tintSevereCaptures = true;

    /// <summary>Temporary folder used while playing; deleted unless the session is saved.</summary>
    public string TempFolder { get; private set; }

    /// <summary>Permanent folder the session is moved to when saved (persistentDataPath).</summary>
    public string SavedFolder { get; private set; }

    public bool IsPersisted { get; private set; }
    public bool IsDiscarded { get; private set; }

    /// <summary>Folder new captures are currently written to.</summary>
    public string SessionFolder => IsPersisted ? SavedFolder : TempFolder;

    private static string TempRoot => Path.Combine(Application.temporaryCachePath, "SimulationCaptures");

    /// <summary>captureId -> absolute PNG path, for captures that finished writing.</summary>
    public readonly Dictionary<string, string> savedCaptures = new Dictionary<string, string>();

    private struct Request
    {
        public string captureId;
        public float time;
        public bool severe;
    }

    private readonly Queue<Request> pending = new Queue<Request>();
    private readonly HashSet<string> capturedKeys = new HashSet<string>();
    private int captureCount;
    private Camera captureCamera;
    private RenderTexture renderTexture;
    private EvaluationLogger subscribedLogger;

    public static GameplayCaptureService EnsureExists()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("GameplayCaptureService");
        return go.AddComponent<GameplayCaptureService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        CleanupLeftoverTempSessions();
        subscribedLogger = EvaluationLogger.Instance;
        subscribedLogger.OnEventLogged += HandleEventLogged;
        subscribedLogger.OnSessionReset += ResetSession;
        ResetSession(subscribedLogger.SessionId);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (subscribedLogger != null)
        {
            subscribedLogger.OnEventLogged -= HandleEventLogged;
            subscribedLogger.OnSessionReset -= ResetSession;
        }
        if (renderTexture != null) renderTexture.Release();
    }

    private void OnApplicationQuit()
    {
        DiscardCaptures();
    }

    /// <summary>Deletes temp session folders left behind by a crash or an unsaved earlier run.</summary>
    private static void CleanupLeftoverTempSessions()
    {
        try
        {
            if (Directory.Exists(TempRoot)) Directory.Delete(TempRoot, true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameplayCapture] Could not clean temp captures: {ex.Message}");
        }
    }

    /// <summary>Moves this session's captures from the temp folder into the permanent session folder.</summary>
    public void PersistCaptures()
    {
        if (IsPersisted) return;
        try
        {
            Directory.CreateDirectory(SavedFolder);
            var moved = new Dictionary<string, string>();
            foreach (var kv in savedCaptures)
            {
                string dest = Path.Combine(SavedFolder, Path.GetFileName(kv.Value));
                if (File.Exists(kv.Value))
                {
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(kv.Value, dest);
                }
                moved[kv.Key] = dest;
            }
            savedCaptures.Clear();
            foreach (var kv in moved) savedCaptures[kv.Key] = kv.Value;
            IsPersisted = true;   // captures still in flight now write straight to SavedFolder
            if (Directory.Exists(TempFolder)) Directory.Delete(TempFolder, true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameplayCapture] Could not move captures to {SavedFolder}: {ex.Message}");
        }
    }

    /// <summary>Deletes this session's temp captures (no-op once saved).</summary>
    public void DiscardCaptures()
    {
        if (IsPersisted) return;
        IsDiscarded = true;
        pending.Clear();
        try
        {
            if (!string.IsNullOrEmpty(TempFolder) && Directory.Exists(TempFolder)) Directory.Delete(TempFolder, true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameplayCapture] Could not delete temp captures: {ex.Message}");
        }
        savedCaptures.Clear();
    }

    public void ResetSession(string sessionId)
    {
        DiscardCaptures();   // previous unsaved session in this run
        IsPersisted = false;
        IsDiscarded = false;
        pending.Clear();
        capturedKeys.Clear();
        savedCaptures.Clear();
        captureCount = 0;
        TempFolder = Path.Combine(TempRoot, sessionId);
        SavedFolder = Path.Combine(Application.persistentDataPath, "SimulationCaptures", sessionId);
    }

    private void HandleEventLogged(EvaluationEvent e)
    {
        if (e.isCorrect || IsDiscarded || captureCount >= maxCaptures) return;
        if (!capturedKeys.Add($"{e.step}|{e.GroupKey}")) return;   // first occurrence per type per step only

        captureCount++;
        string type = string.IsNullOrEmpty(e.violationType) ? "Unclassified" : e.violationType;
        e.captureId = $"cap_{captureCount:00}_step{e.step}_{type}";
        pending.Enqueue(new Request
        {
            captureId = e.captureId,
            time = Time.time + Mathf.Max(0f, delaySeconds),
            severe = e.severity == MistakeSeverity.Severe
        });
    }

    private void LateUpdate()
    {
        // One capture per frame at most, to keep frame time stable in the headset.
        if (pending.Count == 0 || pending.Peek().time > Time.time) return;
        var request = pending.Dequeue();
        try
        {
            Capture(request.captureId, request.severe);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameplayCapture] Capture '{request.captureId}' failed: {ex.Message}");
        }
    }

    private void Capture(string captureId, bool severe)
    {
        Camera source = Camera.main;
        if (source == null) return;

        EnsureCaptureResources(source);
        captureCamera.CopyFrom(source);
        captureCamera.stereoTargetEye = StereoTargetEyeMask.None;
        captureCamera.targetTexture = renderTexture;
        captureCamera.aspect = (float)captureWidth / captureHeight;
        captureCamera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        captureCamera.enabled = false;
        captureCamera.Render();

        if (SystemInfo.supportsAsyncGPUReadback)
        {
            AsyncGPUReadback.Request(renderTexture, 0, TextureFormat.RGBA32, req =>
            {
                if (req.hasError)
                {
                    Debug.LogWarning($"[GameplayCapture] GPU readback failed for '{captureId}'.");
                    return;
                }
                WritePng(captureId, req.GetData<byte>(), severe);
            });
        }
        else
        {
            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var tex = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            tex.Apply(false);
            RenderTexture.active = previous;
            WritePng(captureId, tex.GetRawTextureData<byte>(), severe);
            Destroy(tex);
        }
    }

    private void WritePng(string captureId, NativeArray<byte> rgba, bool severe)
    {
        if (IsDiscarded) return;
        try
        {
            byte[] pixels = rgba.ToArray();
            if (severe && tintSevereCaptures) ApplyRedEdgeTint(pixels, captureWidth, captureHeight);
            byte[] png = ImageConversion.EncodeArrayToPNG(pixels, GraphicsFormat.R8G8B8A8_SRGB, (uint)captureWidth, (uint)captureHeight);
            Directory.CreateDirectory(SessionFolder);
            string path = Path.Combine(SessionFolder, captureId + ".png");
            File.WriteAllBytes(path, png);
            savedCaptures[captureId] = path;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameplayCapture] Could not save '{captureId}': {ex.Message}");
        }
    }

    /// <summary>Radial red tint toward the image edges, matching the in-headset Severe vignette colour.</summary>
    private static void ApplyRedEdgeTint(byte[] rgba, int width, int height)
    {
        const float r = 242f, g = 13f, b = 13f, maxAlpha = 0.75f;
        for (int y = 0; y < height; y++)
        {
            float ny = (y + 0.5f) / height * 2f - 1f;
            for (int x = 0; x < width; x++)
            {
                float nx = (x + 0.5f) / width * 2f - 1f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);           // 0 centre, 1 at edge midpoints
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, d)) * maxAlpha;
                if (a <= 0f) continue;
                int i = (y * width + x) * 4;
                rgba[i] = (byte)(rgba[i] + (r - rgba[i]) * a);
                rgba[i + 1] = (byte)(rgba[i + 1] + (g - rgba[i + 1]) * a);
                rgba[i + 2] = (byte)(rgba[i + 2] + (b - rgba[i + 2]) * a);
            }
        }
    }

    private void EnsureCaptureResources(Camera source)
    {
        if (renderTexture == null || renderTexture.width != captureWidth || renderTexture.height != captureHeight)
        {
            if (renderTexture != null) renderTexture.Release();
            renderTexture = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32) { name = "GameplayCaptureRT" };
            renderTexture.Create();
        }
        if (captureCamera == null)
        {
            var go = new GameObject("GameplayCaptureCamera");
            go.transform.SetParent(transform, false);
            captureCamera = go.AddComponent<Camera>();
            captureCamera.enabled = false;
        }
    }
}
