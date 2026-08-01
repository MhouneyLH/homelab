namespace HomelabBrain.DeviceConfig.Domain;

// IParsable lets minimal API bind the "{deviceId}" route segment straight to
// this type instead of a raw string.
internal readonly record struct DeviceId(string Value) : IParsable<DeviceId> {
    public static DeviceId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out DeviceId result) {
        result = new DeviceId(s ?? string.Empty);
        return !string.IsNullOrWhiteSpace(s);
    }

    public override string ToString() => Value;
}
