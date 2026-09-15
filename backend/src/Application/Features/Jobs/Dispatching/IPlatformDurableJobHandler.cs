namespace Application.Features.Jobs.Dispatching;

/// <summary>
/// Marca um handler que aceita durable jobs sem tenant, usados para conteúdo do
/// catálogo global da plataforma.
/// </summary>
/// <remarks>
/// <para>
/// O dispatcher recusa por omissão qualquer job com <c>TrainerId</c> nulo. Só um
/// handler com este marker o executa, e fá-lo com <c>TenantOrigin.System</c> e sem
/// contexto administrativo. Um job com tenant continua a passar pela validação
/// normal do personal trainer persistido, mesmo quando o handler tem este marker.
/// </para>
/// </remarks>
public interface IPlatformDurableJobHandler : IDurableJobHandler;
