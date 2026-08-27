namespace Api.Authorization;

/// <summary>Roles suportadas pelo contrato de autorização.</summary>
public static class ApiRoleNames
{
    public const string Superuser = "superuser";
    public const string Trainer = "trainer";
    public const string Client = "client";

    public static bool IsSupported(string role) =>
        role is Superuser or Trainer or Client;
}
