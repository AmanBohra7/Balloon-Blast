using UnityEngine;

public class BalloonFloating : MonoBehaviour
{
    public float riseForceMin = 0.1f;
    public float riseForceMax = 0.3f;
    float riseForce = 2f;          // Upward lift force
    public float driftStrength = 0.5f;    // Sideways movement strength
    public float turbulenceSpeed = 1f;    // Speed of noise-based turbulence
    public float turbulenceAmount = 0.2f; // Strength of turbulence
    public float tiltAmount = 5f;         // How much the balloon tilts
    public float rotationDamping = 2f;    // Smooth rotation effect

    private Rigidbody rb;
    private Vector3 lastVelocity;

    private float noiseOffsetX, noiseOffsetZ;

    void Start()
    {
        riseForce = Random.Range(riseForceMin, riseForceMax);
        Random.InitState(System.Environment.TickCount + GetInstanceID());
        noiseOffsetX = Random.Range(0f, 1000f); // Unique offset for each balloon
        noiseOffsetZ = Random.Range(0f, 1000f);
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;  // Disable gravity so it floats
        rb.linearDamping = 1.5f;         // Adds air resistance for a slow floating feel
        rb.angularDamping = 1f;    // Makes rotation settle naturally
        //rb.rotation = Quaternion.identity;
    }

    
    float randomness = 1;
    private void Update()
    {
        randomness = Random.Range(0.1f, 5.5f);
    }

    void FixedUpdate()
    {
        

        // Apply upward force
        rb.AddForce(Vector3.up * riseForce * randomness, ForceMode.Acceleration);


        // Add Perlin noise for random drifting
        float noiseX = (Mathf.PerlinNoise(Time.time * turbulenceSpeed + noiseOffsetX, 0) - 0.5f) * 2f;
        float noiseZ = (Mathf.PerlinNoise(0, Time.time * turbulenceSpeed + noiseOffsetZ) - 0.5f) * 2f;

        // Calculate drift force
        Vector3 driftForce = new Vector3(
            Mathf.Sin(Time.time) * driftStrength + noiseX * turbulenceAmount,
            0,
            Mathf.Cos(Time.time * 1.5f) * driftStrength + noiseZ * turbulenceAmount
        );


        Vector3 randomDrift = Random.insideUnitSphere * driftStrength;
        rb.AddForce(randomDrift, ForceMode.Acceleration);

        // Calculate tilt based on movement direction
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
            targetRotation *= Quaternion.Euler(tiltAmount, 0, 0); // Tilt forward slightly

            // Smoothly rotate towards movement
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * rotationDamping));
        }

        lastVelocity = rb.linearVelocity; // Store for next frame
    }
}
