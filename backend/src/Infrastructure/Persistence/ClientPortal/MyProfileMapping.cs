using Application.Features.ClientPortal.Dtos;
using Domain.Entities.Clients;

namespace Infrastructure.Persistence.ClientPortal;

/// <summary>Projeção única da ficha do cliente para o perfil que ele próprio vê.</summary>
internal static class MyProfileMapping
{
    internal static MyProfileDto ToDto(Client client)
    {
        ArgumentNullException.ThrowIfNull(client);

        return new MyProfileDto(
            client.Name,
            client.ContactEmail,
            client.Phone,
            client.BirthDate.Value,
            client.Sex.Value,
            client.EmergencyContactName,
            client.EmergencyContactPhone,
            client.AvatarUrl,
            client.UpdatedAt);
    }
}
