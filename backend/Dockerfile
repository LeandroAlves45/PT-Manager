# PT Manager API — imagem de produção (Render).

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src

COPY . .

# --locked-mode: o restore falha se os packages.lock.json não corresponderem, para
# que a imagem use exatamente as versões auditadas.
RUN dotnet restore PTManager.sln --locked-mode \
 && dotnet publish src/Api/Api.csproj -c Release -o /app --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS runtime
WORKDIR /app

COPY --from=build /app .

# Utilizador sem privilégios definido pelas imagens oficiais .NET.
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Api.dll"]
