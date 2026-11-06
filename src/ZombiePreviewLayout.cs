namespace DebugMod;

// Align the sprite's lower center to the native preview anchor, regardless of
// its registry-defined pivot. Native foot-pivot portraits retain their layout.
internal static class ZombiePreviewLayout
{
    internal static (float Scale, float X, float Y) Fit(float centerX, float bottom, float height)
    {
        if (!float.IsFinite(height) || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        float scale = 110f * 2.82f / height;
        return (scale, -centerX * scale, -bottom * scale);
    }
}
