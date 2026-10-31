using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Infrastructure.Security;

public sealed class SigningKeyOptions
{
    public const string SectionName = "Jwt:SigningKey";

    [Required]
    public string Path { get; set; } = "keys/signing-key.pem";

    public List<string> PreviousKeyPaths { get; set; } = [];

    public bool GenerateIfMissing { get; set; }
}

public sealed class SigningKeyProvider
{
    private SigningKeyProvider(RsaSecurityKey current, IReadOnlyList<RsaSecurityKey> all)
    {
        SigningCredentials = new SigningCredentials(current, SecurityAlgorithms.RsaSha256);
        ValidationKeys = all;
        Jwks = new
        {
            keys = all.Select(key =>
            {
                var parameters = key.Rsa.ExportParameters(includePrivateParameters: false);
                return new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = SecurityAlgorithms.RsaSha256,
                    kid = key.KeyId,
                    n = Base64UrlEncoder.Encode(parameters.Modulus),
                    e = Base64UrlEncoder.Encode(parameters.Exponent)
                };
            }).ToArray()
        };
    }

    public SigningCredentials SigningCredentials { get; }

    public IReadOnlyList<RsaSecurityKey> ValidationKeys { get; }

    public object Jwks { get; }

    public static SigningKeyProvider Load(SigningKeyOptions options, string contentRootPath)
    {
        var currentPath = Resolve(options.Path, contentRootPath);
        RSA current;
        if (File.Exists(currentPath))
        {
            current = ReadKey(currentPath);
        }
        else if (options.GenerateIfMissing)
        {
            current = RSA.Create(2048);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(currentPath)!);
            File.WriteAllText(currentPath, current.ExportPkcs8PrivateKeyPem());
        }
        else
        {
            throw new InvalidOperationException(
                $"JWT signing key not found at '{currentPath}'. Provide a PKCS#8 RSA key (PEM) or enable Jwt:SigningKey:GenerateIfMissing in development.");
        }

        var keys = new[] { current }
            .Concat(options.PreviousKeyPaths.Select(path => ReadKey(Resolve(path, contentRootPath))))
            .Select(ToSecurityKey)
            .DistinctBy(key => key.KeyId)
            .ToList();

        return new SigningKeyProvider(keys[0], keys);
    }

    private static RsaSecurityKey ToSecurityKey(RSA rsa)
    {
        var publicJwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(rsa.ExportParameters(false)));
        return new RsaSecurityKey(rsa) { KeyId = Base64UrlEncoder.Encode(publicJwk.ComputeJwkThumbprint()) };
    }

    private static RSA ReadKey(string path)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(path));
        return rsa;
    }

    private static string Resolve(string path, string contentRootPath) =>
        System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(contentRootPath, path);
}
