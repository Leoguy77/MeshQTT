using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MeshQTT.Managers
{
    public static class CertificateManager
    {
        /// <summary>
        /// Loads or generates a certificate for TLS.
        /// </summary>
        /// <param name="certificatePath">Path to certificate file (.crt or .pem)</param>
        /// <param name="privateKeyPath">Path to private key file (.key or .pem)</param>
        /// <param name="password">Certificate password (optional)</param>
        /// <returns>X509Certificate2 for TLS</returns>
        public static X509Certificate2 GetOrCreateCertificate(
            string? certificatePath,
            string? privateKeyPath,
            string? password = null
        )
        {
            // Try to load existing certificate first
            if (!string.IsNullOrEmpty(certificatePath) && File.Exists(certificatePath))
            {
                try
                {
                    return LoadCertificate(certificatePath, privateKeyPath, password);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to load certificate from {certificatePath}: {ex.Message}");
                    Logger.Log("Falling back to self-signed certificate generation...");
                }
            }

            // Generate self-signed certificate
            Logger.Log("Generating self-signed certificate for TLS...");
            return GenerateSelfSignedCertificate();
        }

        private static X509Certificate2 LoadCertificate(
            string certificatePath,
            string? privateKeyPath,
            string? password
        )
        {
            var certExtension = Path.GetExtension(certificatePath).ToLowerInvariant();

            switch (certExtension)
            {
                case ".pem":
                    return LoadPemCertificate(certificatePath, privateKeyPath, password);
                case ".crt":
                    return LoadCrtCertificate(certificatePath, privateKeyPath, password);
                case ".pfx":
                case ".p12":
                    return new X509Certificate2(certificatePath, password);
                default:
                    throw new NotSupportedException(
                        $"Certificate format {certExtension} is not supported"
                    );
            }
        }

        private static X509Certificate2 LoadPemCertificate(
            string certificatePath,
            string? privateKeyPath,
            string? password
        )
        {
            var certText = File.ReadAllText(certificatePath);
            var cert = X509Certificate2.CreateFromPem(certText);

            if (!string.IsNullOrEmpty(privateKeyPath) && File.Exists(privateKeyPath))
            {
                var keyText = File.ReadAllText(privateKeyPath);

                // Try different key formats
                try
                {
                    using var rsa = RSA.Create();
                    rsa.ImportFromPem(keyText);
                    cert = cert.CopyWithPrivateKey(rsa);
                }
                catch
                {
                    try
                    {
                        using var ecdsa = ECDsa.Create();
                        ecdsa.ImportFromPem(keyText);
                        cert = cert.CopyWithPrivateKey(ecdsa);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to load private key: {ex.Message}"
                        );
                    }
                }
            }

            // Verify certificate has private key
            if (!cert.HasPrivateKey)
            {
                throw new InvalidOperationException(
                    "Certificate does not have a private key required for TLS"
                );
            }

            return cert;
        }

        private static X509Certificate2 LoadCrtCertificate(
            string certificatePath,
            string? privateKeyPath,
            string? password
        )
        {
            var cert = new X509Certificate2(certificatePath);

            if (!string.IsNullOrEmpty(privateKeyPath) && File.Exists(privateKeyPath))
            {
                var keyText = File.ReadAllText(privateKeyPath);

                try
                {
                    using var rsa = RSA.Create();
                    rsa.ImportFromPem(keyText);
                    cert = cert.CopyWithPrivateKey(rsa);
                }
                catch
                {
                    try
                    {
                        using var ecdsa = ECDsa.Create();
                        ecdsa.ImportFromPem(keyText);
                        cert = cert.CopyWithPrivateKey(ecdsa);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to load private key: {ex.Message}"
                        );
                    }
                }
            }

            // Verify certificate has private key
            if (!cert.HasPrivateKey)
            {
                throw new InvalidOperationException(
                    "Certificate does not have a private key required for TLS"
                );
            }

            return cert;
        }

        private static X509Certificate2 GenerateSelfSignedCertificate()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=MeshQTT Server",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            );

            // Add extensions
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false)
            );

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    false
                )
            );

            // Add Subject Alternative Names
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            sanBuilder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
            request.CertificateExtensions.Add(sanBuilder.Build());

            // Create certificate valid for 1 year
            var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(1)
            );

            // Ensure certificate has private key for TLS
            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException(
                    "Generated certificate does not have a private key"
                );
            }

            // Save certificate to certs directory
            var certsDir = Path.Combine(AppContext.BaseDirectory, "certs");
            Directory.CreateDirectory(certsDir);

            var certPath = Path.Combine(certsDir, "server.crt");
            var keyPath = Path.Combine(certsDir, "server.key");
            var pfxPath = Path.Combine(certsDir, "server.pfx");

            // Export certificate and key
            File.WriteAllText(certPath, certificate.ExportCertificatePem());
            var privateKey = certificate.GetRSAPrivateKey();
            if (privateKey != null)
            {
                File.WriteAllText(keyPath, privateKey.ExportRSAPrivateKeyPem());
            }

            // Also export as PFX for easier loading
            var pfxBytes = certificate.Export(X509ContentType.Pfx, "");
            File.WriteAllBytes(pfxPath, pfxBytes);

            Logger.Log($"Self-signed certificate generated and saved to {certPath}");
            Logger.Log($"Private key saved to {keyPath}");
            Logger.Log($"PFX file saved to {pfxPath}");

            return certificate;
        }
    }
}
