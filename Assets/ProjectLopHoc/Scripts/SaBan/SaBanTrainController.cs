using UnityEngine;
using UnityEngine.Splines;

[DefaultExecutionOrder(-100)]
public class SaBanTrainController : MonoBehaviour
{
    public TrainSplineMover train;
    public SplineContainer route;
    public Transform visual;
    public Terrain terrain;
    public float heightOffset = .018f;

    float routeLength;
    float previousDistance;
    float currentDistance;
    const int ArcSamples = 8192;
    float[] arcDistances;
    float[] arcParameters;

    void Start()
    {
        if (!train || !route || !visual) return;
        train.enabled = false;
        BuildArcLengthTable();
        routeLength = arcDistances != null && arcDistances.Length > 0 ? arcDistances[arcDistances.Length - 1] : 0f;
        train.totalRouteLength = routeLength;
        train.carSpacing = 0f;
        train.startDistance = routeLength * .08f;
        train.currentDistanceMeters = train.startDistance;
        train.currentSpeedKmh = 0f;
        train.targetSpeedKmh = 0f;
        previousDistance = currentDistance = train.currentDistanceMeters;
        PlaceAtDistance(currentDistance);
    }

    void FixedUpdate()
    {
        if (!train || !route || !visual || routeLength <= 0f) return;
        previousDistance = train.currentDistanceMeters;
        float rate = train.targetSpeedKmh > train.currentSpeedKmh ? train.accelerationRate : train.brakeRate;
        train.currentSpeedKmh = Mathf.MoveTowards(train.currentSpeedKmh, train.targetSpeedKmh, rate * Time.deltaTime);
        train.currentDistanceMeters = Mathf.Clamp(train.currentDistanceMeters + train.currentSpeedKmh / 3.6f * Time.deltaTime, 0f, routeLength);
        currentDistance = train.currentDistanceMeters;
    }

    void LateUpdate()
    {
        if (!train || !route || !visual || routeLength <= 0f) return;
        // Render between fixed-step positions so a variable frame rate does not make
        // the miniature train visibly hop from one physics tick to the next.
        float alpha = Time.fixedDeltaTime > 0f
            ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime)
            : 1f;
        PlaceAtDistance(Mathf.Lerp(previousDistance, currentDistance, alpha));
    }

    public void RefreshPlacement()
    {
        if (!route || !visual || routeLength <= 0f) return;
        previousDistance = currentDistance = train ? train.currentDistanceMeters : currentDistance;
        PlaceAtDistance(currentDistance);
    }

    void PlaceAtDistance(float distance)
    {
        float t = ParameterAtDistance(distance);
        Vector3 point = RoutePosition(t);
        Vector3 tangent = RouteTangent(t);
        Vector3 up = RouteUp(t);
        visual.position = point + up * heightOffset;
        if (tangent.sqrMagnitude > .0001f) visual.rotation = Quaternion.LookRotation(tangent, up);
    }

    void BuildArcLengthTable()
    {
        if (!route || route.Splines == null || route.Splines.Count == 0)
        {
            arcDistances = null;
            arcParameters = null;
            return;
        }

        arcDistances = new float[ArcSamples + 1];
        arcParameters = new float[ArcSamples + 1];
        Vector3 previous = RoutePosition(0f);
        for (int i = 1; i <= ArcSamples; i++)
        {
            float t = (float)i / ArcSamples;
            Vector3 point = RoutePosition(t);
            arcDistances[i] = arcDistances[i - 1] + Vector3.Distance(previous, point);
            arcParameters[i] = t;
            previous = point;
        }
    }

    float ParameterAtDistance(float distance)
    {
        if (arcDistances == null || arcParameters == null || arcDistances.Length < 2) return 0f;
        distance = Mathf.Clamp(distance, 0f, routeLength);
        int low = 0, high = arcDistances.Length - 1;
        while (low + 1 < high)
        {
            int mid = (low + high) >> 1;
            if (arcDistances[mid] < distance) low = mid;
            else high = mid;
        }
        float span = arcDistances[high] - arcDistances[low];
        float blend = span > 0.000001f ? (distance - arcDistances[low]) / span : 0f;
        return Mathf.Lerp(arcParameters[low], arcParameters[high], blend);
    }

    Vector3 RoutePosition(float t)
    {
        if (!route || route.Splines == null || route.Splines.Count == 0) return transform.position;
        return route.transform.TransformPoint(SplineUtility.EvaluatePosition(route.Splines[0], t));
    }

    Vector3 RouteTangent(float t)
    {
        if (!route || route.Splines == null || route.Splines.Count == 0) return transform.forward;
        Vector3 tangent = route.transform.TransformVector(SplineUtility.EvaluateTangent(route.Splines[0], t));
        tangent.Normalize();
        return tangent;
    }

    Vector3 RouteUp(float t)
    {
        if (!route || route.Splines == null || route.Splines.Count == 0) return transform.up;
        Vector3 up = route.transform.TransformVector(SplineUtility.EvaluateUpVector(route.Splines[0], t));
        up.Normalize();
        return up;
    }
}
