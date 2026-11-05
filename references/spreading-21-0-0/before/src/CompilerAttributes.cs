namespace System.Runtime.CompilerServices;

// The game's stripped interop metadata has no usable NullableAttribute ctor.
[AttributeUsage(AttributeTargets.All)]
internal sealed class NullableAttribute : Attribute
{
    public NullableAttribute(byte value) { }
    public NullableAttribute(byte[] values) { }
}
