using UnityEngine;

public class CharacterWaterSplashController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform characterRoot;
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private CharacterController targetCharacterController;

    [Header("Particles")]
    [SerializeField] private ParticleSystem splashLeft;
    [SerializeField] private ParticleSystem splashRight;

    [Header("Water")]
    [SerializeField] private float waterHeight = 0f;
    [SerializeField] private float activeDepthThreshold = 0.15f; //角色低于水面多少才激活

    [Header("Speed")]
    [SerializeField] private float minMoveSpeed = 0.2f;
    [SerializeField] private float maxMoveSpeed = 4f;

    [Header("Emission")]
    [SerializeField] private float minEmissionRate = 0f;
    [SerializeField] private float maxEmissionRate = 45f;

    [Header("Particle Size")]
    [SerializeField] private float minParticleSize = 0.08f;
    [SerializeField] private float maxParticleSize = 0.18f;

    private Vector3 lastPosition;

    private void Start()
    {
        if (characterRoot == null)
            characterRoot = transform;

        lastPosition = characterRoot.position;
    }

    private void Update()
    {
        float speed = GetCurrentSpeed();
        bool inWater = IsInWater();

        UpdateParticle(splashLeft, speed, inWater, true);
        UpdateParticle(splashRight, speed, inWater, false);

        lastPosition = characterRoot.position;
    }

    private float GetCurrentSpeed()
    {
        if (targetRigidbody != null)
        {
            Vector3 v = targetRigidbody.velocity;
            v.y = 0f;
            return v.magnitude;
        }

        if (targetCharacterController != null)
        {
            Vector3 v = targetCharacterController.velocity;
            v.y = 0f;
            return v.magnitude;
        }

        Vector3 delta = characterRoot.position - lastPosition;
        delta.y = 0f;
        return delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
    }

    private bool IsInWater()
    {
        return characterRoot.position.y < waterHeight + activeDepthThreshold;
    }

    private void UpdateParticle(ParticleSystem ps, float speed, bool inWater, bool isLeft)
    {
        if (ps == null) return;

        var emission = ps.emission;
        var main = ps.main;
        var shape = ps.shape;

        if (!inWater || speed < minMoveSpeed)
        {
            emission.rateOverTime = 0f;
            return;
        }

        float t = Mathf.InverseLerp(minMoveSpeed, maxMoveSpeed, speed);

        emission.rateOverTime = Mathf.Lerp(minEmissionRate, maxEmissionRate, t);
        main.startSize = Mathf.Lerp(minParticleSize, maxParticleSize, t);

        //左右两边不同方向
        Vector3 dir = isLeft ? -characterRoot.right : characterRoot.right;
        Vector3 finalDir = (dir + Vector3.up * 0.8f).normalized;

        //Box Shape的rotation控制喷射朝向
        Vector3 localDir = ps.transform.InverseTransformDirection(finalDir);
        Vector3 euler = Quaternion.LookRotation(localDir).eulerAngles;
        shape.rotation = euler;
    }
}