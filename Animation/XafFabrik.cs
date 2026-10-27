using Godot;

namespace Xanadu.Animation;

/// <summary>Small 2D FABRIK solver used by visual IK bone chains.</summary>
public static class XafFabrik
{
    /// <summary>Solve a joint chain in local skeleton space.</summary>
    public static Vector2[] Solve(
        Vector2[] joints,
        Vector2 target,
        bool bendPositive,
        int iterations = 24,
        float tolerance = 0.01f)
    {
        if (joints.Length < 2)
            return joints;

        var result = (Vector2[])joints.Clone();
        var lengths = new float[result.Length - 1];
        float total = 0f;
        for (int i = 0; i < lengths.Length; i++)
        {
            lengths[i] = result[i].DistanceTo(result[i + 1]);
            total += lengths[i];
        }

        Vector2 root = result[0];
        float distance = root.DistanceTo(target);

        if (result.Length == 3)
        {
            float reach = Mathf.Min(distance, total);
            Vector2 direction = distance <= 0.0001f
                ? new Vector2(1, 0)
                : (target - root) / distance;
            Vector2 end = root + direction * reach;
            float first = lengths[0];
            float second = lengths[1];
            float along = (first * first - second * second +
                           reach * reach) / (2f * Mathf.Max(0.0001f, reach));
            float height = Mathf.Sqrt(Mathf.Max(
                0f,
                first * first - along * along));
            Vector2 bend = new(-direction.Y, direction.X);
            result[0] = root;
            result[1] = root + direction * along +
                        bend * height * (bendPositive ? 1f : -1f);
            result[2] = end;
            return result;
        }

        if (distance > total)
        {
            Vector2 direction = (target - root).Normalized();
            for (int i = 1; i < result.Length; i++)
                result[i] = result[i - 1] + direction * lengths[i - 1];
            return result;
        }

        if (distance < 0.0001f)
            return result;

        // Break perfect collinearity so the solver chooses the requested bend.
        Vector2 toTarget = (target - root).Normalized();
        Vector2 perpendicular = new(-toTarget.Y, toTarget.X);
        float sign = bendPositive ? 1f : -1f;
        for (int i = 1; i < result.Length - 1; i++)
        {
            if (Mathf.Abs(perpendicular.Dot(
                    (result[i] - root).Normalized())) < 0.0001f)
            {
                result[i] += perpendicular * sign * 1f;
            }
        }

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            result[^1] = target;
            for (int i = result.Length - 2; i >= 0; i--)
            {
                Vector2 direction =
                    (result[i] - result[i + 1]).Normalized();
                result[i] = result[i + 1] + direction * lengths[i];
            }

            result[0] = root;
            for (int i = 1; i < result.Length; i++)
            {
                Vector2 direction =
                    (result[i] - result[i - 1]).Normalized();
                result[i] = result[i - 1] + direction * lengths[i - 1];
            }

            if (result[^1].DistanceTo(target) <= tolerance)
                break;
        }

        return result;
    }
}
