namespace HomelabBrain.PlantAnalyzer.Domain;

internal abstract record MqttMessageResult
{
    internal sealed record Valid(SensorReading Reading) : MqttMessageResult;
    internal sealed record InvalidPayload(string Topic, string RawPayload) : MqttMessageResult;
    internal sealed record UnknownTopic(string Topic) : MqttMessageResult;
}
