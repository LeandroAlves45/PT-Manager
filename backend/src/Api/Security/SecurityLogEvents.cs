namespace Api.Security;

/// <summary>Identificadores estáveis dos eventos de segurança observáveis na API.</summary>
public static class SecurityLogEvents
{
    public static readonly EventId Login = new(2001, nameof(Login));
    public static readonly EventId SignUp = new(2002, nameof(SignUp));
    public static readonly EventId Lockout = new(2003, nameof(Lockout));
    public static readonly EventId Logout = new(2004, nameof(Logout));
    public static readonly EventId EmailConfirmation = new(2005, nameof(EmailConfirmation));
    public static readonly EventId PasswordReset = new(2006, nameof(PasswordReset));
    public static readonly EventId PasswordChange = new(2007, nameof(PasswordChange));
    public static readonly EventId RefreshRotation = new(2008, nameof(RefreshRotation));
    public static readonly EventId RefreshReuse = new(2009, nameof(RefreshReuse));
    public static readonly EventId RefreshFamilyRevocation = new(2010, nameof(RefreshFamilyRevocation));
    public static readonly EventId OriginRejection = new(2011, nameof(OriginRejection));
    public static readonly EventId CsrfRejection = new(2012, nameof(CsrfRejection));
    public static readonly EventId JwtRejection = new(2013, nameof(JwtRejection));
    public static readonly EventId RateLimitRejection = new(2014, nameof(RateLimitRejection));
    public static readonly EventId AuthorizationRejection = new(2015, nameof(AuthorizationRejection));
    public static readonly EventId TenantRejection = new(2016, nameof(TenantRejection));
    public static readonly EventId AdministrativeContext = new(2017, nameof(AdministrativeContext));
    public static readonly EventId Moderation = new(2018, nameof(Moderation));
    public static readonly EventId GoogleSignIn = new(2019, nameof(GoogleSignIn));
    public static readonly EventId GoogleLink = new(2020, nameof(GoogleLink));
}
