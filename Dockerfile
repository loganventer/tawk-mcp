# This image serves tawk-mcp over HTTP on port 8765, never stdio.
# Publish the port on loopback only (-p 127.0.0.1:8765:8765): the bearer token is the only other guard.
# tawk accepts control socket clients of its own user only, so run the container as your user
# (--user "$(id -u):$(id -g)") and mount tawk's runtime folder at /run/tawk. scripts/docker-run.sh does all this.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/Tawk.Mcp.Core/Tawk.Mcp.Core.csproj src/Tawk.Mcp.Core/
COPY src/Tawk.Mcp.ResourceAccess/Tawk.Mcp.ResourceAccess.csproj src/Tawk.Mcp.ResourceAccess/
COPY src/Tawk.Mcp.Engines/Tawk.Mcp.Engines.csproj src/Tawk.Mcp.Engines/
COPY src/Tawk.Mcp.Managers/Tawk.Mcp.Managers.csproj src/Tawk.Mcp.Managers/
COPY src/Tawk.Mcp.Clients/Tawk.Mcp.Clients.csproj src/Tawk.Mcp.Clients/
COPY src/Tawk.Mcp.Host/Tawk.Mcp.Host.csproj src/Tawk.Mcp.Host/
RUN dotnet restore src/Tawk.Mcp.Host/Tawk.Mcp.Host.csproj
COPY . .
RUN dotnet publish src/Tawk.Mcp.Host/Tawk.Mcp.Host.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /data /run/tawk && chown "$APP_UID" /data && chmod 0700 /data
ENV ASPNETCORE_URLS=http://0.0.0.0:8765 \
    HTTP_PORTS= \
    TAWKMCP_TRANSPORT=Http \
    TAWKMCP_BIND=0.0.0.0 \
    TAWKMCP_PORT=8765 \
    TAWKMCP_TOKEN_FILE=/data/token \
    TAWKMCP_DATA_FILE=/data/memory.db \
    TAWK_CONTROL_SOCKET=/run/tawk/control.sock
VOLUME ["/data"]
EXPOSE 8765
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s CMD ["dotnet", "/app/tawk-mcp.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "/app/tawk-mcp.dll"]
