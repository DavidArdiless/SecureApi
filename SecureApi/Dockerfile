# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SecureApi.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
# Imagen "chiseled": sin shell ni gestor de paquetes, con lo mínimo para correr
# la app y ya configurada para correr como usuario sin privilegios.
#
# Por qué no la variante Debian (mcr.microsoft.com/dotnet/aspnet:8.0): arrastra
# paquetes del sistema que esta API no usa, y con ellos sus CVEs. Al momento de
# este cambio traía un HIGH con fix disponible en libpcre2 (CVE-2026-103111) que
# hacía fallar el escaneo de Trivy. La chiseled escanea en cero sin necesidad de
# relajar el gate ni ignorar el hallazgo.
#
# Requiere InvariantGlobalization, que ya está activado en SecureApi.csproj:
# la imagen no incluye ICU.
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# APP_UID lo define la imagen base (usuario sin privilegios, no root).
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SecureApi.dll"]
