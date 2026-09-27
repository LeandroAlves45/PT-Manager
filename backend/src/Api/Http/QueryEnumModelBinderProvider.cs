using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Http;

/// <summary>
/// Liga enums da query string pelos mesmos nomes que o contrato publica.
/// </summary>
/// <remarks>
/// O OpenAPI descreve os enums com os nomes do <c>JsonStringEnumConverter</c> em snake_case
/// (<c>cancelled_by_client</c>), mas o binder por omissão do MVC só aceita o nome do membro
/// (<c>CancelledByClient</c>) — ou um número qualquer. Um cliente gerado a partir do contrato
/// recebia 400 em todos os valores com mais de uma palavra. Este provider aceita as duas
/// formas, sem distinguir maiúsculas, e recusa números: um inteiro fora do enum chegava ao
/// handler e podia rebentar num <c>switch</c> (500) em vez de dar 400.
/// Só atua em parâmetros de query: corpos JSON continuam com o conversor do System.Text.Json.
/// </remarks>
internal sealed class QueryEnumModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.BindingInfo.BindingSource != BindingSource.Query)
            return null;

        var enumType = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return enumType.IsEnum ? new QueryEnumModelBinder(enumType) : null;
    }

    private sealed class QueryEnumModelBinder : IModelBinder
    {
        private readonly Dictionary<string, object> _values;

        public QueryEnumModelBinder(Type enumType)
        {
            // Nome do membro e nome snake_case do contrato apontam para o mesmo valor.
            _values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in Enum.GetNames(enumType))
            {
                var value = Enum.Parse(enumType, name);
                _values.TryAdd(name, value);
                _values.TryAdd(JsonNamingPolicy.SnakeCaseLower.ConvertName(name), value);
            }
        }

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (result == ValueProviderResult.None)
                return Task.CompletedTask;

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);
            var raw = result.FirstValue?.Trim();

            if (string.IsNullOrEmpty(raw))
            {
                // Igual ao binder por omissão: vazio é "sem filtro" num enum anulável e erro
                // num obrigatório.
                if (!bindingContext.ModelMetadata.IsReferenceOrNullableType)
                    AddInvalid(bindingContext, raw ?? string.Empty);
                else
                    bindingContext.Result = ModelBindingResult.Success(null);
                return Task.CompletedTask;
            }

            if (_values.TryGetValue(raw, out var value))
                bindingContext.Result = ModelBindingResult.Success(value);
            else
                AddInvalid(bindingContext, raw);

            return Task.CompletedTask;
        }

        private static void AddInvalid(ModelBindingContext bindingContext, string raw)
        {
            var messages = bindingContext.ModelMetadata.ModelBindingMessageProvider;
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                messages.AttemptedValueIsInvalidAccessor(raw, bindingContext.ModelName));
            bindingContext.Result = ModelBindingResult.Failed();
        }
    }
}
