namespace Api.Authorization;

/// <summary>Nomes estáveis das policies usadas pelos controllers.</summary>
public static class ApiPolicyNames
{
    public const string Authenticated = "authenticated";
    public const string Trainer = "trainer";
    public const string Client = "client";
    public const string Superuser = "superuser";
    public const string AdministrativeContext = "administrative_context";
}
