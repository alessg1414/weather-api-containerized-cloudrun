# =========================================================
# Etapa 1: build
# Usamos la imagen del SDK completo (pesada) solo para compilar.
# =========================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos primero solo el .csproj para aprovechar el cache de capas de Docker:
# si no cambian las dependencias, "dotnet restore" no se vuelve a ejecutar
# aunque cambie el código fuente.
COPY DataVisionAPI.csproj ./
RUN dotnet restore DataVisionAPI.csproj

# Ahora sí copiamos el resto del código y publicamos en Release.
COPY . .
RUN dotnet publish DataVisionAPI.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# =========================================================
# Etapa 2: runtime
# Imagen liviana, solo con el runtime de ASP.NET (no el SDK).
# =========================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Cloud Run inyecta la variable de entorno PORT (8080 por defecto) y espera
# que el contenedor escuche en ese puerto. ASPNETCORE_HTTP_PORTS (disponible
# desde .NET 8) le dice a Kestrel en qué puerto escuchar sin tocar código.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DataVisionAPI.dll"]