# PT Manager API — imagem de produção (Render).

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /src

# Camada de restore: só muda quando um .csproj, um lock file ou as props mudam, por isso o
# código-fonte editado não invalida o download dos pacotes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Domain/Domain.csproj src/Domain/packages.lock.json src/Domain/
COPY src/Application/Application.csproj src/Application/packages.lock.json src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/packages.lock.json src/Infrastructure/
COPY src/Api/Api.csproj src/Api/packages.lock.json src/Api/

# --locked-mode: o restore falha se os packages.lock.json não corresponderem, para
# que a imagem use exatamente as versões auditadas. Só o grafo da API é restaurado:
# os projetos de teste não entram na imagem.
RUN dotnet restore src/Api/Api.csproj --locked-mode

COPY src ./src
RUN dotnet publish src/Api/Api.csproj -c Release -o /app --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS runtime
WORKDIR /app

COPY --from=build /app .

# Utilizador sem privilégios definido pelas imagens oficiais .NET.
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Sem HEALTHCHECK: a imagem aspnet não traz curl nem wget. O Render verifica
# `/health/live` pelo próprio healthCheckPath.
ENTRYPOINT ["dotnet", "Api.dll"]
