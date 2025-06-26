using MeshQTT.Entities;

namespace MeshQTT.Managers.Notifications
{
    public interface INotificationProvider
    {
        string ProviderType { get; }
        Task<bool> SendNotificationAsync(AlertEvent alertEvent, Dictionary<string, string> config);
        bool ValidateConfig(Dictionary<string, string> config);
    }
}
