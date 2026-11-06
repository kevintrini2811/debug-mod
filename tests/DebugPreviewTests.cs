using DebugMod;

static void Near(float actual, float expected, string message)
{
    if (Math.Abs(actual - expected) > .0001f) throw new Exception(message);
}
// Equivalent source artwork with three different pivots must share the same
// native lower-center anchor and visible height after placement.
foreach (var bounds in new[] { (0f, 0f, 3.2f), (0f, -1.6f, 3.2f), (.8f, -2.4f, 3.2f) })
{
    var fit = ZombiePreviewLayout.Fit(bounds.Item1, bounds.Item2, bounds.Item3);
    Near(bounds.Item1 * fit.Scale + fit.X, 0, "lower center X must match native anchor");
    Near(bounds.Item2 * fit.Scale + fit.Y, 0, "foot Y must match native anchor");
    Near(bounds.Item3 * fit.Scale, 310.2f, "native preview height must stay fixed");
}
var native = ZombiePreviewLayout.Fit(0, 0, 3.2f);
Near(native.X, 0, "native foot pivot X remains unchanged");
Near(native.Y, 0, "native foot pivot Y remains unchanged");
foreach (float height in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
{
    try { ZombiePreviewLayout.Fit(0, 0, height); throw new Exception("invalid geometry accepted"); }
    catch (ArgumentOutOfRangeException) { }
}
Console.WriteLine("PASS preview lower-center alignment across native, center and asymmetric pivots; invalid geometry rejected.");
