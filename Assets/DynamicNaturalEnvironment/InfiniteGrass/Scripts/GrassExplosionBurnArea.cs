using UnityEngine;

public class GrassExplosionBurnArea : MonoBehaviour
{
    [Header("References")]
    public Renderer burnRenderer;

    [Header("Fire VFX")]
    public ParticleSystem flameParticle;
    public ParticleSystem smokeParticle;

    [Header("Burn Area")]
    public float burnRadius = 3f;
    public float burnDuration = 1.5f;
    public float burnStrength = 1f;

    [Header("Play")]
    public bool playOnEnable = true;
    public bool destroyAfterPlay = false;

    private Material runtimeMaterial;
    private float elapsedTime;
    private bool isPlaying;

    private static readonly int StrengthID = Shader.PropertyToID("_Strength");

    private void Awake()
    {
        if (burnRenderer == null)
        {
            burnRenderer = GetComponent<Renderer>();
        }

        if (burnRenderer != null)
        {
            runtimeMaterial = burnRenderer.material;
            burnRenderer.enabled = false;
        }

        StopParticleImmediately(flameParticle);
        StopParticleImmediately(smokeParticle);
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        elapsedTime += Time.deltaTime;

        if (elapsedTime >= burnDuration)
        {
            Stop();

            if (destroyAfterPlay)
            {
                Destroy(gameObject);
            }
        }
    }

    public void Play()
    {
        elapsedTime = 0f;
        isPlaying = true;

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        transform.localScale = new Vector3(burnRadius * 2f, burnRadius * 2f, 1f);

        if (burnRenderer != null)
        {
            burnRenderer.enabled = true;
        }

        if (runtimeMaterial != null)
        {
            runtimeMaterial.SetFloat(StrengthID, burnStrength);
        }

        PlayParticle(flameParticle);
        PlayParticle(smokeParticle);
    }

    public void Stop()
    {
        isPlaying = false;

        if (runtimeMaterial != null)
        {
            runtimeMaterial.SetFloat(StrengthID, 0f);
        }

        if (burnRenderer != null)
        {
            burnRenderer.enabled = false;
        }

        StopParticleEmission(flameParticle);
        StopParticleEmission(smokeParticle);
    }

    private void PlayParticle(ParticleSystem particle)
    {
        if (particle == null)
        {
            return;
        }

        ParticleSystem.ShapeModule shape = particle.shape;

        if (shape.enabled)
        {
            shape.radius = burnRadius;
        }

        particle.Clear(true);
        particle.Play(true);
    }

    private void StopParticleEmission(ParticleSystem particle)
    {
        if (particle == null)
        {
            return;
        }

        particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void StopParticleImmediately(ParticleSystem particle)
    {
        if (particle == null)
        {
            return;
        }

        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable()
    {
        isPlaying = false;

        if (burnRenderer != null)
        {
            burnRenderer.enabled = false;
        }

        StopParticleImmediately(flameParticle);
        StopParticleImmediately(smokeParticle);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }
}