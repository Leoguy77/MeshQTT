namespace MeshQTT;

public class Config
{
  /// <summary>
  ///     Gets or sets the port.
  /// </summary>
  public int Port { get; set; }

  /// <summary>
  ///     Gets or sets the list of valid users.
  /// </summary>
  public List<User> Users { get; set; } = [];

  /// <summary>
  /// Gets or sets the list of encryption keys.
  /// </summary>
  public List<string> EncryptionKeys { get; set; } = [];
}