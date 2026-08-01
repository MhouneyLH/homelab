namespace HomelabBrain.DeviceConfig.Domain;

// Validated (see SetPlantIdEndpoint) then wrapped, so it can't be swapped
// with a DeviceId at a call site by accident - both are otherwise plain
// strings with no compiler-visible distinction.
internal readonly record struct PlantId(string Value) {
    public override string ToString() => Value;
}
