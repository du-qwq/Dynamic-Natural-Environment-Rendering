using UnityEngine;

public class GrassExplosionShockwave : MonoBehaviour
{
    [Header("References")]
    public Renderer shockwaveRenderer;

    [Header("Shockwave")]
    public float startRadius = 0.2f;
    public float endRadius = 8f;
    public float duration = 0.8f;
    public float maxStrength = 1f;
    public AnimationCurve radiusCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve strengthCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Play")]
    public bool playOnEnable = true;
    public bool destroyAfterPlay = false;

    private Material runtimeMaterial;
    private float elapsedTime;
    private bool isPlaying;

    private static readonly int StrengthID = Shader.PropertyToID("_Strength");

    private void Awake()
    {
        CacheReferences();
        SetRendererEnabled(false);
    }

    private void OnEnable()
    {
        CacheReferences();

        if (!Application.isPlaying)
        {
            SetRendererEnabled(false);
            return;
        }

        if (playOnEnable && !isPlaying)
        {
            Play();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || !isPlaying) return;

        elapsedTime += Time.deltaTime;

        float safeDuration = Mathf.Max(duration, 0.0001f);
        float time01 = Mathf.Clamp01(elapsedTime / safeDuration);
        float radius = Mathf.Lerp(startRadius, endRadius, radiusCurve.Evaluate(time01));
        float strength = maxStrength * strengthCurve.Evaluate(time01);

        transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        if (runtimeMaterial != null) runtimeMaterial.SetFloat(StrengthID, strength);

        if (time01 >= 1f)
        {
            Stop();

            if (destroyAfterPlay) Destroy(gameObject);
        }
    }

    public void Play()
    {
        CacheReferences();

        elapsedTime = 0f;
        isPlaying = true;

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        transform.localScale = new Vector3(startRadius * 2f, startRadius * 2f, 1f);

        if (runtimeMaterial != null) runtimeMaterial.SetFloat(StrengthID, maxStrength);

        SetRendererEnabled(true);
    }

    public void Stop()
    {
        isPlaying = false;

        if (runtimeMaterial != null) runtimeMaterial.SetFloat(StrengthID, 0f);

        SetRendererEnabled(false);
    }

    private void CacheReferences()
    {
        if (shockwaveRenderer == null) shockwaveRenderer = GetComponent<Renderer>();

        if (Application.isPlaying && shockwaveRenderer != null && runtimeMaterial == null)
        {
            runtimeMaterial = shockwaveRenderer.material;
        }
    }

    private void SetRendererEnabled(bool enabledState)
    {
        if (shockwaveRenderer != null) shockwaveRenderer.enabled = enabledState;
    }

    private void OnDisable()
    {
        if (Application.isPlaying) Stop();
    }

    private void OnValidate()
    {
        startRadius = Mathf.Max(0f, startRadius);
        endRadius = Mathf.Max(startRadius, endRadius);
        duration = Mathf.Max(0.0001f, duration);
        maxStrength = Mathf.Clamp01(maxStrength);

        if (!Application.isPlaying)
        {
            if (shockwaveRenderer == null) shockwaveRenderer = GetComponent<Renderer>();
            SetRendererEnabled(false);
        }
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && runtimeMaterial != null) Destroy(runtimeMaterial);
    }
}
