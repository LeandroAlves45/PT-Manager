namespace Api.Middlewares;

/// <summary>Representa claims autenticadas ausentes, duplicadas ou incoerentes.</summary>
public sealed class InvalidAuthenticatedPrincipalException : Exception
{
    public InvalidAuthenticatedPrincipalException()
        : base("The authenticated principal is invalid.")
    {
    }
}
